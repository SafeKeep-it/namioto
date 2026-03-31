namespace Thoth.Rendering.Text;

public interface IWidthProvider
{
    byte GetWidth(ReadOnlySpan<char> cluster);
}