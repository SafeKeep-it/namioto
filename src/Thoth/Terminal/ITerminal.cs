namespace Thoth.Terminal;

public interface ITerminal
{
    TerminalKind Kind { get; }
    int WindowWidth { get; }
    int WindowHeight { get; }
    int ReadRawInput(Span<byte> buffer);
    void WriteRaw(ReadOnlySpan<byte> buffer);
    void SetClipboard(string text);
}
