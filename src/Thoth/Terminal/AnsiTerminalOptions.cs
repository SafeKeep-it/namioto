namespace Thoth.Terminal;

public record AnsiTerminalOptions(Action<ConsoleCancelEventArgs> CancelKeyPress,
                                  string? Title = null);
