using Shouldly;
using Thoth.Widgets;
using comptatata.tests.app.cli.thoth.rendering.frame_engine.utilities;

namespace comptatata.tests.app.cli.thoth.rendering.frame_engine;

public class frame_number_rollover_forces_full_frame : IAsyncLifetime
{
    readonly CapturingFrameDrawStrategy _drawStrategy = new();
    readonly FrameRenderer _frameEngine;
    readonly Screen _root = new();
    readonly UiContext _uiContext;

    (ScreenBuffer Buffer, ushort FrameNumber, bool RequiresFullFrame) _rolloverFrame;

    public frame_number_rollover_forces_full_frame()
    {
        _frameEngine = new(fullRender: false, drawStrategy: _drawStrategy);
        _uiContext = new(_root);
    }

    public Task InitializeAsync()
    {
        _frameEngine.RenderFrame(_root,
                                 _uiContext,
                                 20,
                                 4,
                                 new Dictionary<IWidget, InvalidationKind>());

        for (var i = 0; i < ushort.MaxValue - 1; i++)
        {
            _frameEngine.RenderFrame(_root,
                                     _uiContext,
                                     20,
                                     4,
                                     new Dictionary<IWidget, InvalidationKind>());
        }

        _rolloverFrame = _frameEngine.RenderFrame(_root,
                                                  _uiContext,
                                                  20,
                                                  4,
                                                  new Dictionary<IWidget, InvalidationKind>());
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void frame_number_wraps_to_one_instead_of_zero()
    {
        _rolloverFrame.FrameNumber.ShouldBe((ushort)1);
        _drawStrategy.LastFrameNumber.ShouldBe((ushort)1);
    }

    [Fact]
    public void rollover_frame_requires_full_frame_render()
    {
        _rolloverFrame.RequiresFullFrame.ShouldBeTrue();
    }
}
