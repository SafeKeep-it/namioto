using Thoth.Terminal.Raw;

namespace Thoth.Terminal;

internal sealed class TerminalSession : IDisposable
{
    readonly IDisposable _ansiSession;
    readonly bool _rawModeEnabled;

    internal TerminalSession(TerminalSessionOptions options)
    {
        if (options.Capabilities.EnableRawMode)
        {
            RawMode.Enable();
            _rawModeEnabled = true;
        }

        _ansiSession = AnsiTerminal.Initialize(new(options.OnCancel, options.Title));
        if (options.Capabilities.EnableAnsiOptions)
            AnsiTerminal.EnableAnsiOptions();
    }

    public void Dispose()
    {
        _ansiSession.Dispose();

        if (_rawModeEnabled)
            RawMode.Disable();
    }
}
