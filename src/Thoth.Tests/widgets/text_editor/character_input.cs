using System.Text;
using Shouldly;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_editor_input;

public class character_input : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    Canvas? _canvas;
    RenderContext _context = null!;
    TextEditor _editor = null!;

    public Task InitializeAsync()
    {
        _editor = new();
        _buffer = new(10, 1);
        _context = new(new(new Screen()));
        _canvas = new Canvas(_buffer, new(0, 0, 10, 1), _context);

        // Act: Dispatch a key press
        var keyInfo = new ConsoleKeyInfo('A', ConsoleKey.A, false, false, false);
        var dispatcher = new EventDispatcher();
        dispatcher.Dispatch(_editor, new KeyPressedInput(keyInfo));

        // Render after input
        if (_canvas is { } c) _editor.GetScribe().Draw(c);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void renders_typed_character()
    {
        var sb = new StringBuilder();
        for (var x = 0; x < 10; x++)
        {
            var cell = _buffer.GetCell(x, 0);
            if (cell.GlyphId != 0)
                sb.Append((char)cell.GlyphId);
            else
                sb.Append(' ');
        }

        // Expected: 'A' at index 0, space (caret) at index 1, spaces for the rest
        sb.ToString().ShouldBe("A         ");
    }
}
