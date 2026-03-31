using System.Diagnostics;

namespace Thoth.Diagnostics;

internal static class RuntimeTiming
{
    static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("THOTH_TIMING")?.Trim(),
                      "1",
                      StringComparison.OrdinalIgnoreCase);

    static readonly object Sync = new();
    static readonly Dictionary<string, TimingWindow> Windows = new(StringComparer.Ordinal);
    static readonly long EmitIntervalTicks =
        long.TryParse(Environment.GetEnvironmentVariable("THOTH_TIMING_EMIT_MS"), out var emitMs) && emitMs > 0
            ? Stopwatch.Frequency * emitMs / 1000
            : Stopwatch.Frequency * 5;
    static long _nextEmitTick = Stopwatch.GetTimestamp() + EmitIntervalTicks;

    public static bool IsEnabled => Enabled;

    public static long CaptureTimestamp() => Stopwatch.GetTimestamp();

    public static void RecordSince(string name, long startedAt)
    {
        if (!Enabled) return;
        RecordTicks(name, Stopwatch.GetTimestamp() - startedAt);
    }

    public static void RecordTicks(string name, long ticks)
    {
        if (!Enabled) return;

        lock (Sync)
        {
            if (!Windows.TryGetValue(name, out var window))
            {
                window = new();
                Windows.Add(name, window);
            }

            window.Add(ticks);
        }
    }

    public static void EmitIfDue()
    {
        if (!Enabled) return;

        var now = Stopwatch.GetTimestamp();
        if (now < _nextEmitTick) return;

        lock (Sync)
        {
            if (now < _nextEmitTick) return;
            _nextEmitTick = now + EmitIntervalTicks;

            foreach (var pair in Windows)
            {
                var stats = pair.Value.SnapshotStats();
                if (stats.Count == 0) continue;

                Console.WriteLine($"THOTH_TIMING {pair.Key} count={stats.Count} p50={stats.P50Ms:F4} p95={stats.P95Ms:F4} p99={stats.P99Ms:F4} tm99={stats.Tm99Ms:F4}");
            }

            Windows.Clear();
        }
    }

    sealed class TimingWindow
    {
        const int Capacity = 4096;
        readonly long[] _samples = new long[Capacity];
        int _writeIndex;
        int _count;

        public void Add(long ticks)
        {
            _samples[_writeIndex] = ticks;
            _writeIndex = (_writeIndex + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public TimingStats SnapshotStats()
        {
            if (_count == 0) return new(0, 0, 0, 0, 0);

            var copy = new long[_count];
            Array.Copy(_samples, copy, _count);
            Array.Sort(copy);

            var p50 = Percentile(copy, 50);
            var p95 = Percentile(copy, 95);
            var p99 = Percentile(copy, 99);
            var tm99 = TailMean(copy, 99);

            return new(_count,
                       ToMilliseconds(p50),
                       ToMilliseconds(p95),
                       ToMilliseconds(p99),
                       ToMilliseconds(tm99));
        }

        static long Percentile(long[] sorted, int percentile)
        {
            if (sorted.Length == 0) return 0;
            var index = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }

        static double TailMean(long[] sorted, int percentile)
        {
            if (sorted.Length == 0) return 0;

            var start = (int)Math.Ceiling((percentile / 100.0) * sorted.Length) - 1;
            start = Math.Clamp(start, 0, sorted.Length - 1);

            long total = 0;
            for (var i = start; i < sorted.Length; i++)
                total += sorted[i];

            return (double)total / Math.Max(1, sorted.Length - start);
        }

        static double ToMilliseconds(double ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }

    readonly record struct TimingStats(int Count, double P50Ms, double P95Ms, double P99Ms, double Tm99Ms);
}
