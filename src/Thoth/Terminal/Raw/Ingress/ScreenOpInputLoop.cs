using Thoth.Terminal;

namespace Thoth.Terminal.Raw.Ingress;

public sealed class ScreenOpInputLoop
{
    readonly InputReader _inputReader;

    public ScreenOpInputLoop(ITerminal terminal, Action<ScreenOp> post)
    {
        _inputReader = new(terminal, post);
    }

    public void Start(CancellationToken ct)
    {
        var readerThread = new Thread(() => _inputReader.RunReader(ct))
        {
            IsBackground = true,
            Name = "Thoth.RawInput.Reader"
        };

        var parserThread = new Thread(() => _inputReader.RunParser(ct))
        {
            IsBackground = true,
            Name = "Thoth.RawInput.Parser"
        };

        readerThread.Start();
        parserThread.Start();
    }
}
