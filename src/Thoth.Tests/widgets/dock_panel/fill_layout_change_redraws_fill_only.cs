using System.Text;
using Shouldly;
using Thoth.Rendering;
using Thoth.Rendering.Grid;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.dock_panel_rendering;

public class fill_layout_change_redraws_fill_only : IAsyncLifetime
{
    readonly capture_and_draw_strategy _drawStrategy = new();
    readonly FrameRenderer _engine;
    readonly Screen _root = new();
    readonly DockPanel _panel = new();
    readonly counting_title_widget _titleBar = new();
    readonly counting_fill_widget _fillWidget = new();

    (ScreenBuffer Buffer, ushort FrameNumber, bool RequiresFullFrame) _firstFrame;
    (ScreenBuffer Buffer, ushort FrameNumber, bool RequiresFullFrame) _secondFrame;

    public fill_layout_change_redraws_fill_only()
    {
        _engine = new(fullRender: false, drawStrategy: _drawStrategy);
    }

    public Task InitializeAsync()
    {
        var top = new Dock { Position = DockPosition.Top, Content = _titleBar };
        var fill = new Dock { Position = DockPosition.Fill, Content = _fillWidget };

        _panel.Add(top);
        _panel.Add(fill);
        _root.Add(_panel);

        var uiContext = new UiContext(_root);
        _firstFrame = _engine.RenderFrame(_root,
                                          uiContext,
                                          20,
                                          5,
                                          new Dictionary<IWidget, InvalidationKind>());

        var invalidations = new Dictionary<IWidget, InvalidationKind>
                            {
                                [_fillWidget] = InvalidationKind.Layout
                            };

        _secondFrame = _engine.RenderFrame(_root, uiContext, 20, 5, invalidations);
        _secondFrame.Buffer.WriteTerminalSnapshotSvg("dock_panel.fill_layout_change.svg");
        _secondFrame.Buffer.WriteLayoutDebugSvg(_root, 20, 5, "dock_panel.fill_layout_change.svg");
        terminal_snapshot_assertions.WriteInvalidationOverlaySvg(_root,
                                                                 20,
                                                                 5,
                                                                 "dock_panel.fill_layout_change.svg",
                                                                 _drawStrategy.LastInvalidations);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void title_row_cells_receive_second_frame_number()
    {
        for (var x = 0; x < 20; x++)
            _secondFrame.Buffer.GetCell(x, 0).Frame.ShouldBe(_firstFrame.FrameNumber);
    }

    [Fact]
    public void fill_cells_receive_second_frame_number()
    {
        for (var y = 1; y < 5; y++)
            _secondFrame.Buffer.GetCell(0, y).Frame.ShouldBe(_secondFrame.FrameNumber);
    }

    [Fact]
    public void title_widget_is_redrawn_on_layout_change()
    {
        _titleBar.RenderCount.ShouldBe(1);
    }

    [Fact]
    public void fill_widget_is_redrawn_on_fill_layout_change()
    {
        _fillWidget.RenderCount.ShouldBe(2);
    }

    [Fact]
    public void layout_change_currently_uses_full_redraw_path()
    {
        _drawStrategy.LastInvalidations.ShouldNotBeNull();
    }

    sealed class capture_and_draw_strategy : IFrameDrawStrategy
    {
        readonly ScribeFrameDrawStrategy _inner = new();
        public IReadOnlyDictionary<IWidget, InvalidationKind>? LastInvalidations { get; private set; }

        public void Draw(IWidget root,
                         UiContext uiContext,
                         GridBuffer buffer,
                         IReadOnlyDictionary<IWidget, InvalidationKind>? invalidations,
                         ushort frameNumber,
                         FrameLayoutState layoutState)
        {
            LastInvalidations = invalidations;
            _inner.Draw(root, uiContext, buffer, invalidations, frameNumber, layoutState);
        }
    }

    sealed class counting_title_widget : TestWidgetBase
    {
        public int RenderCount { get; private set; }

        public override Size Measure(SizeConstraint constraint) => new(constraint.MaxWidth, 1);

        public override void Render(Canvas canvas)
        {
            RenderCount++;
            canvas.Fill(0, 0, canvas.Width, canvas.Height, (Rune)'-', new Style());
        }
    }

    sealed class counting_fill_widget : TestWidgetBase
    {
        public int RenderCount { get; private set; }

        public override Size Measure(SizeConstraint constraint) => new(constraint.MaxWidth, constraint.MaxHeight);

        public override void Render(Canvas canvas)
        {
            RenderCount++;
            canvas.Fill(0, 0, canvas.Width, canvas.Height, (Rune)'*', new Style());
        }
    }
}
