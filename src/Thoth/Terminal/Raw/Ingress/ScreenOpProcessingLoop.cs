using System.Diagnostics;
using System.Threading.Channels;
using Thoth.Diagnostics;
using Thoth.Eventing;
using Thoth.Terminal;

namespace Thoth.Terminal.Raw.Ingress;

internal sealed class ScreenOpProcessingLoop
{
    public static readonly long Freq = Stopwatch.Frequency;

    public static readonly long Ticks_1fps = Freq;
    public static readonly long Ticks_15fps = Freq / 15;
    public static readonly long Ticks_30fps = Freq / 30;
    public static readonly long Ticks_60fps = Freq / 60;

    public static readonly long DrainBudget_2ms = Freq * 2 / 1000;
    public static readonly long DrainBudget_3ms = Freq * 3 / 1000;
    public static readonly long DrainBudget_4ms = Freq * 4 / 1000;

    readonly ChannelReader<ScreenOp> _ops;
    readonly ScreenOpApply _opApply;
    readonly ScreenOpBatchCoalescer _coalescer = new();
    readonly ITerminal _terminal;
    readonly AttentionManager _attention;

    internal ScreenOpProcessingLoop(AttentionManager attention, ITerminal terminal, ChannelReader<ScreenOp> ops)
    {
        _attention = attention;
        _terminal = terminal;
        _ops = ops;
        _opApply = new(attention);
    }

    public void RenderInitialFrame() => _attention.Render();

    public async Task RunAsync(CancellationToken ct)
    {
        var lastW = _terminal.WindowWidth;
        var lastH = _terminal.WindowHeight;

        var opsBuffer = new List<ScreenOp>(256);
        var commandBuffer = new List<object>(64);

        while (!ct.IsCancellationRequested)
        {
            var drainStartedAt = RuntimeTiming.IsEnabled ? RuntimeTiming.CaptureTimestamp() : 0;
            var w = _terminal.WindowWidth;
            var h = _terminal.WindowHeight;
            var start = Stopwatch.GetTimestamp();
            var deadline = start + DrainBudget_3ms;

            opsBuffer.Clear();
            commandBuffer.Clear();

            for (var i = 0; _ops.TryRead(out var op); i++)
            {
                if (op.Message is { } message)
                {
                    _coalescer.Flush(opsBuffer);
                    commandBuffer.Add(message);
                    continue;
                }

                if (op.Coalescence == ScreenOpCoalesce.None)
                {
                    _coalescer.Flush(opsBuffer);
                    opsBuffer.Add(op);
                    continue;
                }

                if (_coalescer.TryMerge(opsBuffer, op)) continue;

                _coalescer.Flush(opsBuffer);
                opsBuffer.Add(op);

                if (i % 32 != 0) continue;
                var now = Stopwatch.GetTimestamp();
                if (now >= deadline) break;
            }

            _coalescer.Flush(opsBuffer);
            if (RuntimeTiming.IsEnabled)
                RuntimeTiming.RecordSince("ingress.drain", drainStartedAt);

            var applyStartedAt = RuntimeTiming.IsEnabled ? RuntimeTiming.CaptureTimestamp() : 0;
            foreach (var op in opsBuffer) _opApply.Apply(op, w, h, ct);

            foreach (var command in commandBuffer) _attention.SendCommand(command);

            var sizeChanged = w != lastW || h != lastH;
            var animationChanged = _attention.TickAnimations(Stopwatch.GetTimestamp());
            if (RuntimeTiming.IsEnabled)
                RuntimeTiming.RecordSince("ingress.apply_dispatch", applyStartedAt);

            if (opsBuffer.Count > 0 || commandBuffer.Count > 0 || sizeChanged || animationChanged)
            {
                lastW = w;
                lastH = h;

                var renderStartedAt = RuntimeTiming.IsEnabled ? RuntimeTiming.CaptureTimestamp() : 0;
                _attention.Render();
                if (RuntimeTiming.IsEnabled)
                    RuntimeTiming.RecordSince("render.frame", renderStartedAt);

                RuntimeTiming.EmitIfDue();
                await Task.Yield();
                continue;
            }

            await Task.Delay(16, ct);
        }
    }
}
