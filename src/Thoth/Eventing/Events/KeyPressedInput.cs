namespace Thoth.Eventing.Events;

public readonly struct KeyPressedInput(ConsoleKeyInfo key)
{
    public ConsoleKeyInfo Key { get; } = key;
}