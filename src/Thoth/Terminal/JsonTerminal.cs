using System.Text;

namespace Thoth.Terminal;

public sealed class JsonTerminal : ITerminal
{
    readonly MemoryStream _input = new();
    readonly MemoryStream _output = new();

    public JsonTerminal(int windowWidth = 80, int windowHeight = 24)
    {
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
    }

    public TerminalKind Kind => TerminalKind.Json;
    public int WindowWidth { get; }
    public int WindowHeight { get; }
    public string Clipboard { get; private set; } = string.Empty;

    public int ReadRawInput(Span<byte> buffer) => _input.Read(buffer);

    public void WriteRaw(ReadOnlySpan<byte> buffer)
    {
        _output.Write(buffer);
    }

    public void SetClipboard(string text)
    {
        Clipboard = text;
    }

    public string GetOutputText()
    {
        return Encoding.UTF8.GetString(_output.GetBuffer(), 0, (int)_output.Length);
    }

    public void QueueInput(ReadOnlySpan<byte> input)
    {
        var readIndex = _input.Position;
        _input.Position = _input.Length;
        _input.Write(input);
        _input.Position = readIndex;
    }
}
