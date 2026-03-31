namespace Thoth.Rendering.Text;

public static class WidthProviderExtensions
{
    extension(IWidthProvider inner)
    {
        public IWidthProvider WithTerminalOverrides(string? termProgram)
        {
            var exceptions = TerminalQuirks.For(termProgram);
            return exceptions.Count > 0 ? new TerminalWidthOverrides(exceptions, inner) : inner;
        }

        public IWidthProvider WithOverrides(IReadOnlyDictionary<string, byte>? overrides)
        {
            if (overrides is not { Count: > 0 }) return inner;
            var dict = overrides as Dictionary<string, byte> ?? new Dictionary<string, byte>(overrides);
            return new TerminalWidthOverrides(dict, inner);
        }
    }
}
