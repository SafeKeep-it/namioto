using System.Threading;

namespace Thoth.Eventing;

internal static class IdGenerator
{
    static int _counter;
    public static int Next() => Interlocked.Increment(ref _counter);
}
