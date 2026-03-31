using System.Buffers;

namespace Thoth.Terminal.Raw.Ingress;

internal sealed class InputBuffer
{
    const int PooledChunkSize = 256;
    readonly Queue<(byte[] data, int length, long timestamp)> _chunks = new();
    readonly object _lock = new();

    public void Write(ReadOnlySpan<byte> data, long timestamp)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(data.Length, PooledChunkSize));
        data.CopyTo(buffer);
        lock (_lock)
        {
            _chunks.Enqueue((buffer, data.Length, timestamp));
        }
    }

    public (byte[] data, int length, long timestamp)? Read()
    {
        lock (_lock)
        {
            if (_chunks.Count == 0) return null;

            return _chunks.Dequeue();
        }
    }

    public void Return(byte[] buffer)
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}