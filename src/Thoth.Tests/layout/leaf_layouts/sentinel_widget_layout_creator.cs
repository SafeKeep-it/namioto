using Shouldly;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.leaf_layouts;

public class SentinelWidgetLayoutCreator
{
    static readonly SizeConstraint constraint = new(16, 8);

    [Fact]
    public void GetLayoutCreatorReturnsNonNull()
    {
        LeafLayoutTestHelpers.GetLayoutCreator(CreateWidget());
    }

    [Fact]
    public void MeasureReturnsZeroSize()
    {
        var request = LeafLayoutTestHelpers.Measure(CreateWidget(), constraint);

        request.Size.ShouldBe(new Size(0, 0));
    }

    [Fact]
    public void ArrangeWithEmptyChildrenDoesNotThrow()
    {
        LeafLayoutTestHelpers.AssertArrangeDoesNotThrow(CreateWidget(), new(0, 0, 0, 0));
    }

    [Fact]
    public void AcceptVisitsZeroChildren()
    {
        LeafLayoutTestHelpers.AssertAcceptVisitsZero(CreateWidget());
    }

    [Fact]
    public void DrawIsInertAndDoesNotThrow()
    {
        var widget = CreateWidget();
        var creator = LeafLayoutTestHelpers.GetLayoutCreator(widget);
        var buffer = new ScreenBuffer(1, 1);
        var context = new RenderContext(new UiContext(new Screen()));
        var canvas = new Canvas(buffer, new(0, 0, 1, 1), context);

        Should.NotThrow(() => creator.Draw(widget, canvas));
    }

    static SentinelWidget CreateWidget() => SentinelWidget.Instance;
}
