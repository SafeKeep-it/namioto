using Microsoft.Win32.SafeHandles;
using Thoth.Terminal.Raw;

namespace Thoth.Terminal;

public sealed class SystemTerminal : ITerminal, IDisposable
{
    readonly Stream _stdin;
    readonly Stream _stdout;
    readonly SafeFileHandle _ttyHandle;

    public SystemTerminal()
    {
        var fd = RawMode.TtyFd;
        if (fd < 0)
        {
            _stdin = Console.OpenStandardInput();
            _stdout = Console.OpenStandardOutput();
            _ttyHandle = new(-1, false);
        }
        else
        {
            _ttyHandle = new(fd, false);
            _stdin = new FileStream(_ttyHandle, FileAccess.Read);
            _stdout = new FileStream(_ttyHandle, FileAccess.Write);
        }
    }

    public void Dispose()
    {
        _stdin.Dispose();
        _stdout.Dispose();
        _ttyHandle.Dispose();
    }

    public TerminalKind Kind => TerminalKind.Console;

    public int ReadRawInput(Span<byte> buffer) => _stdin.Read(buffer);

    public void WriteRaw(ReadOnlySpan<byte> buffer)
    {
        _stdout.Write(buffer);
        _stdout.Flush();
    }

    public int WindowWidth => Console.WindowWidth;
    public int WindowHeight => Console.WindowHeight;

    public void SetClipboard(string text)
    {
        TerminalProtocolSequences.Osc.WriteClipboardCommand(_stdout, text);
        _stdout.Flush();
    }
}
