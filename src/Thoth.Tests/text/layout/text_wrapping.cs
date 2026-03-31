using System.Text;
using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_wrapping;

public class text_wrapping : IAsyncLifetime
{
    const int Width = 10;
    const int Height = 5;
    ScreenBuffer _buffer = null!;
    RenderContext _context = null!;

    public Task InitializeAsync()
    {
        _buffer = new(Width, Height);
        _context = new(new(new Screen()));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void wraps_at_word_boundary()
    {
        var canvas = new Canvas(_buffer, new(0, 0, Width, Height), _context);
        var text = "Hello world"; // "Hello " is 6, "world" is 5. Width is 10.
        // Line 0: "Hello "
        // Line 1: "world"
        canvas.DrawString(0, 0, text, new());

        var line0 = GetLine(0);
        var line1 = GetLine(1);

        line0.TrimEnd().ShouldBe("Hello");
        line1.TrimEnd().ShouldBe("world");
        _buffer.WriteTerminalSnapshotSvg("text_wrapping.word_boundary.svg");
    }

    [Fact]
    public void splits_long_words()
    {
        var canvas = new Canvas(_buffer, new(0, 0, 5, Height), _context);
        var text = "Supercalifragilistic";
        canvas.DrawString(0, 0, text, new());

        GetLine(0).TrimEnd().ShouldBe("Super");
        GetLine(1).TrimEnd().ShouldBe("calif");
        GetLine(2).TrimEnd().ShouldBe("ragil");
        GetLine(3).TrimEnd().ShouldBe("istic");
        _buffer.WriteTerminalSnapshotSvg("text_wrapping.long_word_split.svg");
    }

    [Fact]
    public void handles_empty_string()
    {
        var canvas = new Canvas(_buffer, new(0, 0, Width, Height), _context);
        canvas.DrawString(0, 0, "", new());
        GetLine(0).TrimEnd().ShouldBe("");
        _buffer.WriteTerminalSnapshotSvg("text_wrapping.empty_string.svg");
    }

    [Fact]
    public void does_not_split_emoji_at_line_end()
    {
        var canvas = new Canvas(_buffer, new(0, 0, 3, Height), _context);
        var text = "A 🚀"; // 'A' (1) + ' ' (1) + '🚀' (2) = 4 chars. Width 3.
        // Line 0: "A  " (The space at index 1 is fine, but the emoji at 2 would take 2 cells, total 4 > 3)
        // So line 0 should be "A  " and line 1 should be "🚀 "
        canvas.DrawString(0, 0, text, new());

        GetLine(0).TrimEnd().ShouldBe("A");
        GetLine(1).TrimEnd().ShouldBe("🚀");
        _buffer.WriteTerminalSnapshotSvg("text_wrapping.emoji_line_end.svg");
    }

    string GetLine(int y)
    {
        var sb = new StringBuilder();
        for (var x = 0; x < Width; x++)
        {
            var cell = _buffer.GetCell(x, y);
            if (cell.Width == 0 && cell.GlyphId == 0) continue; // Skip continuation cells

            if (cell.GlyphId > 0)
            {
                sb.Append(char.ConvertFromUtf32(cell.GlyphId));
            }
            else if (cell.GlyphId < 0)
            {
                var cluster = _context.Glyphs.Get(~cell.GlyphId);
                sb.Append(cluster);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }
}