using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class emoji_rendering : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;
    TextEditor _editor = null!;

    public Task InitializeAsync()
    {
        _buffer = new(10, 2);
        _context = new(new(new Screen()));
        _editor = new TextEditor { Text = "A 🚀 Z" };
        _editor.GetRenderer().Arrange(new(0, 0, 10, 1));

        var canvas = new Canvas(_buffer, new(0, 0, 10, 2), _context);
        _editor.GetScribe().Draw(canvas);
        _buffer.WriteTerminalSnapshotSvg("emoji_rendering.rocket_cluster.svg");
        _buffer.WriteLayoutDebugSvg(_editor, 10, 2, "emoji_rendering.rocket_cluster.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void keeps_double_width_emoji_and_continuation_cell_consistent()
    {
        var rocketLead = _buffer.GetCell(2, 0);
        var rocketTail = _buffer.GetCell(3, 0);

        rocketLead.GlyphId.ShouldBe(128640);
        rocketLead.Width.ShouldBe((byte)2);
        rocketTail.Width.ShouldBe((byte)0);
    }
}
