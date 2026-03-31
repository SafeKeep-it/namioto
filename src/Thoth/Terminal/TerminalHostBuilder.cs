using Thoth.Widgets;
using Thoth.Terminal.Bootstrap;

namespace Thoth.Terminal;

public sealed class TerminalHostBuilder
{
    readonly ITerminal _terminal;
    readonly List<Action<IUiObserver>> _screenConfigurators = [];
    IWidget? _content;
    IWidget? _keyboardFocus;
    string? _title;
    Action<ConsoleCancelEventArgs>? _onCancel;

    internal TerminalHostBuilder(ITerminal terminal)
    {
        _terminal = terminal;
    }

    public TerminalHostBuilder Content(IWidget content, IWidget? keyboardFocus = null)
    {
        _content = content;
        _keyboardFocus = keyboardFocus;
        return this;
    }

    public TerminalHostBuilder On<T>(Action<T> handler) where T : struct
    {
        _screenConfigurators.Add(screen => screen.On(handler));
        return this;
    }

    public TerminalHostBuilder Title(string title)
    {
        _title = title;
        return this;
    }

    public TerminalHostBuilder OnCancel(Action<ConsoleCancelEventArgs> onCancel)
    {
        _onCancel = onCancel;
        return this;
    }

    public Task<IThothConsoleSession> StartAsync(CancellationToken ct)
    {
        if (_content is null)
            throw new InvalidOperationException("Content must be configured before StartAsync.");

        var bootstrapper = new TerminalBootstrapper(_terminal,
                                                    _content,
                                                    _keyboardFocus,
                                                    _screenConfigurators,
                                                    _title,
                                                    _onCancel ?? (_ => { }));
        return Task.FromResult(bootstrapper.Start(ct));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await using var thoth = await StartAsync(ct);
        await thoth.WaitForExitAsync(ct);
    }
}
