using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class word_enumeration : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void splits_on_spaces()
    {
        var words = Collect("Hello world");
        words.ShouldBe(["Hello ", "world"]);
    }

    [Fact]
    public void empty_span_yields_nothing()
    {
        var words = Collect("");
        words.ShouldBeEmpty();
    }

    [Fact]
    public void single_word_no_space()
    {
        var words = Collect("Hello");
        words.ShouldBe(["Hello"]);
    }

    [Fact]
    public void consecutive_spaces_are_separate_words()
    {
        // Limitation: consecutive whitespace is not grouped
        var words = Collect("Hello  world");
        words.ShouldBe(["Hello ", " ", "world"]);
    }

    [Fact]
    public void leading_spaces_are_individual_words()
    {
        // Limitation: consecutive whitespace is not grouped
        var words = Collect("  Hello");
        words.ShouldBe([" ", " ", "Hello"]);
    }

    [Fact]
    public void trailing_space_stays_with_word()
    {
        var words = Collect("Hello ");
        words.ShouldBe(["Hello "]);
    }

    [Fact]
    public void tabs_are_word_separators()
    {
        var words = Collect("Hello\tworld");
        words.ShouldBe(["Hello\t", "world"]);
    }

    [Fact]
    public void mixed_whitespace_is_split()
    {
        // Limitation: consecutive whitespace is not grouped
        var words = Collect("A \t B");
        words.ShouldBe(["A ", "\t", " ", "B"]);
    }

    [Fact]
    public void unicode_text_splits_correctly()
    {
        var words = Collect("中文 日本語");
        words.ShouldBe(["中文 ", "日本語"]);
    }

    [Fact]
    public void emoji_with_spaces()
    {
        var words = Collect("Hi 🚀 there");
        words.ShouldBe(["Hi ", "🚀 ", "there"]);
    }

    [Fact]
    public void only_spaces_are_individual()
    {
        // Limitation: consecutive whitespace is not grouped
        var words = Collect("   ");
        words.ShouldBe([" ", " ", " "]);
    }

    [Fact]
    public void single_character()
    {
        var words = Collect("A");
        words.ShouldBe(["A"]);
    }

    [Fact]
    public void single_space()
    {
        var words = Collect(" ");
        words.ShouldBe([" "]);
    }

    static List<string> Collect(string text)
    {
        var result = new List<string>();
        foreach (var w in text.AsSpan().EnumerateWords()) result.Add(new(w));
        return result;
    }
}