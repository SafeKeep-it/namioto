namespace Thoth.Rendering;

public static class RenderPhaseGuard
{
    [ThreadStatic]
    static int _depth;

    public static bool IsActive => _depth > 0;

    public static IDisposable Enter()
    {
        _depth++;
        return new Scope();
    }

    public static void ThrowIfActive(string operation)
    {
        if (!IsActive) return;

        throw new InvalidOperationException(
            $"{operation} is not allowed during render phase.");
    }

    sealed class Scope : IDisposable
    {
        public void Dispose()
        {
            if (_depth > 0) _depth--;
        }
    }
}
