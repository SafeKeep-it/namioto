using System.Threading;
using Thoth.Widgets;

namespace Thoth;

internal sealed class ThothConsoleSession : IThothConsoleSession
{
    readonly ThothConsoleBuilder _thothBuilder;
    readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ThothConsoleSession(string? title, IWidget root, ThothConsoleBuilder thothBuilder)
    {
        _thothBuilder = thothBuilder;
    }

    public void Publish<T>(T message)
    {
        HandlerRepository<T>.Publish(_thothBuilder, message);
    }

    public Task WaitForExitAsync(CancellationToken ct = default)
    {
        if (!ct.CanBeCanceled)
            return _stopped.Task;

        return _stopped.Task.WaitAsync(ct);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _stopped.TrySetResult();
        return WaitForExitAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
