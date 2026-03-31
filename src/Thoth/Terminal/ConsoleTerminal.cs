namespace Thoth.Terminal;

public sealed class ConsoleTerminal : ITerminal, IDisposable
{
    readonly SystemTerminal _inner = new();

    public TerminalKind Kind => TerminalKind.Console;
    public int WindowWidth => _inner.WindowWidth;
    public int WindowHeight => _inner.WindowHeight;

    public int ReadRawInput(Span<byte> buffer) => _inner.ReadRawInput(buffer);
    public void WriteRaw(ReadOnlySpan<byte> buffer) => _inner.WriteRaw(buffer);
    public void SetClipboard(string text) => _inner.SetClipboard(text);

    public void Dispose() => _inner.Dispose();
}
