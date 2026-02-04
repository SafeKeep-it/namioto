using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace Comptatata.CodeAnalysis.Common;

/// <summary>
/// Client for sending lightweight touch notifications to Balor.Daemon.
/// Uses a Channel for lock-free fire-and-forget writes from multiple threads.
/// </summary>
static class BalorClient
{
    const string PipeName = "BalorDaemon";
    const int ConnectTimeoutMs = 50;

    // Unbounded channel for lock-free producer writes
    static readonly Channel<string> _channel = Channel.CreateUnbounded<string>(
        new() { SingleReader = true, SingleWriter = false });

    static readonly Thread _consumerThread;
    static volatile bool _running = true;

    static BalorClient()
    {
        _consumerThread = new(ConsumerLoop) { IsBackground = true, Name = "BalorClient" };
        _consumerThread.Start();
    }

    /// <summary>
    /// Sends a lightweight touch notification to the daemon (fire-and-forget).
    /// Format: TOUCH|symbolId|timestamp|projectName
    /// </summary>
    public static void Touch(string symbolId, string projectName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var notification = $"TOUCH|{symbolId}|{timestamp}|{projectName}\n";

        // Non-blocking write to channel (lock-free)
        _channel.Writer.TryWrite(notification);
    }

    /// <summary>
    /// Signals flush request (consumer will flush pipe after draining).
    /// </summary>
    public static void Flush()
    {
        _channel.Writer.TryWrite("__FLUSH__");
    }

    static void ConsumerLoop()
    {
        NamedPipeClientStream? client = null;

        while (_running)
        {
            try
            {
                // Block until message available
                if (!_channel.Reader.TryRead(out var msg))
                {
                    // Use synchronous wait with timeout
                    var task = _channel.Reader.WaitToReadAsync().AsTask();
                    if (!task.Wait(1000)) continue;
                    if (!_channel.Reader.TryRead(out msg)) continue;
                }

                if (msg == "__FLUSH__")
                {
                    try
                    {
                        client?.Flush();
                    }
                    catch { }

                    continue;
                }

                // Ensure connection
                if (client == null || !client.IsConnected)
                {
                    client?.Dispose();
                    client = new(".", PipeName, PipeDirection.Out);
                    try
                    {
                        client.Connect(ConnectTimeoutMs);
                    }
                    catch
                    {
                        client = null;
                        continue;
                    }
                }

                // Write message
                var bytes = Encoding.UTF8.GetBytes(msg);
                client.Write(bytes, 0, bytes.Length);
            }
            catch
            {
                client?.Dispose();
                client = null;
            }
        }

        client?.Dispose();
    }
}