using Thoth.Eventing;
using Thoth.Internal;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Thoth.Terminal.Bootstrap;

internal sealed class TerminalBootstrapper
{
    readonly ITerminal _terminal;
    readonly IWidget _content;
    readonly IWidget? _keyboardFocus;
    readonly IReadOnlyList<Action<IUiObserver>> _screenConfigurators;
    readonly string? _title;
    readonly Action<ConsoleCancelEventArgs> _onCancel;

    public TerminalBootstrapper(ITerminal terminal,
                                IWidget content,
                                IWidget? keyboardFocus,
                                IReadOnlyList<Action<IUiObserver>> screenConfigurators,
                                string? title,
                                Action<ConsoleCancelEventArgs> onCancel)
    {
        _terminal = terminal;
        _content = content;
        _keyboardFocus = keyboardFocus;
        _screenConfigurators = screenConfigurators;
        _title = title;
        _onCancel = onCancel;
    }

    public IThothConsoleSession Start(CancellationToken ct)
    {
        var environment = HostEnvironmentProbe.Probe(_terminal);
        var capabilities = TerminalCapabilityResolver.Resolve(environment, _terminal.Kind);
        var sessionOptions = new TerminalSessionOptions(_onCancel, _title, environment, capabilities);
        var widthProvider = WidthProviders.ForProfile(capabilities.WidthProfile);

        var attention = new AttentionManager(_terminal, _content, _keyboardFocus, widthProvider);
        foreach (var configure in _screenConfigurators) configure(attention);

        return new TerminalRuntime(attention, _terminal, ct, sessionOptions);
    }
}
