using Shouldly;
using Thoth.Rendering.Text;

namespace Comptatata.Tests.App.Cli.UI.Thoth.text_metrics;

public class rune_width : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void ascii_printable_is_width_1()
    {
        TextMetrics.GetRuneWidth(new('A')).ShouldBe(1);
        TextMetrics.GetRuneWidth(new('z')).ShouldBe(1);
        TextMetrics.GetRuneWidth(new('0')).ShouldBe(1);
        TextMetrics.GetRuneWidth(new(' ')).ShouldBe(1);
        TextMetrics.GetRuneWidth(new('~')).ShouldBe(1);
    }

    [Fact]
    public void control_characters_are_width_0()
    {
        TextMetrics.GetRuneWidth(new('\0')).ShouldBe(0);
        TextMetrics.GetRuneWidth(new('\t')).ShouldBe(0);
        TextMetrics.GetRuneWidth(new('\n')).ShouldBe(0);
        TextMetrics.GetRuneWidth(new('\r')).ShouldBe(0);
        TextMetrics.GetRuneWidth(new(0x1F)).ShouldBe(0); // Unit separator
    }

    [Fact]
    public void cjk_characters_are_width_2()
    {
        TextMetrics.GetRuneWidth(new(0x4E2D)).ShouldBe(2); // 中
        TextMetrics.GetRuneWidth(new(0x65E5)).ShouldBe(2); // 日
        TextMetrics.GetRuneWidth(new(0x672C)).ShouldBe(2); // 本
        TextMetrics.GetRuneWidth(new(0xAC00)).ShouldBe(2); // 가 (Korean)
    }

    [Fact]
    public void fullwidth_characters_are_width_2()
    {
        TextMetrics.GetRuneWidth(new(0xFF01)).ShouldBe(2); // ！ Fullwidth exclamation
        TextMetrics.GetRuneWidth(new(0xFF21)).ShouldBe(2); // Ａ Fullwidth A
    }

    [Fact]
    public void emoji_are_width_2()
    {
        TextMetrics.GetRuneWidth(new(0x1F600)).ShouldBe(2); // 😀
        TextMetrics.GetRuneWidth(new(0x1F680)).ShouldBe(2); // 🚀
        TextMetrics.GetRuneWidth(new(0x2764)).ShouldBe(1); // ❤ (not wide emoji)
    }

    [Fact]
    public void combining_marks_are_width_0()
    {
        TextMetrics.GetRuneWidth(new(0x0300)).ShouldBe(0); // Combining grave accent
        TextMetrics.GetRuneWidth(new(0x0301)).ShouldBe(0); // Combining acute accent
        TextMetrics.GetRuneWidth(new(0x0308)).ShouldBe(0); // Combining diaeresis
    }

    [Fact]
    public void zero_width_characters_are_width_0()
    {
        TextMetrics.GetRuneWidth(new(0x200B)).ShouldBe(0); // Zero-width space
        TextMetrics.GetRuneWidth(new(0x200C)).ShouldBe(0); // ZWNJ
        TextMetrics.GetRuneWidth(new(0x200D)).ShouldBe(0); // ZWJ
        TextMetrics.GetRuneWidth(new(0xFEFF)).ShouldBe(0); // BOM
    }

    [Fact]
    public void soft_hyphen_is_width_0()
    {
        TextMetrics.GetRuneWidth(new(0x00AD)).ShouldBe(0); // Soft hyphen
    }

    [Fact]
    public void delete_character_is_width_0()
    {
        TextMetrics.GetRuneWidth(new(0x007F)).ShouldBe(0); // DEL
    }
}