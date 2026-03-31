namespace Thoth;

public interface IThothConsoleSession : IAsyncDisposable
{
    void Publish<T>(T message);
    Task WaitForExitAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
