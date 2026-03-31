using System.Text;
using Shouldly;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.dock_panel_rendering;

public class dock_panel : IAsyncLifetime
{
    ScreenBuffer _buffer = null!;
    public Task InitializeAsync()
    {
        _buffer = new(10, 5);

        var dockPanel = new DockPanel();

        var top = new Dock
                  {
                      Position = DockPosition.Top,
                      Content = new TextBar { LeftTitle = "TOP", Line = "-" }
                  };
        var bottom = new Dock
                     {
                         Position = DockPosition.Bottom,
                         Content = new TextBar { LeftTitle = "BOT", Line = "=" }
                     };
        var fill = new Dock
                   {
                       Position = DockPosition.Fill, Content = new FillWidget { Character = '*' }
                   };

        dockPanel.Add(top);
        dockPanel.Add(bottom);
        dockPanel.Add(fill);

        tree_render_harness.Render(dockPanel, _buffer);
        _buffer.WriteTerminalSnapshotSvg("dock_panel.basic.svg");
        _buffer.WriteLayoutDebugSvg(dockPanel, 10, 5, "dock_panel.basic.svg");

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void lays_out_components_correctly()
    {
        var rows = new string[5];
        for (var y = 0; y < 5; y++)
        {
            var sb = new StringBuilder();
            for (var x = 0; x < 10; x++)
            {
                var cell = _buffer.GetCell(x, y);
                sb.Append((char)cell.GlyphId);
            }

            rows[y] = sb.ToString();
        }

        // Expected:
        // 0: TOP-------
        // 1: **********
        // 2: **********
        // 3: **********
        // 4: BOT=======

        rows[0].ShouldBe("TOP-------");
        rows[1].ShouldBe("**********");
        rows[2].ShouldBe("**********");
        rows[3].ShouldBe("**********");
        rows[4].ShouldBe("BOT=======");
    }
}

public class FillWidget : TestWidgetBase
{
    public char Character { get; set; } = ' ';

    public override void Render(Canvas canvas)
    {
        canvas.Fill(0, 0, canvas.Width, canvas.Height, (Rune)Character, new());
    }
}
