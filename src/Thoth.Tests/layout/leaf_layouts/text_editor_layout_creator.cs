using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.leaf_layouts;

public class TextEditorLayoutCreator
{
    static readonly SizeConstraint constraint = new(16, 8);

    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        LeafLayoutTestHelpers.GetLayoutCreator(CreateWidget());
    }

    [Fact]
    public void MeasureReturnsNonZeroSize()
    {
        LeafLayoutTestHelpers.AssertMeasureReturnsNonZeroSize(CreateWidget(), constraint);
    }

    [Fact]
    public void ArrangeWithEmptyChildrenDoesNotThrow()
    {
        var widget = CreateWidget();
        var request = LeafLayoutTestHelpers.Measure(widget, constraint);

        LeafLayoutTestHelpers.AssertArrangeDoesNotThrow(widget, new(0, 0, request.Size.Width, request.Size.Height));
    }

    [Fact]
    public void AcceptVisitsZeroChildren()
    {
        LeafLayoutTestHelpers.AssertAcceptVisitsZero(CreateWidget());
    }

    static TextEditor CreateWidget() => new() { Text = "Hello\nLayout", MinHeight = 1 };
}
