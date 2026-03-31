using Thoth.Terminal;
using Thoth.Widgets;

namespace Thoth;

public interface IThothConsoleBuilder
{
    public IThothConsoleBuilder On<T>(Action<T> action) where T : struct;
    public IThothConsoleBuilder Title(string title);
    public IThothConsoleBuilder Content(IWidget widget);
    public IThothConsoleBuilder Terminal(ITerminal terminal);
    public IThothConsoleSession Start(CancellationToken ct = default);
}
