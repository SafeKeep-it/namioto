namespace Thoth.Eventing.Events;

public readonly struct CopyToClipboardRequested(string text)
{
    public string Text { get; } = text;
}
