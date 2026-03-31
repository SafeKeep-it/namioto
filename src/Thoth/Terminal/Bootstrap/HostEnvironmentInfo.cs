namespace Thoth.Terminal.Bootstrap;

public sealed record HostEnvironmentInfo(bool IsMacOs,
                                         bool IsLinux,
                                         bool IsWindows,
                                         bool IsInputRedirected,
                                         bool IsOutputRedirected,
                                         bool IsErrorRedirected,
                                         bool IsInteractive,
                                         bool IsInsideTmux,
                                         bool IsInsideScreen,
                                         bool WantsTrueColor,
                                         string? TermProgram,
                                         string? TermProgramVersion,
                                         string? Term,
                                         string? ColorTerm,
                                         TerminalHostKind TerminalHost);
