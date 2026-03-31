using Shouldly;
using System.Runtime.CompilerServices;
using System.Text;
using Thoth.Modal;
using Thoth.Rendering;
using Thoth.Themes;
using Thoth.Widgets;
using Thoth.Tests.utilities;

namespace Thoth.Tests.themes;

public class thoth_theme_widget_preview
{
    [Fact]
    public void writes_side_by_side_widget_preview_for_thoth_day_and_night()
    {
        var day = CreateThothDayTheme().BuildPalette();
        var night = CreateThothNightTheme().BuildPalette();

        var root = new Screen
                   {
                       Title = "Thoth Theme Preview",
                       Style = new(day.Foreground, day.Background)
                   };

        var table = new Table();
        table.AddFillColumn();
        table.AddFillColumn();
        table.AddRow(BuildPanel("Thoth Day", day, isDark: false), BuildPanel("Thoth Night", night, isDark: true));
        root.Add(table);

        var (buffer, context, layout) = Render(root, 120, 24);

        buffer.WriteTerminalSnapshotSvg("thoth.theme.day_night.widget_preview.svg");
        buffer.WriteLayoutDebugSvg(root, 120, 24, "thoth.theme.day_night.widget_preview.svg", layoutState: layout);
        WriteTrueColorSvg(buffer, context, "thoth.theme.day_night.widget_preview.truecolor.svg");

        var rows = ReadRows(buffer);
        rows.Any(r => r.Contains("Thoth Day", StringComparison.Ordinal)).ShouldBeTrue();
        rows.Any(r => r.Contains("Thoth Night", StringComparison.Ordinal)).ShouldBeTrue();
    }

    static (ScreenBuffer Buffer, RenderContext Context, FrameLayoutState Layout) Render(IWidget root, int width, int height)
    {
        var engine = new FrameEngine(fullRender: false);
        var uiContext = new UiContext(root);
        var (renderBuffer, _, _) = engine.RenderFrame(root,
                                                      uiContext,
                                                      width,
                                                      height,
                                                      new Dictionary<IWidget, InvalidationKind>());

        var buffer = new ScreenBuffer(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                buffer.SetCell(x, y, renderBuffer.GetCell(x, y));

        return (buffer, new RenderContext(uiContext), engine.LayoutState);
    }

    static void WriteTrueColorSvg(ScreenBuffer buffer,
                                  RenderContext context,
                                  string snapshotFileName,
                                  [CallerFilePath] string callerFilePath = "")
    {
        var targetPath = ResolveSnapshotPath(callerFilePath, snapshotFileName);
        var parent = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);

        const int cellWidth = 10;
        const int cellHeight = 18;
        const int baseline = 13;
        var widthPx = buffer.Width * cellWidth;
        var heightPx = buffer.Height * cellHeight;

        var sb = new StringBuilder(Math.Max(4096, buffer.Width * buffer.Height * 40));
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{widthPx}\" height=\"{heightPx}\" viewBox=\"0 0 {widthPx} {heightPx}\">");
        sb.AppendLine("  <rect width=\"100%\" height=\"100%\" fill=\"#ffffff\" />");
        sb.AppendLine("  <g font-family=\"ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, Liberation Mono, monospace\" font-size=\"13\" text-rendering=\"optimizeSpeed\">");

        for (var y = 0; y < buffer.Height; y++)
        {
            var rowBackground = Color.White;
            for (var x = 0; x < buffer.Width; x++)
            {
                var cell = buffer.GetCell(x, y);
                if (cell.Width == 0) continue;

                context.Styles.TryGet(cell.StyleIndex, out var style);
                var bg = style.Background ?? rowBackground;
                var fg = style.Foreground ?? Color.Black;
                var drawWidth = Math.Max(1, (int)cell.Width) * cellWidth;
                var px = x * cellWidth;
                var py = y * cellHeight;
                rowBackground = bg;

                sb.AppendLine($"    <rect x=\"{px}\" y=\"{py}\" width=\"{drawWidth}\" height=\"{cellHeight}\" fill=\"{ToHex(bg)}\" />");

                if (cell.GlyphId != 0)
                {
                    var glyph = EscapeXml(((char)cell.GlyphId).ToString());
                    if (glyph != " ")
                        sb.AppendLine($"    <text x=\"{px + 1}\" y=\"{py + baseline}\" fill=\"{ToHex(fg)}\">{glyph}</text>");
                }
            }
        }

        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");
        File.WriteAllText(targetPath, sb.ToString());
    }

    static string ResolveSnapshotPath(string callerFilePath, string snapshotFileName)
    {
        if (Path.IsPathRooted(snapshotFileName)) return snapshotFileName;
        var callerDirectory = Path.GetDirectoryName(callerFilePath) ?? ".";
        return Path.GetFullPath(Path.Combine(callerDirectory, snapshotFileName));
    }

    static string ToHex(Color color)
    {
        return $"#{color.R:x2}{color.G:x2}{color.B:x2}";
    }

    static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
                    .Replace("<", "&lt;", StringComparison.Ordinal)
                    .Replace(">", "&gt;", StringComparison.Ordinal)
                    .Replace("\"", "&quot;", StringComparison.Ordinal)
                    .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    static IWidget BuildPanel(string title, ThemePalette palette, bool isDark)
    {
        var variants = ThemeControlVariants.From(palette, isDark);
        var body = new StackPanel();
        body.Items.Add(new TextBar
                 {
                     LeftTitle = title,
                     CenterTitle = "Modal Preview",
                     Style = new(palette.Foreground, palette.PanelBackground)
                 });

        body.Items.Add(new TextBlock
                 {
                     Text = "Focused actions, list choices, and progress stay themed.",
                     Overflow = TextOverflow.Wrap,
                     ForegroundColor = palette.Foreground,
                     BackgroundColor = palette.PanelBackground
                 });

        body.Items.Add(new ProgressBar
                 {
                     Width = 42,
                     Progress = 0.62,
                     Style = ProgressBarStyle.Solid,
                     FillColor = variants.ProgressBar.Fill,
                     TrackColor = variants.ProgressBar.Track
                 });

        var choices = new MultipleChoiceList
                      {
                          RowBackgroundColor = variants.ChoiceList.RowBackground,
                          RowForegroundColor = variants.ChoiceList.RowForeground,
                          ActiveRowBackgroundColor = variants.ChoiceList.ActiveRowBackground,
                          ActiveRowForegroundColor = variants.ChoiceList.ActiveRowForeground
                      };
        choices.SetChoices([
            new("option-a", "Enable archive index", true),
            new("option-b", "Run glyph fallback probe")
        ]);
        body.Items.Add(choices);

        var confirm = new Button
                      {
                          Text = "Confirm",
                          ForegroundColor = variants.PrimaryButton.Foreground,
                          BackgroundColor = variants.PrimaryButton.Background,
                          BorderColor = variants.PrimaryButton.Border
                      };
        var cancel = new Button
                     {
                          Text = "Cancel",
                          ForegroundColor = variants.SecondaryButton.Foreground,
                          BackgroundColor = variants.SecondaryButton.Background,
                          BorderColor = variants.SecondaryButton.Border
                     };
        var buttons = new ButtonGroup
                      {
                          DefaultButton = confirm,
                           SelectedButton = confirm,
                           ButtonGap = 1,
                           SelectedBorderColor = variants.Modal.FocusOutline
                       };
        buttons.Add(cancel);
        buttons.Add(confirm);
        body.Items.Add(buttons);

        return new Border
               {
                    BorderStyle = BorderStyle.Rounded,
                    BorderColor = variants.Modal.Border,
                    Style = new(variants.Modal.PanelForeground, variants.Modal.PanelBackground),
                    Content = body
               };
    }

    static Theme CreateThothDayTheme()
    {
        return new("Thoth (Day)",
                   "thoth-light",
                   "Sunlit lapis, gilded stone, and ruby palette.",
                   "Comptatata",
                   "light",
                   1,
                   new(new("#f7f0dc", 230, "white", "Sunlit limestone background."),
                       new("#3a2a1a", 235, "black", "Primary text, dark ink on stone."),
                       new("#6b5a3c", 95, "bright_black", "Secondary and helper text."),
                       new("#a3acc6", 146, "bright_black", "Structural borders and separators."),
                       new("#2b6fd6", 33, "blue", "Lapis-driven focus and active controls."),
                       new("#ffcc33", 220, "yellow", "Gold highlight for notifications."),
                       new("#2d8f59", 29, "green", "Success and positive states."),
                       new("#cd8526", 172, "yellow", "Warning states."),
                       new("#a5263e", 124, "red", "Error and critical states.")));
    }

    static Theme CreateThothNightTheme()
    {
        return new("Thoth (Night)",
                   "thoth-dark",
                   "Underworld lapis, bright gold, and ruby palette.",
                   "Comptatata",
                   "dark",
                   1,
                   new(new("#070b17", 233, "black", "Night chamber background."),
                       new("#e0d2ae", 223, "white", "Primary text, gilded ivory."),
                       new("#7a6e8f", 103, "bright_black", "Secondary text with amethyst undertone."),
                       new("#4b3f76", 61, "blue", "Structural borders and separators."),
                       new("#3f7cff", 69, "blue", "Focus and active controls."),
                       new("#ffcc33", 220, "yellow", "Gold notifications and highlights."),
                       new("#2e9b63", 35, "green", "Success and positive states."),
                       new("#e09a2f", 179, "yellow", "Warning states."),
                       new("#b62f4b", 125, "red", "Error and critical states.")));
    }

    static List<string> ReadRows(ScreenBuffer buffer)
    {
        var rows = new List<string>(buffer.Height);
        for (var y = 0; y < buffer.Height; y++)
        {
            var chars = new char[buffer.Width];
            for (var x = 0; x < buffer.Width; x++)
            {
                var glyphId = buffer.GetCell(x, y).GlyphId;
                chars[x] = glyphId == 0 ? ' ' : (char)glyphId;
            }

            rows.Add(new(chars));
        }

        return rows;
    }

}
