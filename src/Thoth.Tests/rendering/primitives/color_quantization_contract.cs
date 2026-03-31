using Shouldly;
using Thoth.Rendering;

namespace Comptatata.Tests.App.Cli.UI.Thoth.canvas_rendering;

public class color_quantization_contract
{
    [Fact]
    public void rgb_exposes_quantized_indexes_within_valid_ranges()
    {
        var color = new Color(123, 45, 210);

        color.Xterm256.ShouldBeInRange(0, 255);
        color.Ansi16.ShouldBeInRange(0, 15);
        color.Ansi8.ShouldBeInRange(0, 7);
    }

    [Fact]
    public void canonical_rgb_values_map_to_expected_ansi_indexes()
    {
        new Color(0, 0, 0).Ansi16.ShouldBe(0);
        new Color(255, 255, 255).Ansi16.ShouldBe(15);
        new Color(255, 0, 0).Ansi16.ShouldBe(9);
    }

    [Fact]
    public void ansi_names_follow_quantized_indexes()
    {
        var red = new Color(255, 0, 0);
        var cyan = new Color(0, 255, 255);

        red.Ansi16Name.ShouldBe("bright_red");
        cyan.Ansi8Name.ShouldBe("cyan");
    }
}
