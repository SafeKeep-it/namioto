using System.Buffers;

namespace Thoth.Rendering;

/// <summary>
/// A ref struct that wraps a stackalloc/ArrayPool allocation pattern.
/// Uses the provided stack memory if sufficient; otherwise rents from ArrayPool.
/// Must be disposed to return any rented array.
/// </summary>
public ref struct StackBuffer<T> : IDisposable where T : unmanaged
{
    T[]? _rentedArray;
    readonly Span<T> _span;

    public StackBuffer(Span<T> stackMemory, int requiredLength)
    {
        if (stackMemory.Length >= requiredLength)
        {
            _span = stackMemory[..requiredLength];
            _rentedArray = null;
        }
        else
        {
            _rentedArray = ArrayPool<T>.Shared.Rent(requiredLength);
            _span = _rentedArray.AsSpan(0, requiredLength);
        }
    }

    public Span<T> Span => _span;

    public static StackBuffer<T> Create(Span<T> stackMemory, int length) => new(stackMemory, length);

    public void Dispose()
    {
        if (_rentedArray is not null)
        {
            ArrayPool<T>.Shared.Return(_rentedArray);
            _rentedArray = null;
        }
    }
}
