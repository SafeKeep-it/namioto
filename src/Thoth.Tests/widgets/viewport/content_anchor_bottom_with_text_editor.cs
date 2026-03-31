using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.components;

public class auto_scroll_bottom_with_text_editor
{
    [Fact]
    public void when_auto_scroll_bottom_and_user_is_at_bottom_then_new_content_keeps_bottom_visible()
    {
        var viewport = new Viewport
                       {
                           AutoScroll = AutoScrollMode.Bottom,
                           ScrollDirection = ScrollDirection.Vertical
                       };

        var editor = new TextEditor { MinHeight = 1 };
        viewport.Content = editor;

        editor.Text = "1\n2\n3\n4\n5\n6";
        viewport.GetRenderer().Measure(new(12, 4));
        viewport.GetRenderer().Arrange(new(0, 0, 12, 4));
        viewport.OffsetY.ShouldBe(2);

        editor.Text = "1\n2\n3\n4\n5\n6\n7";
        viewport.GetRenderer().Measure(new(12, 4));
        viewport.GetRenderer().Arrange(new(0, 0, 12, 4));
        viewport.OffsetY.ShouldBe(3);
    }
}
