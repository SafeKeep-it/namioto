namespace Thoth.Terminal;

public sealed class SshTerminal : ITerminal
{
    readonly Stream _stdin;
    readonly Stream _stdout;
    readonly Func<int> _windowWidth;
    readonly Func<int> _windowHeight;
    readonly Action<string>? _setClipboard;

    public SshTerminal(
        Stream stdin,
        Stream stdout,
        Func<int> windowWidth,
        Func<int> windowHeight,
        Action<string>? setClipboard = null)
    {
        _stdin = stdin;
        _stdout = stdout;
        _windowWidth = windowWidth;
        _windowHeight = windowHeight;
        _setClipboard = setClipboard;
    }

    public TerminalKind Kind => TerminalKind.Ssh;
    public int WindowWidth => _windowWidth();
    public int WindowHeight => _windowHeight();

    public int ReadRawInput(Span<byte> buffer) => _stdin.Read(buffer);

    public void WriteRaw(ReadOnlySpan<byte> buffer)
    {
        _stdout.Write(buffer);
        _stdout.Flush();
    }

    public void SetClipboard(string text)
    {
        if (_setClipboard is null) return;
        _setClipboard(text);
    }

}
