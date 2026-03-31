using Shouldly;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class emoji_input : IAsyncLifetime
{
    const int Width = 10;
    ScreenBuffer _buffer = null!;
    TextEditor _editor = null!;

    public Task InitializeAsync()
    {
        _editor = new();
        // The requirement says "original width of the root - the 1 cell of the border".
        // If the border is around the editor, and we have 10 cells total, 
        // the inner width for the editor is 10 - 2 = 8.
        _buffer = new(Width, 3);

        // Setup a Border containing the editor
        var border = new Border { Content = _editor };

        // Send a 100 emoji. In UTF-16 it is a surrogate pair. 
        // We'll simulate 100 emojis input.
        // Actually the issue says "send 100 emoji textinput", which I interpret as the emoji for "100" 💯
        // or 100 emojis. Let's assume the 💯 emoji (U+1F4AF).
        var emoji = "💯";

        // InputReader handles strings by calling PostRune or similar.
        // We can simulate multiple key presses or just insert into text.
        // But the requirement says "send 100 emoji textinput", let's assume it means 100 instances of 💯.
        for (var i = 0; i < 100; i++) _editor.Text += emoji;

        tree_render_harness.Render(border, _buffer);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void verifies_emoji_rendering_and_wrapping()
    {
        // Inner width is 8 (10 - 2 for border).
        // 💯 is width 2.
        // 8 / 2 = 4 emojis per line.

        // Check first line of content (y=1 in buffer because border top is y=0)
        // x=0 is border, x=1 to 8 is content, x=9 is border.

        for (var y = 1; y < 2; y++)
        {
            for (var x = 1; x < 9; x += 2)
            {
                var cell1 = _buffer.GetCell(x, y);
                var cell2 = _buffer.GetCell(x + 1, y);

                // cell1 should have the emoji glyph (Width 2)
                cell1.GlyphId.ShouldBe(128175); // 💯
                cell1.Width.ShouldBe((byte)2);

                // cell2 should have Width 0
                cell2.Width.ShouldBe((byte)0);
            }
        }
    }
}
