namespace Thoth.Rendering.Text;

public static class GraphemeEnumeratorExtensions
{
    extension(ReadOnlySpan<char> text)
    {
        public GraphemeEnumerator EnumerateGraphemes() => new(text);
    }
}
