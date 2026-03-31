namespace Thoth.Rendering.Text;

public static class WordEnumeratorExtensions
{
    extension(ReadOnlySpan<char> text)
    {
        public WordEnumerator EnumerateWords() => new(text);
    }
}
