namespace Thoth.Terminal.Bootstrap;

public sealed record TerminalCapabilities(string Profile,
                                          int MaxColors,
                                          bool SupportsTrueColor,
                                          bool SupportsAlternateScreen,
                                          bool SupportsMouse,
                                          bool SupportsBracketedPaste,
                                          bool SupportsClipboardOsc52,
                                          bool EnableRawMode,
                                          bool EnableAnsiOptions,
                                          string WidthProfile);
