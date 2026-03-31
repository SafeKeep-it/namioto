using System.Text;
using Shouldly;
using Thoth.Eventing;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class terminal_scribe_osc_progress
{
    [Fact]
    public void writes_osc_progress_when_progress_bar_is_present()
    {
        var previous = Environment.GetEnvironmentVariable("THOTH_OSC_PROGRESS");
        Environment.SetEnvironmentVariable("THOTH_OSC_PROGRESS", "1");

        try
        {
            var terminal = new MockTerminal { WindowWidth = 20, WindowHeight = 1 };
            var root = new Screen();
            root.Add(new ProgressBar { Width = 10, Progress = 0.42 });
            var attention = new AttentionManager(terminal, root);

            attention.Render();

            var output = Encoding.UTF8.GetString(terminal.DrainWrittenBytes());
            output.ShouldContain("\u001b]9;4;2;42\a");
        }
        finally
        {
            Environment.SetEnvironmentVariable("THOTH_OSC_PROGRESS", previous);
        }
    }

    [Fact]
    public void clears_osc_progress_when_progress_bar_is_removed()
    {
        var previous = Environment.GetEnvironmentVariable("THOTH_OSC_PROGRESS");
        Environment.SetEnvironmentVariable("THOTH_OSC_PROGRESS", "1");

        try
        {
            var terminal = new MockTerminal { WindowWidth = 20, WindowHeight = 1 };
            var root = new Screen();
            var bar = new ProgressBar { Width = 10, Progress = 0.75 };
            root.Add(bar);
            var attention = new AttentionManager(terminal, root);

            attention.Render();
            _ = terminal.DrainWrittenBytes();

            root.Remove(bar);
            attention.Render();

            var output = Encoding.UTF8.GetString(terminal.DrainWrittenBytes());
            output.ShouldContain("\u001b]9;4;0;0\a");
        }
        finally
        {
            Environment.SetEnvironmentVariable("THOTH_OSC_PROGRESS", previous);
        }
    }
}
