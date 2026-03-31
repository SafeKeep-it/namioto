using System.Diagnostics;
using System.Text;

namespace Thoth.Terminal;

public static class AnsiTerminal
{
    static TextWriter _output = Console.Out;

    public static IDisposable Initialize(AnsiTerminalOptions options)
    {
        _output = Console.Out;
        var originalEncoding = Console.OutputEncoding;
        var originalControlC = Console.TreatControlCAsInput;

        Console.OutputEncoding = Encoding.UTF8;

        if (OperatingSystem.IsWindows())
        {
            Console.TreatControlCAsInput = true;

            Console.CancelKeyPress += (ConsoleCancelEventHandler)((_, e) =>
            {
                e.Cancel = true;
                options.CancelKeyPress(e);
            });
        }

        _output.Write(TerminalProtocolSequences.Csi.EnableAlternateBuffer);
        if (options.Title != null) _output.Write(TerminalProtocolSequences.Osc.SetTitle(options.Title));
        _output.Flush();

        return new TerminalSession(originalEncoding, originalControlC, (sender, args) => { });
    }

    public static void EnableAnsiOptions()
    {
        try
        {
            if (Console.IsOutputRedirected) return;
            _output.Write(TerminalProtocolSequences.Csi.EnableMouseClick); // enable mouse (click)
            _output.Write(
                TerminalProtocolSequences.Csi.EnableMouseButtonEvent); // button-event tracking (press/drag/release)
            _output.Write(TerminalProtocolSequences.Csi.EnableMouseAnyEvent); // any-event tracking (motion)
            _output.Write(TerminalProtocolSequences.Csi.EnableSgrExtendedMode); // SGR extended mode
            _output.Write(TerminalProtocolSequences.Csi.EnableBracketedPaste); // enable bracketed paste
            _output.Write(TerminalProtocolSequences.Csi.EnableFixKeyboard); // enable CSI u / KKP
            _output.Write(TerminalProtocolSequences.Csi.EnableModifyOtherKeys); // enable modifyOtherKeys
            _output.Flush();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or
                                         InvalidOperationException)
        {
            /* ignore */
        }
    }

    public static void DisableAnsiOptions() => DisableAnsiOptions(true);

    static void DisableAnsiOptions(bool flush)
    {
        try
        {
            if (Console.IsOutputRedirected) return;
            _output.Write(TerminalProtocolSequences.Csi.DisableMouseAnyEvent); // disable any-event
            _output.Write(TerminalProtocolSequences.Csi.DisableMouseButtonEvent); // disable button-event
            _output.Write(TerminalProtocolSequences.Csi.DisableMouseClick); // disable basic mouse
            _output.Write(TerminalProtocolSequences.Csi.DisableSgrExtendedMode); // disable SGR
            _output.Write(TerminalProtocolSequences.Csi.DisableBracketedPaste);
            _output.Write(TerminalProtocolSequences.Csi.DisableFixKeyboard);
            _output.Write(TerminalProtocolSequences.Csi.DisableModifyOtherKeys);
            if (flush) _output.Flush();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or
                                         InvalidOperationException)
        {
            /* ignore */
        }
    }

    sealed class TerminalSession(Encoding originalEncoding,
                                 bool originalControlC,
                                 ConsoleCancelEventHandler handler) : IDisposable
    {
        bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                DisableAnsiOptions(false);
                _output.Write(TerminalProtocolSequences.Csi.DisableSyncFrame);
                _output.Write(TerminalProtocolSequences.Csi.DisableAlternateBuffer);
                _output.Flush();

                Console.CancelKeyPress -= handler;
                if (OperatingSystem.IsWindows()) Console.TreatControlCAsInput = originalControlC;
                Console.OutputEncoding = originalEncoding;
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or
                                             InvalidOperationException)
            {
                Debug.WriteLine($"Terminal input read error: {ex.Message}");
            }
        }
    }
}
