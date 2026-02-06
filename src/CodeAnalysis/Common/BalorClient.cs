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

    static void Enqueue(string notification)
    {
        // Non-blocking write to channel (lock-free)
        _channel.Writer.TryWrite(notification);
    }

    static void EnqueueTouch(string symbolId, string projectName, long timestamp)
    {
        // Format: TOUCH|symbolId|timestamp|projectName
        var notification = $"TOUCH|{symbolId}|{timestamp}|{projectName}\n";
        Enqueue(notification);
    }

    /// <summary>
    /// Sends a lightweight touch notification to the daemon (fire-and-forget).
    /// Format: TOUCH|symbolId|timestamp|projectName
    /// </summary>
    public static void Touch(string symbolId, string projectName)
    {
        EnqueueTouch(symbolId, projectName, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// Enqueues a touch notification using the provided timestamp.
    /// Use this when another artifact (e.g., a shape file) is named with the same timestamp.
    /// </summary>
    public static void Touch(string symbolId, string projectName, long timestamp)
    {
        EnqueueTouch(symbolId, projectName, timestamp);
    }

    /// <summary>
    /// Attempts to send a touch notification, returning true if daemon is available.
    /// Waits briefly to confirm connection before returning.
    /// </summary>
    public static bool TryTouch(string symbolId, string projectName)
    {
        try
        {
            using var testClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            testClient.Connect(ConnectTimeoutMs);
            if (!testClient.IsConnected) return false;

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var notification = $"TOUCH|{symbolId}|{timestamp}|{projectName}\n";

            var bytes = Encoding.UTF8.GetBytes(notification);
            testClient.Write(bytes, 0, bytes.Length);
            testClient.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Attempts to send a touch notification with an explicit timestamp.
    /// The timestamp MUST match the emitted shape file timestamp.
    /// </summary>
    public static bool TryTouch(string symbolId, string projectName, long timestamp)
    {
        try
        {
            using var testClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            testClient.Connect(ConnectTimeoutMs);
            if (!testClient.IsConnected) return false;

            var notification = $"TOUCH|{symbolId}|{timestamp}|{projectName}\n";

            var bytes = Encoding.UTF8.GetBytes(notification);
            testClient.Write(bytes, 0, bytes.Length);
            testClient.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Signals flush request (consumer will flush pipe after draining).
    /// </summary>
    public static void Flush()
    {
        Enqueue("__FLUSH__");
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

                // Ensure connection; if connect fails, re-enqueue and retry later.
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
                        client?.Dispose();
                        client = null;
                        continue;
                    }
                }

                // Write message; if it fails, re-enqueue and retry later.
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    client.Write(bytes, 0, bytes.Length);
                }
                catch
                {
                    client?.Dispose();
                    client = null;
                }
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