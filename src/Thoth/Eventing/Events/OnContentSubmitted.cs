namespace Thoth.Eventing.Events;

public readonly struct OnContentSubmitted(string content)
{
    public string Content { get; } = content;
}
