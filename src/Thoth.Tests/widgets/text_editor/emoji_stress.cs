using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class emoji_stress : IAsyncLifetime
{
    const int Width = 100;
    const int Height = 20;
    ScreenBuffer _buffer = null!;
    TextEditor _editor = null!;
    DockPanel _root = null!;
    UiContext _uiContext = null!;

    public Task InitializeAsync()
    {
        _editor = new() { Style = new(Color.White, Color.Black) };
        _root = new();

        var titleBar = new TextBar
                       {
                           CenterTitle = "Stress Test", Style = new(Color.White, Color.Gray)
                       };
        _root.Add(new Dock { Position = DockPosition.Top, Content = titleBar });

        var editorBorder = new Border { Content = _editor, Style = new(Color.Gray, Color.Black) };
        _root.Add(new Dock { Position = DockPosition.Bottom, Content = editorBorder });

        _uiContext = new(_root);
        _uiContext.KeyboardFocus = _editor;
        _buffer = new(Width, Height);

        // Act: Input 100 emojis (Rocket: 🚀 \U0001F680)
        // Each rocket is 2 chars in UTF-16 and 2 cells wide.
        var emoji = "🚀";
        var dispatcher = new EventDispatcher();
        for (var i = 0; i < 100; i++)
        {
            foreach (var ch in emoji)
            {
                var keyInfo = new ConsoleKeyInfo(ch, ConsoleKey.NoName, false, false, false);
                dispatcher.Dispatch(_editor, new KeyPressedInput(keyInfo));
            }
        }

        // Render
        tree_render_harness.Render(_root, _buffer, _uiContext);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_emojis_correctly_in_editor()
    {
        // Border height is content height (3) + 2 = 5.
        // DockPanel Bottom dock for Border:
        // Height is 20. TitleBar Top takes 1. remaining is 19.
        // Border gets min(5, 19) = 5.
        // Border is at y = 1 + 19 - 5 = 15.
        // Editor is at y = 15 + 1 = 16.

        const int editorY = 16;
        const int editorXStart = 1;
        const int editorWidth = Width - 2;
        _ = editorWidth;
        // We input 100 emojis, but the editor currently only supports single line (Measure returns 1).
        // Each emoji is 2 cells wide. 98 / 2 = 49 emojis should fit.

        for (var i = 0; i < 49; i++)
        {
            var x = editorXStart + i * 2;
            var cell0 = _buffer.GetCell(x, editorY);
            var cell1 = _buffer.GetCell(x + 1, editorY);

            cell0.GlyphId.ShouldBe(128640, $"Emoji {i} at x={x} should be rocket");
            cell0.Width.ShouldBe((byte)2);
            cell1.Width.ShouldBe((byte)0);
        }

        // The 50th emoji would start at x = 1 + 49*2 = 99.
        // Editor width is 98, so editor ends at x = 1 + 98 = 99.
        // 50th emoji needs 2 cells (99 and 100).
        // Canvas.SetCell(x, y, ...) where x + width > bounds.Width should return.
        // editorXStart + 49*2 = 99. bounds.Width for editor is 98.
        // x = 98 (relative to editor). 98 + 2 > 98. So it shouldn't render.

        var cellOverflow = _buffer.GetCell(editorXStart + 49 * 2, editorY);
        cellOverflow.GlyphId.ShouldNotBe(128640);
    }
}
