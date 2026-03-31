namespace Thoth.Eventing;

public readonly struct PasteInput(string text)
{
    public string Text { get; } = text;
}