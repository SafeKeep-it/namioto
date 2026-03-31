using System.Text;

namespace Thoth.Terminal;

public sealed class VisualTerminal : ITerminal
{
    readonly List<byte> _output = [];

    public VisualTerminal(int windowWidth, int windowHeight)
    {
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
    }

    public TerminalKind Kind => TerminalKind.Visual;
    public int WindowWidth { get; }
    public int WindowHeight { get; }
    public string Clipboard { get; private set; } = string.Empty;

    public int ReadRawInput(Span<byte> buffer) => 0;

    public void WriteRaw(ReadOnlySpan<byte> buffer)
    {
        for (var i = 0; i < buffer.Length; i++)
            _output.Add(buffer[i]);
    }

    public void SetClipboard(string text)
    {
        Clipboard = text;
    }

    public string GetOutputText() => Encoding.UTF8.GetString(_output.ToArray());
}
