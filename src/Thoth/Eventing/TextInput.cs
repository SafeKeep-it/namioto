namespace Thoth.Eventing;

public readonly struct TextInput(string text)
{
    public string Text { get; } = text;
}