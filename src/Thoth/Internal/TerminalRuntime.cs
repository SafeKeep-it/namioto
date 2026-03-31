using System.Threading;
using System.Runtime.ExceptionServices;
using Thoth.Eventing;
using Thoth.Terminal;
using Thoth.Terminal.Raw.Ingress;

namespace Thoth.Internal;

internal sealed class TerminalRuntime : IThothConsoleSession
{
    readonly IDisposable _session;
    readonly ScreenOpProcessingLoop _opProcessingLoop;
    readonly ScreenOpIngressLoop _opIngressLoop;
    readonly Task _runTask;
    #pragma warning disable IDISP006
    readonly CancellationTokenSource _lifetime;
    #pragma warning restore IDISP006
    bool _started;
    int _stopRequested;

    internal TerminalRuntime(AttentionManager attention,
                             ITerminal terminal,
                             CancellationToken ct,
                             TerminalSessionOptions sessionOptions)
        : this(attention,
               terminal,
               ct,
               sessionOptions,
               options => new TerminalSession(options))
    {
    }

    internal TerminalRuntime(AttentionManager attention,
                             ITerminal terminal,
                             CancellationToken ct,
                             TerminalSessionOptions sessionOptions,
                             Func<TerminalSessionOptions, IDisposable> sessionFactory)
    {
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IDisposable? session = null;
        try
        {
            session = sessionFactory(sessionOptions);
            var opIngressLoop = new ScreenOpIngressLoop(terminal);
            var opProcessingLoop = new ScreenOpProcessingLoop(attention, terminal, opIngressLoop.Reader);

            _session = session;
            _opIngressLoop = opIngressLoop;
            _opProcessingLoop = opProcessingLoop;
            _runTask = RunUiAsync(_lifetime.Token);
        }
        catch (Exception ex)
        {
            try
            {
                session?.Dispose();
            }
            catch (Exception cleanupException)
            {
                // Cleanup failures are secondary when startup/run already failed.
                ex.Data["thoth.cleanup_exception"] = cleanupException;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
            throw;
        }
    }

    async Task RunUiAsync(CancellationToken ct)
    {
        try
        {
            StartUiWithInitialRenderBarrier(ct);
            await _opProcessingLoop.RunAsync(ct);
        }
        catch (Exception ex)
        {
            StopCore(ex);
            ExceptionDispatchInfo.Capture(ex).Throw();
            throw;
        }
    }

    void StopCore(Exception? primaryException = null)
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) != 0) return;
        _lifetime.Cancel();

        if (primaryException is null)
        {
            _session.Dispose();
            return;
        }

        try
        {
            _session.Dispose();
        }
        catch (Exception cleanupException)
        {
            // Cleanup failures are secondary when startup/run already failed.
            primaryException.Data["thoth.cleanup_exception"] = cleanupException;
        }
    }

    void StartUiWithInitialRenderBarrier(CancellationToken ct)
    {
        if (_started) return;

        RenderInitialFrameBeforeIngressStart();
        StartIngressLoop(ct);
        _started = true;
    }

    void RenderInitialFrameBeforeIngressStart()
    {
        _opProcessingLoop.RenderInitialFrame();
    }

    void StartIngressLoop(CancellationToken ct)
    {
        _opIngressLoop.Start(ct);
    }

    public async Task WaitForExitAsync(CancellationToken ct = default)
    {
        if (!ct.CanBeCanceled)
        {
            await _runTask;
            return;
        }

        await _runTask.WaitAsync(ct);
    }

    public void Publish<T>(T message) => _opIngressLoop.PostCommand(message!);

    public async Task StopAsync(CancellationToken ct = default)
    {
        StopCore();

        try
        {
            await WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested && !ct.IsCancellationRequested)
        {
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await StopAsync();
        _lifetime.Dispose();
    }
}
