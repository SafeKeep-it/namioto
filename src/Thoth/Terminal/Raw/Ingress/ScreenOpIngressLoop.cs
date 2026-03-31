using System.Threading.Channels;

namespace Thoth.Terminal.Raw.Ingress;

public sealed class ScreenOpIngressLoop
{
    readonly Channel<ScreenOp> _ops = Channel.CreateUnbounded<ScreenOp>();
    readonly ScreenOpInputLoop _inputLoop;

    public ScreenOpIngressLoop(ITerminal terminal)
    {
        _inputLoop = new(terminal, Post);
    }

    public ChannelReader<ScreenOp> Reader => _ops.Reader;

    public void Start(CancellationToken ct) => _inputLoop.Start(ct);

    public void Post(ScreenOp op) => _ops.Writer.TryWrite(op);

    public void PostCommand(object command)
    {
        _ops.Writer.TryWrite(new(ScreenOpTarget.Application,
                                 ScreenOpKind.StateChange,
                                 ScreenOpCoalesce.None,
                                 0,
                                 0,
                                 null,
                                 command));
    }
}
