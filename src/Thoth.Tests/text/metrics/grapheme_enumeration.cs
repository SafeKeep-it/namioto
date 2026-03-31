using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class grapheme_enumeration : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ascii_text_yields_individual_chars()
    {
        var graphemes = Collect("Hello");
        graphemes.ShouldBe(["H", "e", "l", "l", "o"]);
    }

    [Fact]
    public void empty_span_yields_nothing()
    {
        var graphemes = Collect("");
        graphemes.ShouldBeEmpty();
    }

    [Fact]
    public void surrogate_pair_emoji_is_single_grapheme()
    {
        var graphemes = Collect("A🚀B");
        graphemes.ShouldBe(["A", "🚀", "B"]);
    }

    [Fact]
    public void cjk_characters_are_individual_graphemes()
    {
        var graphemes = Collect("中文");
        graphemes.ShouldBe(["中", "文"]);
    }

    [Fact]
    public void combining_acute_accent_joins_base()
    {
        // e + combining acute = é (2 code units, 1 grapheme)
        var graphemes = Collect("e\u0301");
        graphemes.Count.ShouldBe(1);
        graphemes[0].Length.ShouldBe(2);
    }

    [Fact]
    public void zwj_sequence_splits_on_emoji_boundaries()
    {
        // Limitation: ZWJ joins base with modifier but not across full emoji sequences
        // 👨‍👩‍👧 = man + ZWJ + woman + ZWJ + girl → splits at each emoji
        var family = "👨\u200D👩\u200D👧";
        var graphemes = Collect(family);
        graphemes.Count
                 .ShouldBe(
                     3); // Each emoji is separate (ZWJ consumed but next emoji starts new grapheme)
    }

    [Fact]
    public void skin_tone_modifier_joins_emoji()
    {
        // 👋🏽 = waving hand + skin tone
        var graphemes = Collect("👋🏽");
        graphemes.Count.ShouldBe(1);
    }

    [Fact]
    public void variation_selector_joins_base()
    {
        // ❤️ = heart + variation selector 16
        var graphemes = Collect("❤\uFE0F");
        graphemes.Count.ShouldBe(1);
    }

    [Fact]
    public void multiple_combining_marks_join()
    {
        // a + combining ring above + combining acute
        var graphemes = Collect("a\u030A\u0301");
        graphemes.Count.ShouldBe(1);
        graphemes[0].Length.ShouldBe(3);
    }

    [Fact]
    public void mixed_content_handles_all()
    {
        var graphemes = Collect("Hi中🚀");
        graphemes.ShouldBe(["H", "i", "中", "🚀"]);
    }

    [Fact]
    public void control_characters_are_individual_graphemes()
    {
        var graphemes = Collect("A\tB\nC");
        graphemes.ShouldBe(["A", "\t", "B", "\n", "C"]);
    }

    static List<string> Collect(string text)
    {
        var result = new List<string>();
        foreach (var g in text.AsSpan().EnumerateGraphemes()) result.Add(new(g));
        return result;
    }
}