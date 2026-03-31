using System.Text;
using Shouldly;
using Thoth.Rendering.Text;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class text_style_tokenizer
{
    [Fact]
    public void tokenize_creates_one_style_span_per_non_empty_run()
    {
        var tokenizer = new TextStyleTokenizer();
        var delta = tokenizer.Tokenize([
            Run("abc", new StyleId(1)),
            Run("", new StyleId(2)),
            Run("def", new StyleId(3), new LinkId(9))
        ]);

        delta.ReplaceStart.ShouldBe(0);
        delta.ReplaceCount.ShouldBe(0);
        delta.Spans.Count.ShouldBe(2);

        delta.Spans[0].RunIndex.ShouldBe(0);
        delta.Spans[0].ByteLength.ShouldBe(3);
        delta.Spans[0].StyleId.ShouldBe(new StyleId(1));

        delta.Spans[1].RunIndex.ShouldBe(2);
        delta.Spans[1].ByteLength.ShouldBe(3);
        delta.Spans[1].StyleId.ShouldBe(new StyleId(3));
        delta.Spans[1].LinkId.ShouldBe(new LinkId(9));
    }

    [Fact]
    public void apply_edit_returns_scoped_style_patch()
    {
        var tokenizer = new TextStyleTokenizer();

        var beforeRuns = new[]
        {
            Run("alpha", new StyleId(1)),
            Run(" ", new StyleId(2)),
            Run("beta", new StyleId(3))
        };

        var afterRuns = new[]
        {
            Run("alpha", new StyleId(1)),
            Run(" ", new StyleId(2)),
            Run("BETA", new StyleId(4))
        };

        var current = tokenizer.Tokenize(beforeRuns).Spans;
        var delta = tokenizer.ApplyEdit(afterRuns,
                                        new(TextEditKind.Replace, 2, 2, 0, 4),
                                        current);

        delta.ReplaceStart.ShouldBe(2);
        delta.ReplaceCount.ShouldBe(1);
        delta.Spans.Count.ShouldBe(1);
        delta.Spans[0].StyleId.ShouldBe(new StyleId(4));
    }

    static TextStyleRun Run(string text, StyleId styleId, LinkId? linkId = null)
    {
        return new(Encoding.UTF8.GetBytes(text), styleId, linkId);
    }
}
