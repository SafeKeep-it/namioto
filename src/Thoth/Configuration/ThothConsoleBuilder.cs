using Thoth.Terminal;
using Thoth.Terminal.Bootstrap;
using Thoth.Widgets;

namespace Thoth;

internal class ThothConsoleBuilder : IThothConsoleBuilder
{
    string? _title;
    ITerminal? _terminal;
    IWidget? _root;
    readonly List<Action<IUiObserver>> _screenConfigurators = [];

    public IThothConsoleBuilder On<T>(Action<T> action) where T : struct
    {
        _screenConfigurators.Add(screen => screen.On(action));
        return this;
    }

    public IThothConsoleBuilder Title(string title)
    {
        _title = title;
        return this;
    }

    public IThothConsoleBuilder Content(IWidget root)
    {
        _root = root;
        return this;
    }

    public IThothConsoleBuilder Terminal(ITerminal terminal)
    {
        _terminal = terminal;
        return this;
    }

    public IThothConsoleSession Start(CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(_root);
        var terminal = _terminal ?? new SystemTerminal();
        var bootstrapper = new TerminalBootstrapper(terminal,
                                                    _root,
                                                    keyboardFocus: null,
                                                    _screenConfigurators,
                                                    _title,
                                                    _ => { });
        return bootstrapper.Start(ct);
    }
}
