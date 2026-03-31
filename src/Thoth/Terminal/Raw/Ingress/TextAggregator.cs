using System.Diagnostics;
using System.Text;

namespace Thoth.Terminal.Raw.Ingress;

/// <summary>
///     Coalesces text chunks into batched ScreenOp events.
///     Follows the same time-window pattern as scroll coalescing.
///     Thread-safe for single producer.
/// </summary>
public sealed class TextAggregator
{
    static readonly long DefaultWindowTicks = Stopwatch.Frequency *
        (long.TryParse(Environment.GetEnvironmentVariable("THOTH_TEXT_WINDOW_MS"), out var ms)
            ? ms
            : 16) / 1000;
    readonly ScreenOpKind _kind;

    readonly Action<ScreenOp> _post;
    readonly ScreenOpTarget _target;
    readonly long _windowTicks;
    bool _active;

    StringBuilder? _buffer;
    long _lastTicks;
    int _targetId;

    public TextAggregator(Action<ScreenOp> post,
                          ScreenOpTarget target = ScreenOpTarget.Editor,
                          ScreenOpKind kind = ScreenOpKind.Append,
                          long? windowTicks = null)
    {
        _post = post;
        _target = target;
        _kind = kind;
        _windowTicks = windowTicks ?? DefaultWindowTicks;
    }

    /// <summary>
    ///     Whether there is pending text that hasn't been flushed.
    /// </summary>
    public bool HasPending => _active && _buffer is { Length: > 0 };

    /// <summary>
    ///     Appends text to the aggregation buffer.
    ///     If targetId changes or time window expires, flushes pending text first.
    /// </summary>
    public void Append(int targetId, ReadOnlySpan<char> text)
    {
        var now = Stopwatch.GetTimestamp();

        if (_active && (_targetId != targetId || now - _lastTicks > _windowTicks)) Flush();

        if (!_active)
        {
            _buffer ??= new(256);
            _active = true;
            _targetId = targetId;
        }

        _buffer!.Append(text);
        _lastTicks = now;
    }

    /// <summary>
    ///     Appends a string to the aggregation buffer.
    /// </summary>
    public void Append(int targetId, string text) => Append(targetId, text.AsSpan());

    /// <summary>
    ///     Forces a flush of any pending text, regardless of time window.
    /// </summary>
    public void Flush()
    {
        if (!_active || _buffer is not { Length: > 0 }) return;

        _post(new(_target,
                  _kind,
                  ScreenOpCoalesce.AppendText,
                  _targetId,
                  0,
                  _buffer.ToString()));

        _buffer.Clear(); // Keeps capacity for reuse
        _active = false;
    }

    /// <summary>
    ///     Attempts to flush if the time window has expired.
    ///     Call this periodically (e.g., in a tick loop).
    /// </summary>
    /// <returns>True if text was flushed.</returns>
    public bool TryFlush()
    {
        if (!_active) return false;

        var now = Stopwatch.GetTimestamp();
        if (now - _lastTicks <= _windowTicks) return false;

        Flush();
        return true;
    }
}
