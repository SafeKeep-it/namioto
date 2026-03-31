using Thoth.Terminal.Bootstrap;

namespace Thoth.Terminal;

internal sealed record TerminalSessionOptions(Action<ConsoleCancelEventArgs> OnCancel,
                                              string? Title,
                                              HostEnvironmentInfo Environment,
                                              TerminalCapabilities Capabilities);
