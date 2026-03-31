using System.Text.Json;
using System.Diagnostics;
using System.Text;
using Thoth;
using Thoth.Modal;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Terminal;
using Thoth.Terminal.Raw;
using Thoth.Terminal.Raw.Egress;
using Thoth.Themes;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

var options = StressOptions.Parse(args);
using var markerWriter = CreateMarkerWriter(options.MarkerOutputFile);

var theme = LoadTheme(options.ThemeName, options.ThemeVariant);
var palette = theme.BuildPalette();
var variants = ThemeControlVariants.From(palette,
                                         string.Equals(theme.Variant, "dark", StringComparison.OrdinalIgnoreCase));

var scenarios = StressScenarios.Build();
if (options.Scenarios.Count > 0)
{
    var requested = new List<string>(options.Scenarios);
    scenarios = scenarios.Where(scenario => requested.Any(r => string.Equals(r, scenario.Id, StringComparison.OrdinalIgnoreCase))).ToArray();

    if (scenarios.Count == 0)
        throw new InvalidOperationException($"No matching scenarios for --scenarios={string.Join(',', options.Scenarios)}");
}

{
    var nullScenario = scenarios.FirstOrDefault(s => s.Id == "_null");
    if (!string.IsNullOrEmpty(nullScenario.Id))
    {
        var realScenarios = scenarios.Where(s => s.Id != "_null").ToArray();
        var interleaved = new List<ScenarioDefinition> { nullScenario };
        foreach (var scenario in realScenarios)
        {
            interleaved.Add(scenario);
            interleaved.Add(nullScenario);
        }
        scenarios = interleaved;
    }
}

var warmupCases = StressCases.Build(scenarios, [options.Width], [options.Height]);

var terminalBound = options.TerminalBound;
var rawModeEnabled = false;
if (terminalBound)
{
    try
    {
        RawMode.Enable();
        rawModeEnabled = true;
    }
    catch (InvalidOperationException)
    {
        terminalBound = false;
    }
}

using SystemTerminal? terminal = terminalBound ? new() : null;
var terminalScribe = terminal is null ? null : new TerminalScribe(terminal);

Warmup(warmupCases,
       palette,
       variants,
       terminalScribe,
       options.FullRender,
       options.WarmupIterations,
       options.WarmupFrames);

WriteMarkerLine($"TIMESTAMP_FREQ:{System.Diagnostics.Stopwatch.Frequency}");

var iterWidth = options.Width;
var iterHeight = options.Height;
var executed = 0;
{
    var iterIndex = 0;
    WriteMarkerLine($"ITERATION_START:{iterIndex + 1}:{iterWidth}x{iterHeight}:{System.Diagnostics.Stopwatch.GetTimestamp()}");

    for (var scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
    {
        var scenario = scenarios[scenarioIndex];
        var caseName = scenario.Id;
        WriteMarkerLine($"BLOCK_START:{caseName}:{System.Diagnostics.Stopwatch.GetTimestamp()}");

        var reusableEngine = options.FullRender ? new FrameEngine(fullRender: true) : null;
        var reusableInvalidations = options.FullRender
            ? new Dictionary<IWidget, InvalidationKind>()
            : null;

        try
        {
            var root = scenario.Build(palette, variants);
            var uiContext = new UiContext(root);
            var engine = reusableEngine ?? new FrameEngine(options.FullRender);
            var invalidations = reusableInvalidations ?? new Dictionary<IWidget, InvalidationKind>();
            invalidations.Clear();

            var checksum = 17L;
            for (var frame = 0; frame < options.Frames; frame++)
            {
                var (buffer, frameNumber, requiresFullFrame) = engine.RenderFrame(root,
                                                                                   uiContext,
                                                                                   iterWidth,
                                                                                   iterHeight,
                                                                                   invalidations);

                if (terminalScribe is not null)
                {
                    var renderContext = new RenderContext(uiContext);
                    terminalScribe.Render(buffer, renderContext, frameNumber, requiresFullFrame);
                }

                checksum = CombineChecksum(checksum, buffer, frame, iterWidth, iterHeight);
            }

            executed++;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Scenario failure scenario={scenario.Id} iteration={iterIndex + 1}: {ex.GetType().Name}: {ex.Message}");
        }

        WriteMarkerLine($"BLOCK_END:{caseName}:{System.Diagnostics.Stopwatch.GetTimestamp()}");

        // Null scenarios are cooldown separators. Sleep 1.2s so dotnet-counters
        // (1-second sampling interval) captures at least one clean sample between
        // real scenarios, enabling per-scenario counter correlation.
        if (scenario.Id == "_null")
            Thread.Sleep(1200);
    }
}

if (terminal is not null)
{
    terminal.WriteRaw(TerminalProtocolSequences.Sgr.ResetBytes);
    terminal.WriteRaw(TerminalProtocolSequences.Csi.ShowCursorBytes);
}

if (rawModeEnabled)
    RawMode.Disable();

Console.WriteLine($"Stress test finished. Executed runs: {executed}.");

void WriteMarkerLine(string line)
{
    if (markerWriter is null)
    {
        Console.WriteLine(line);
        return;
    }

    markerWriter.WriteLine(line);
}

static StreamWriter? CreateMarkerWriter(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return null;

    var fullPath = Path.GetFullPath(path);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
        Directory.CreateDirectory(directory);

    var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
    return new StreamWriter(stream) { AutoFlush = true };
}

static Theme LoadTheme(string themeName, string? variant)
{
    try
    {
        Themes.Load(themeName, variant);
        if (Themes.Current is { } loaded)
            return loaded;
    }
    catch (InvalidOperationException)
    {
    }

    var themesDirectory = ResolveThemesDirectory();
    var pattern = string.IsNullOrWhiteSpace(variant)
        ? $"{themeName}.*.theme.json"
        : $"{themeName}.{variant}.theme.json";

    var files = Directory.GetFiles(themesDirectory, pattern)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                         .ToArray();
    if (files.Length == 0)
        throw new InvalidOperationException($"No theme files found for '{themeName}' in '{themesDirectory}'.");

    using var stream = File.OpenRead(files[0]);
    var theme = JsonSerializer.Deserialize(stream, ThemeJsonContext.Default.Theme);
    return theme ?? throw new InvalidOperationException($"Failed to deserialize theme from '{files[0]}'.");
}

static string ResolveThemesDirectory()
{
    var repoRoot = Environment.GetEnvironmentVariable("THOTH_REPO_ROOT");
    var candidates = new[]
    {
        string.IsNullOrWhiteSpace(repoRoot)
            ? string.Empty
            : Path.GetFullPath(Path.Combine(repoRoot!, "src", "dotnet", "Thoth", "Themes")),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                                      "..",
                                      "..",
                                      "..",
                                      "..",
                                      "..",
                                      "dotnet",
                                      "Thoth",
                                      "Themes")),
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "dotnet", "Thoth", "Themes")),
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "Thoth", "Themes"))
    };

    for (var i = 0; i < candidates.Length; i++)
        if (Directory.Exists(candidates[i]))
            return candidates[i];

    throw new InvalidOperationException($"Unable to locate Thoth theme directory for stress run. base={AppContext.BaseDirectory}; cwd={Environment.CurrentDirectory}; repo={repoRoot ?? "<unset>"}");
}

static long CombineChecksum(long current, GridBuffer buffer, int frame, int width, int height)
{
    if (width <= 0 || height <= 0) return current;
    var x = frame % width;
    var y = (frame * 7) % height;
    var cell = buffer.GetCell(x, y);
    unchecked
    {
        return (current * 31) + cell.GlyphId + (cell.StyleIndex * 13);
    }
}

static void Warmup(IReadOnlyList<ScenarioCase> cases,
                   ThemePalette palette,
                   ThemeControlVariants variants,
                   TerminalScribe? terminalScribe,
                   bool fullRender,
                   int warmupIterations,
                   int warmupFrames)
{
    if (warmupIterations <= 0 || warmupFrames <= 0) return;

    for (var caseIndex = 0; caseIndex < cases.Count; caseIndex++)
    {
        var testCase = cases[caseIndex];
        var reusableEngine = fullRender ? new FrameEngine(fullRender: true) : null;
        var reusableInvalidations = fullRender
            ? new Dictionary<IWidget, InvalidationKind>()
            : null;

        for (var iteration = 0; iteration < warmupIterations; iteration++)
        {
            var root = testCase.Build(palette, variants);
            var uiContext = new UiContext(root);
            var engine = reusableEngine ?? new FrameEngine(fullRender);
            var invalidations = reusableInvalidations ?? new Dictionary<IWidget, InvalidationKind>();
            invalidations.Clear();

            for (var frame = 0; frame < warmupFrames; frame++)
            {
                var (buffer, frameNumber, requiresFullFrame) = engine.RenderFrame(root,
                                                                                   uiContext,
                                                                                   testCase.Width,
                                                                                   testCase.Height,
                                                                                   invalidations);

                if (terminalScribe is not null)
                {
                    var renderContext = new RenderContext(uiContext);
                    terminalScribe.Render(buffer, renderContext, frameNumber, requiresFullFrame);
                }
            }
        }
    }
}

static class StressScenarios
{
     public static IReadOnlyList<ScenarioDefinition> Build()
     {
         return
         [
             new("_null", BuildNull),
             new("border_gallery", BuildBorderGallery),
             new("dashboard", BuildDashboard),
             new("dense_table", BuildDenseTable),
             new("dock_panel_viewport", BuildDockPanelViewport),
             new("mixed_controls", BuildMixedControls),
             new("overlay_modal_multiple", BuildOverlayModalMultiple),
             new("overlay_modal_single", BuildOverlayModalSingle),
             new("settings_panel", BuildSettingsPanel),
             new("split_workspace", BuildSplitWorkspace),
             new("text_editor", BuildTextEditor),
             new("text_overflow_gallery", BuildTextOverflowGallery),
             new("toggle_matrix", BuildToggleMatrix)
         ];
     }

    static IWidget BuildDashboard(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Dashboard", palette);
        var stack = new StackPanel();

        stack.Items.Add(new TextBar
                  {
                      CenterTitle = "Stress Dashboard",
                      Line = "─",
                      Style = new(palette.Foreground, palette.Separator)
                  });

        stack.Items.Add(new TextBlock
                  {
                       Text = "Deterministic render pass over themed controls.",
                      Overflow = TextOverflow.Wrap,
                      ForegroundColor = palette.Foreground,
                      BackgroundColor = palette.PanelBackground
                  });

        stack.Items.Add(new ProgressBar
                  {
                      Width = 38,
                      Progress = 0.73,
                      FillColor = variants.ProgressBar.Fill,
                      TrackColor = variants.ProgressBar.Track,
                      Style = ProgressBarStyle.Solid
                  });

        root.Add(new Border
                 {
                     BorderStyle = BorderStyle.Rounded,
                     Style = new(palette.Separator, palette.PanelBackground),
                     Content = stack
                 });
        return root;
    }

    static IWidget BuildOverlayModalMultiple(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Modal Multiple", palette);
        var overlay = new OverlayWidget
                      {
                          Content = new TextBlock
                                    {
                                        Text = "Background content remains mounted behind modal overlay.",
                                        Overflow = TextOverflow.Wrap,
                                        ForegroundColor = palette.Foreground,
                                        BackgroundColor = palette.Background
                                    }
                      };

        var list = new MultipleChoiceList
                   {
                       RowBackgroundColor = variants.ChoiceList.RowBackground,
                       RowForegroundColor = variants.ChoiceList.RowForeground,
                       ActiveRowBackgroundColor = variants.ChoiceList.ActiveRowBackground,
                       ActiveRowForegroundColor = variants.ChoiceList.ActiveRowForeground
                   };
        list.SetChoices(
        [
            new("choice-a", "Enable OSC progress reporting", true),
            new("choice-b", "Capture runtime timing samples"),
            new("choice-c", "Exercise fallback rendering paths")
        ]);

        var body = new StackPanel();
        body.Items.Add(new TextBlock
                 {
                     Text = "Pick any stress toggles, then continue.",
                     Overflow = TextOverflow.Wrap,
                     ForegroundColor = palette.Foreground,
                     BackgroundColor = variants.Modal.PanelBackground
                 });
        body.Items.Add(list);

        var dialog = NewDialog("Stress Multiple Choice", body, false, palette, variants);
        AddModalButtons(dialog, variants, ("Continue", true), ("Cancel", false));
        overlay.Show(dialog);

        root.Add(overlay);
        return root;
    }

    static IWidget BuildOverlayModalSingle(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Modal Single", palette);
        var overlay = new OverlayWidget
                      {
                          Content = new TextBlock
                                    {
                                        Text = "Single-choice scenario with strict deterministic order.",
                                        Overflow = TextOverflow.Wrap,
                                        ForegroundColor = palette.Foreground,
                                        BackgroundColor = palette.Background
                                    }
                      };

        var list = new SingleChoiceList
                   {
                       RowBackgroundColor = variants.ChoiceList.RowBackground,
                       RowForegroundColor = variants.ChoiceList.RowForeground,
                       ActiveRowBackgroundColor = variants.ChoiceList.ActiveRowBackground,
                       ActiveRowForegroundColor = variants.ChoiceList.ActiveRowForeground,
                       CheckedForegroundColor = variants.ChoiceList.CheckedForeground
                   };
        list.SetChoices(
        [
            new("small", "Small graph"),
            new("medium", "Medium graph", true),
            new("large", "Large graph")
        ]);

        var body = new StackPanel();
        body.Items.Add(new TextBlock
                 {
                     Text = "Select stress graph size.",
                     Overflow = TextOverflow.Wrap,
                     ForegroundColor = palette.Foreground,
                     BackgroundColor = variants.Modal.PanelBackground
                 });
        body.Items.Add(list);

        var dialog = NewDialog("Stress Size", body, true, palette, variants);
        AddModalButtons(dialog, variants, ("Run", true));
        overlay.Show(dialog);

        root.Add(overlay);
        return root;
    }

    static IWidget BuildDenseTable(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Dense Table", palette);
        var table = new Table();
        table.AddAutoColumn();
        table.AddProportionalColumn(2);
        table.AddFillColumn(3);

        for (var i = 0; i < 24; i++)
        {
            table.AddRow(new TextBlock
                         {
                              Text = $"R{i:00}",
                              ForegroundColor = palette.MutedText,
                              BackgroundColor = variants.ChoiceList.RowBackground
                          },
                          new TextBlock
                          {
                              Text = "Synthetic stress row payload",
                              Overflow = TextOverflow.Clip,
                             ForegroundColor = palette.Foreground,
                             BackgroundColor = variants.ChoiceList.RowBackground
                         },
                         new ProgressBar
                         {
                             Width = 18,
                             Progress = (i % 10) / 9d,
                             Style = i % 2 == 0 ? ProgressBarStyle.Solid : ProgressBarStyle.Pulse,
                             FillColor = variants.ProgressBar.Fill,
                             TrackColor = variants.ProgressBar.Track
                         });
        }

        root.Add(new Viewport { Content = table });
        return root;
    }

    static IWidget BuildMixedControls(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Mixed Controls", palette);
        var stack = new StackPanel();

        stack.Items.Add(new Spinner
                  {
                      Dial = SpinnerDial.Kit,
                      LaneWidth = 15,
                      TrailRadius = 3,
                      ForegroundColor = variants.PrimaryButton.Background,
                      BackgroundColor = palette.PanelBackground
                  });

        var group = new ButtonGroup { SelectedBorderColor = variants.Modal.FocusOutline };
        var primary = new Button
                      {
                          Text = "Primary",
                          BackgroundColor = variants.PrimaryButton.Background,
                          ForegroundColor = variants.PrimaryButton.Foreground,
                          BorderColor = variants.PrimaryButton.Border
                      };
        var secondary = new Button
                        {
                            Text = "Secondary",
                            BackgroundColor = variants.SecondaryButton.Background,
                            ForegroundColor = variants.SecondaryButton.Foreground,
                            BorderColor = variants.SecondaryButton.Border
                        };
        group.Add(primary);
        group.Add(secondary);
        group.DefaultButton = primary;
        group.SelectedButton = secondary;

        stack.Items.Add(group);
        stack.Items.Add(new TextBlock
                  {
                      Text = "Deterministic mixed control graph for stress pass.",
                      Overflow = TextOverflow.Wrap,
                      ForegroundColor = palette.Foreground,
                      BackgroundColor = palette.PanelBackground
                  });

        root.Add(new Border
                 {
                     BorderStyle = BorderStyle.Single,
                     Style = new(palette.Separator, palette.PanelBackground),
                     Content = stack
                 });
        return root;
    }

    static IWidget BuildTextEditor(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Text Editor", palette);
        var stack = new StackPanel();

        stack.Items.Add(new TextBar
                  {
                      CenterTitle = "Editor Replay",
                      RightTitle = "Ctrl+Enter",
                      Line = "━",
                      Style = new(palette.Foreground, palette.PanelBackground)
                  });

        stack.Items.Add(new TextEditor
                  {
                      Text = "Deterministic editor payload with wrapped lines and punctuation.\nSecond line with symbols: []{}<>() and unicode width checks αβγ.",
                      Style = new(palette.Foreground, palette.PanelBackground),
                      MinHeight = 8
                  });

        stack.Items.Add(new ProgressBar
                  {
                      Width = 38,
                      Progress = 0.58,
                      FillColor = variants.ProgressBar.Fill,
                      TrackColor = variants.ProgressBar.Track,
                      Style = ProgressBarStyle.Pulse
                  });

        root.Add(new Border
                 {
                     BorderStyle = BorderStyle.Outline,
                     Style = new(palette.Separator, palette.PanelBackground),
                     Content = stack
                 });
        return root;
    }

    static IWidget BuildDockPanelViewport(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Dock+Viewport", palette);
        var dock = new DockPanel();

        dock.Add(new Dock
                 {
                     Position = DockPosition.Top,
                     MaximumHeight = 1,
                     Content = new TextBar
                               {
                                   CenterTitle = "Docked Header",
                                   Line = "─",
                                   Style = new(palette.Foreground, palette.Separator)
                               }
                 });

        dock.Add(new Dock
                 {
                     Position = DockPosition.Bottom,
                     MaximumHeight = 1,
                     Content = new ProgressBar
                               {
                                   Width = 48,
                                   Progress = 0.81,
                                   FillColor = variants.ProgressBar.Fill,
                                   TrackColor = variants.ProgressBar.Track,
                                   Style = ProgressBarStyle.Solid
                               }
                 });

        var body = new StackPanel();
        for (var i = 0; i < 16; i++)
        {
            body.Items.Add(new TextBlock
                     {
                         Text = $"Viewport row {i:00} with deterministic payload and clipped trailing detail.",
                         Overflow = TextOverflow.Clip,
                         ForegroundColor = palette.Foreground,
                         BackgroundColor = i % 2 == 0 ? palette.PanelBackground : palette.Background
                     });
        }

        dock.Add(new Dock
                 {
                     Position = DockPosition.Fill,
                     Content = new Viewport { Content = body }
                 });

        root.Add(dock);
        return root;
    }

    static IWidget BuildToggleMatrix(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Toggle Matrix", palette);
        var table = new Table();
        table.AddAutoColumn();
        table.AddAutoColumn();
        table.AddFillColumn(4);

        for (var i = 0; i < 18; i++)
        {
            table.AddRow(new Toggle
                         {
                             IsChecked = i % 3 != 0,
                             BackgroundColor = variants.ChoiceList.RowBackground,
                             CheckedForegroundColor = variants.ChoiceList.CheckedForeground,
                             UncheckedForegroundColor = variants.ChoiceList.RowForeground
                         },
                         new TextBlock
                         {
                             Text = " ",
                             BackgroundColor = variants.ChoiceList.RowBackground
                         },
                         new TextBlock
                         {
                             Text = $"Feature gate #{i:00}",
                             Overflow = TextOverflow.Clip,
                             ForegroundColor = variants.ChoiceList.RowForeground,
                             BackgroundColor = variants.ChoiceList.RowBackground
                         });
        }

        root.Add(new Viewport { Content = table });
        return root;
    }

     static IWidget BuildNull(ThemePalette palette, ThemeControlVariants variants)
     {
         return NewRoot("Null Scenario", palette);
     }

     static IWidget BuildBorderGallery(ThemePalette palette, ThemeControlVariants variants)
     {
         var root = NewRoot("Stress Border Gallery", palette);
         var stack = new StackPanel();
         var styles = new[] { BorderStyle.Single, BorderStyle.Rounded, BorderStyle.Outline, BorderStyle.Inset };

         for (var i = 0; i < styles.Length; i++)
         {
             stack.Items.Add(new Border
                      {
                          BorderStyle = styles[i],
                          Style = new(palette.Separator, palette.PanelBackground),
                          Content = new TextBlock
                                    {
                                        Text = $"{styles[i]} chrome with themed body",
                                        Overflow = TextOverflow.Clip,
                                        ForegroundColor = palette.Foreground,
                                        BackgroundColor = i % 2 == 0 ? palette.PanelBackground : variants.ChoiceList.RowBackground
                                    }
                      });
         }

         root.Add(stack);
         return root;
     }

    static IWidget BuildTextOverflowGallery(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Stress Overflow Modes", palette);
        var stack = new StackPanel();

        stack.Items.Add(new TextBlock
                  {
                      Text = "Wrap mode keeps natural flow for longer descriptions in constrained widths.",
                      Overflow = TextOverflow.Wrap,
                      ForegroundColor = palette.Foreground,
                      BackgroundColor = palette.PanelBackground
                  });
        stack.Items.Add(new TextBlock
                  {
                      Text = "Clip mode hard-cuts trailing payload for deterministic rendering output in stress mode.",
                      Overflow = TextOverflow.Clip,
                      ForegroundColor = palette.Foreground,
                      BackgroundColor = variants.ChoiceList.RowBackground
                  });
        stack.Items.Add(new TextBlock
                  {
                      Text = "Ellipsis mode demonstrates truncation marker semantics for narrow terminal bounds.",
                      Overflow = TextOverflow.Ellipsis,
                      ForegroundColor = palette.Foreground,
                      BackgroundColor = palette.PanelBackground
                  });
        stack.Items.Add(new Spinner
                  {
                      Dial = SpinnerDial.Braille,
                      Speed = 5,
                      ForegroundColor = variants.PrimaryButton.Background,
                      BackgroundColor = palette.PanelBackground
                  });

        root.Add(new Border
                 {
                     BorderStyle = BorderStyle.Rounded,
                     Style = new(variants.Modal.Border, variants.Modal.PanelBackground),
                     Content = stack
                 });
        return root;
    }

    static IWidget BuildSettingsPanel(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Settings Panel", palette);
        var dock = new DockPanel();

        dock.Add(new Dock
                 {
                     Position = DockPosition.Top,
                     MaximumHeight = 1,
                     Content = new TextBar
                               {
                                   CenterTitle = "Settings",
                                   Line = "─",
                                   Style = new(palette.Foreground, palette.Separator)
                               }
                 });

        var buttons = new ButtonGroup { SelectedBorderColor = variants.Modal.FocusOutline };
        var apply = new Button
                    {
                        Text = "Apply",
                        BackgroundColor = variants.PrimaryButton.Background,
                        ForegroundColor = variants.PrimaryButton.Foreground,
                        BorderColor = variants.PrimaryButton.Border
                    };
        var discard = new Button
                      {
                          Text = "Discard",
                          BackgroundColor = variants.SecondaryButton.Background,
                          ForegroundColor = variants.SecondaryButton.Foreground,
                          BorderColor = variants.SecondaryButton.Border
                      };
        buttons.Add(apply);
        buttons.Add(discard);
        buttons.DefaultButton = apply;
        buttons.SelectedButton = apply;

        dock.Add(new Dock
                 {
                     Position = DockPosition.Bottom,
                     MaximumHeight = 1,
                     Content = new Thoth.Widgets.Layout.Align
                               {
                                   HorizontalAlignment = HorizontalAlignment.Right,
                                   WidthSizeMode = WidthSizeMode.Fill,
                                   Content = buttons
                               }
                 });

        var settingsStack = new StackPanel();

        var appearanceChoices = new SingleChoiceList
                                {
                                    RowBackgroundColor = variants.ChoiceList.RowBackground,
                                    RowForegroundColor = variants.ChoiceList.RowForeground,
                                    ActiveRowBackgroundColor = variants.ChoiceList.ActiveRowBackground,
                                    ActiveRowForegroundColor = variants.ChoiceList.ActiveRowForeground,
                                    CheckedForegroundColor = variants.ChoiceList.CheckedForeground
                                };
        appearanceChoices.SetChoices(
        [
            new("light", "Light"),
            new("dark", "Dark", true),
            new("high-contrast", "High Contrast"),
            new("solarized", "Solarized")
        ]);

        var appearanceStack = new StackPanel();
        appearanceStack.Items.Add(new Thoth.Widgets.Layout.Align
                            {
                                HorizontalAlignment = HorizontalAlignment.Center,
                                WidthSizeMode = WidthSizeMode.Fill,
                                Content = new TextBlock
                                          {
                                              Text = "Appearance",
                                              Overflow = TextOverflow.Clip,
                                              ForegroundColor = palette.Foreground,
                                              BackgroundColor = palette.PanelBackground
                                          }
                            });
        appearanceStack.Items.Add(appearanceChoices);

        settingsStack.Items.Add(new Border
                          {
                              BorderStyle = BorderStyle.Single,
                              Style = new(palette.Separator, palette.PanelBackground),
                              Content = appearanceStack
                          });

        var editorStack = new StackPanel();
        editorStack.Items.Add(new TextBlock
                        {
                            Text = "Editor Configuration",
                            Overflow = TextOverflow.Clip,
                            ForegroundColor = palette.Foreground,
                            BackgroundColor = palette.PanelBackground
                        });
        editorStack.Items.Add(new TextEditor
                        {
                            Text = "Editable content in settings form. Tab width, indent size, word wrap preferences.",
                            Style = new(palette.Foreground, palette.PanelBackground),
                            MinHeight = 2
                        });
        editorStack.Items.Add(new Thoth.Widgets.Layout.Align
                        {
                            HorizontalAlignment = HorizontalAlignment.Right,
                            WidthSizeMode = WidthSizeMode.Fill,
                            Content = new ProgressBar
                                      {
                                          Width = 20,
                                          Progress = 0.6,
                                          FillColor = variants.ProgressBar.Fill,
                                          TrackColor = variants.ProgressBar.Track,
                                          Style = ProgressBarStyle.Solid
                                      }
                        });

        settingsStack.Items.Add(new Border
                          {
                              BorderStyle = BorderStyle.Rounded,
                              Style = new(palette.Separator, palette.PanelBackground),
                              Content = editorStack
                          });

        var diagnosticsStack = new StackPanel();
        diagnosticsStack.Items.Add(new TextBlock
                             {
                                 Text = "System Diagnostics",
                                 Overflow = TextOverflow.Clip,
                                 ForegroundColor = palette.Foreground,
                                 BackgroundColor = palette.PanelBackground
                             });
        diagnosticsStack.Items.Add(new ProgressBar
                             {
                                 Width = 30,
                                 Progress = 0.33,
                                 FillColor = variants.ProgressBar.Fill,
                                 TrackColor = variants.ProgressBar.Track,
                                 Style = ProgressBarStyle.Pulse
                             });
        diagnosticsStack.Items.Add(new Toggle
                             {
                                 IsChecked = true,
                                 BackgroundColor = palette.PanelBackground,
                                 CheckedForegroundColor = variants.ChoiceList.CheckedForeground,
                                 UncheckedForegroundColor = variants.ChoiceList.RowForeground
                             });
        diagnosticsStack.Items.Add(new Spinner
                             {
                                 Dial = SpinnerDial.Braille,
                                 Speed = 5,
                                 ForegroundColor = variants.PrimaryButton.Background,
                                 BackgroundColor = palette.PanelBackground
                             });

        settingsStack.Items.Add(new Border
                          {
                              BorderStyle = BorderStyle.Outline,
                              Style = new(palette.Separator, palette.PanelBackground),
                              Content = diagnosticsStack
                          });

        dock.Add(new Dock
                 {
                     Position = DockPosition.Fill,
                     Content = new Viewport { Content = settingsStack }
                 });

        root.Add(dock);
        return root;
    }

    static IWidget BuildSplitWorkspace(ThemePalette palette, ThemeControlVariants variants)
    {
        var root = NewRoot("Split Workspace", palette);
        var dock = new DockPanel();

        dock.Add(new Dock
                 {
                     Position = DockPosition.Top,
                     MaximumHeight = 1,
                     Content = new TextBar
                               {
                                   CenterTitle = "Data Workspace",
                                   RightTitle = "Editor",
                                   Line = "━",
                                   Style = new(palette.Foreground, palette.PanelBackground)
                               }
                 });

        dock.Add(new Dock
                 {
                     Position = DockPosition.Bottom,
                     MaximumHeight = 1,
                     Content = new ProgressBar
                               {
                                   Width = 40,
                                   Progress = 0.5,
                                   FillColor = variants.ProgressBar.Fill,
                                   TrackColor = variants.ProgressBar.Track,
                                   Style = ProgressBarStyle.Solid
                               }
                 });

        var split = new Table();
        split.AddAutoColumn();
        split.AddFillColumn(4);

        var navigationStack = new StackPanel();
        navigationStack.Items.Add(new TextBlock
                            {
                                Text = "Navigation",
                                Overflow = TextOverflow.Clip,
                                ForegroundColor = palette.Foreground,
                                BackgroundColor = palette.PanelBackground
                            });

        for (var i = 0; i < 3; i++)
        {
            navigationStack.Items.Add(new Toggle
                                {
                                    IsChecked = i % 2 == 0,
                                    BackgroundColor = palette.PanelBackground,
                                    CheckedForegroundColor = variants.ChoiceList.CheckedForeground,
                                    UncheckedForegroundColor = variants.ChoiceList.RowForeground
                                });
        }

        navigationStack.Items.Add(new Spinner
                            {
                                Dial = SpinnerDial.Kit,
                                LaneWidth = 12,
                                TrailRadius = 2,
                                ForegroundColor = variants.PrimaryButton.Background,
                                BackgroundColor = palette.PanelBackground
                            });
        navigationStack.Items.Add(new Thoth.Widgets.Layout.Align
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                WidthSizeMode = WidthSizeMode.Content,
                                Content = new TextBlock
                                          {
                                              Text = "v1.0",
                                              Overflow = TextOverflow.Clip,
                                              ForegroundColor = palette.MutedText,
                                              BackgroundColor = palette.PanelBackground
                                          }
                            });

        var leftPane = new Border
                       {
                           BorderStyle = BorderStyle.Single,
                           Style = new(palette.Separator, palette.PanelBackground),
                           Content = navigationStack
                       };

        var recordsDock = new DockPanel();
        recordsDock.Add(new Dock
                        {
                            Position = DockPosition.Top,
                            MaximumHeight = 1,
                            Content = new Thoth.Widgets.Layout.Align
                                      {
                                          HorizontalAlignment = HorizontalAlignment.Center,
                                          WidthSizeMode = WidthSizeMode.Fill,
                                          Content = new TextBlock
                                                    {
                                                        Text = "Records",
                                                        Overflow = TextOverflow.Clip,
                                                        ForegroundColor = palette.Foreground,
                                                        BackgroundColor = palette.PanelBackground
                                                    }
                                      }
                        });

        var recordsTable = new Table();
        recordsTable.AddAutoColumn();
        recordsTable.AddFillColumn(3);
        recordsTable.AddAutoColumn();
        recordsTable.AddAutoColumn();

        for (var row = 0; row < 8; row++)
        {
            var rowBackground = row % 2 == 0 ? palette.PanelBackground : variants.ChoiceList.RowBackground;
            recordsTable.AddRow(new TextBlock
                                {
                                    Text = $"R{row:00}",
                                    Overflow = TextOverflow.Clip,
                                    ForegroundColor = palette.MutedText,
                                    BackgroundColor = rowBackground
                                },
                                new TextEditor
                                {
                                    Text = $"Record {row} editable content.",
                                    Style = new(palette.Foreground, rowBackground),
                                    MinHeight = 1
                                },
                                new ProgressBar
                                {
                                    Width = 10,
                                    Progress = row / 7d,
                                    FillColor = variants.ProgressBar.Fill,
                                    TrackColor = variants.ProgressBar.Track,
                                    Style = row % 2 == 0 ? ProgressBarStyle.Solid : ProgressBarStyle.Pulse
                                },
                                new Spinner
                                {
                                    Dial = SpinnerDial.Braille,
                                    Speed = 5,
                                    ForegroundColor = variants.PrimaryButton.Background,
                                    BackgroundColor = rowBackground
                                });
        }

        recordsDock.Add(new Dock
                        {
                            Position = DockPosition.Fill,
                            Content = new Viewport { Content = recordsTable }
                        });

        split.AddRow(leftPane, recordsDock);

        dock.Add(new Dock
                 {
                     Position = DockPosition.Fill,
                     Content = split
                 });

        root.Add(dock);
        return root;
    }


    static Screen NewRoot(string title, ThemePalette palette)
    {
        return new()
               {
                   Title = title,
                   Style = new(palette.Foreground, palette.Background)
               };
    }

    static ModalDialog NewDialog(string title,
                                 IWidget content,
                                 bool mandatory,
                                 ThemePalette palette,
                                 ThemeControlVariants variants)
    {
        return new()
               {
                   Title = title,
                   Mandatory = mandatory,
                   FooterVisible = true,
                   Style = new(variants.Modal.Border, variants.Modal.PanelBackground),
                   HeaderStyle = new(variants.Modal.PanelForeground,
                                     variants.Modal.PanelBackground,
                                     Attributes: Thoth.Rendering.TextAttributes.Bold),
                   CloseButtonBackgroundColor = palette.Error,
                   CloseButtonForegroundColor = ReadableTextOn(palette.Error),
                   Content = new Viewport { Content = content }
               };
    }

    static void AddModalButtons(ModalDialog dialog,
                                ThemeControlVariants variants,
                                params (string Label, bool IsDefault)[] buttons)
    {
        dialog.FooterButtons.SelectedBorderColor = variants.Modal.FocusOutline;
        for (var i = 0; i < buttons.Length; i++)
        {
            var definition = buttons[i];
            var button = new Button
                         {
                             Text = definition.Label,
                             BackgroundColor = definition.IsDefault
                                 ? variants.PrimaryButton.Background
                                 : variants.SecondaryButton.Background,
                             ForegroundColor = definition.IsDefault
                                 ? variants.PrimaryButton.Foreground
                                 : variants.SecondaryButton.Foreground,
                             BorderColor = definition.IsDefault
                                 ? variants.PrimaryButton.Border
                                 : variants.SecondaryButton.Border
                         };

            dialog.FooterButtons.Add(button);
            if (definition.IsDefault)
                dialog.FooterButtons.DefaultButton = button;
        }
    }

    static Color ReadableTextOn(Color color)
    {
        var luminance = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
        return luminance >= 150 ? new Color(22, 22, 28) : Color.White;
    }
}

static class StressCases
{
    public static IReadOnlyList<ScenarioCase> Build(IReadOnlyList<ScenarioDefinition> scenarios,
                                                    IReadOnlyList<int> widths,
                                                    IReadOnlyList<int> heights)
    {
        var cases = new List<ScenarioCase>(scenarios.Count * widths.Count * heights.Count);
        for (var scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
        {
            var scenario = scenarios[scenarioIndex];
            for (var widthIndex = 0; widthIndex < widths.Count; widthIndex++)
            {
                for (var heightIndex = 0; heightIndex < heights.Count; heightIndex++)
                {
                    var width = widths[widthIndex];
                    var height = heights[heightIndex];
                    cases.Add(new($"{scenario.Id}/w{width}/h{height}",
                                  width,
                                  height,
                                  scenario.Build));
                }
            }
        }

        return cases;
    }
}

readonly record struct ScenarioDefinition(string Id, Func<ThemePalette, ThemeControlVariants, IWidget> Build);

readonly record struct ScenarioCase(string Id,
                                    int Width,
                                    int Height,
                                    Func<ThemePalette, ThemeControlVariants, IWidget> BuildWidget)
{
    public IWidget Build(ThemePalette palette, ThemeControlVariants variants)
    {
        return BuildWidget(palette, variants);
    }
}

sealed record StressOptions(
    string ThemeName,
    string? ThemeVariant,
    int Iterations,
    int? DurationSeconds,
    int Frames,
    int WarmupIterations,
    int WarmupFrames,
    bool FullRender,
    bool TerminalBound,
    int Width,
    int Height,
    IReadOnlyList<string> Scenarios,
    string? MarkerOutputFile)
{
    public static StressOptions Parse(string[] args)
    {
        var map = ParseMap(args);

        var themeName = Get(map, "theme") ?? "thoth";
        var themeVariant = Get(map, "variant");
        var markerOutputFile = Get(map, "marker-output-file");
        var iterations = ParseInt(Get(map, "iterations"), 2, min: 1, max: 10_000);
        var durationSeconds = ParseNullableInt(Get(map, "duration"), min: 1, max: 86_400);
        var frames = ParseInt(Get(map, "frames"), 300, min: 1, max: 100_000);
        var warmupIterations = ParseInt(Get(map, "warmup-iterations"), 0, min: 0, max: 1_000);
        var warmupFrames = ParseInt(Get(map, "warmup-frames"), 0, min: 0, max: 10_000);
        var fullRender = ParseBool(Get(map, "full-render"), defaultValue: true);
        var terminalBound = ParseBool(Get(map, "terminal-bound"), defaultValue: false);
        var width = ParseInt(Get(map, "width"), 80, min: 1, max: 10_000);
        var height = ParseInt(Get(map, "height"), 24, min: 1, max: 10_000);
        var scenarios = ParseStringList(Get(map, "scenarios"));
        


        return new(themeName,
                   themeVariant,
                   iterations,
                   durationSeconds,
                   frames,
                   warmupIterations,
                   warmupFrames,
                   fullRender,
                   terminalBound,
                   width,
                   height,
                   scenarios,
                   markerOutputFile);
    }

    static Dictionary<string, string> ParseMap(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal)) continue;

            var key = token[2..];
            var value = "true";
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
                i++;
            }

            map[key] = value;
        }

        return map;
    }

    static string? Get(IReadOnlyDictionary<string, string> map, string key)
    {
        return map.TryGetValue(key, out var value) ? value : null;
    }

    static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed)) return defaultValue;
        return Math.Clamp(parsed, min, max);
    }

    static int? ParseNullableInt(string? raw, int min, int max)
    {
        if (!int.TryParse(raw, out var parsed)) return null;
        return Math.Clamp(parsed, min, max);
    }

    static bool ParseBool(string? raw, bool defaultValue)
    {
        if (raw is null) return defaultValue;
        if (bool.TryParse(raw, out var parsed)) return parsed;
        return defaultValue;
    }

    static IReadOnlyList<int> ParseList(string? raw, int[] defaults)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaults;

        var values = raw.Split(',')
                        .Select(static s => s.Trim())
                        .Where(static s => s.Length > 0)
                        .Select(static s => int.TryParse(s, out var value) ? value : -1)
                        .Where(static value => value > 0)
                        .Distinct()
                        .OrderBy(static value => value)
                        .ToArray();

        return values.Length == 0 ? defaults : values;
    }

    static IReadOnlyList<string> ParseStringList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();

        return raw.Split(',')
                  .Select(static s => s.Trim())
                  .Where(static s => s.Length > 0)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .ToArray();
    }
}
