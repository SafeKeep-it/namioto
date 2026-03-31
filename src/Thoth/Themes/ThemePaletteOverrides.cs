using Thoth.Rendering;

namespace Thoth.Themes;

public record ThemePaletteOverrides(Color? Background = null,
                                    Color? Foreground = null,
                                    Color? MutedText = null,
                                    Color? Separator = null,
                                    Color? Accent = null,
                                    Color? Notification = null,
                                    Color? Success = null,
                                    Color? Warning = null,
                                    Color? Error = null,
                                    Color? FocusOutline = null,
                                    Color? PanelBackground = null);
