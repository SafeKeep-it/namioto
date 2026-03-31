using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout;

public class ArrangeVisitorTests
{
    [Fact]
    public void TwoLevelTreeProducesExpectedWidgetSizes()
    {
        var childA = new TestLayoutWidget("child-a", new Size(3, 1));
        var childB = new TestLayoutWidget("child-b", new Size(4, 2));
        var root = new TestLayoutWidget("root", new Size(9, 5), childA, childB);

        var arranged = Arrange(root, new SizeConstraint(10, 6));

        arranged.Length.ShouldBe(3);
        arranged[0].Child.ShouldBe(root);
        arranged[1].Child.ShouldBe(childA);
        arranged[2].Child.ShouldBe(childB);

        arranged[0].Rect.ShouldBe(new Rect(0, 0, 10, 6));
        arranged[1].Rect.ShouldBe(new Rect(0, 0, 3, 1));
        arranged[2].Rect.ShouldBe(new Rect(3, 0, 4, 2));
    }

    [Fact]
    public void SingleWidgetUsesFullConstraintBounds()
    {
        var root = new TestLayoutWidget("root", new Size(1, 1));

        var arranged = Arrange(root, new SizeConstraint(7, 4));

        arranged.Length.ShouldBe(1);
        arranged[0].Child.ShouldBe(root);
        arranged[0].Rect.ShouldBe(new Rect(0, 0, 7, 4));
    }

    static WidgetSize[] Arrange(TestLayoutWidget root, in SizeConstraint constraint)
    {
        var measureBuffer = new WidgetSizeRequest[256];
        var arrangeBuffer = new WidgetSize[256];
        var arrange = new ArrangeVisitor(in constraint, measureBuffer, arrangeBuffer);
        var measure = new MeasureVisitor(measureBuffer, in constraint, arrange);
        measure.Visit(root);
        return arrangeBuffer.AsSpan(0, measure.Count).ToArray();
    }
}

public class DrawVisitorTests
{
    [Fact]
    public void DrawsEveryWidgetSizeInBuffer()
    {
        var recorder = new DrawRecorder();
        var childA = new TestLayoutWidget("child-a", new Size(2, 1), recorder: recorder);
        var childB = new TestLayoutWidget("child-b", new Size(2, 1), recorder: recorder);
        var root = new TestLayoutWidget("root", new Size(5, 3), childA, childB, recorder);

        var arranged = Arrange(root, new SizeConstraint(10, 6));
        var canvas = CreateCanvas(10, 6);
        var visitor = new DrawVisitor(canvas, arranged.AsSpan());

        visitor.Visit(arranged[0].Child);

        recorder.DrawOrder.Count.ShouldBe(arranged.Length);
        recorder.DrawOrder.ShouldBe(["root", "child-a", "child-b"]);
    }

    [Fact]
    public void DrawsInParentBeforeChildrenDepthFirstOrder()
    {
        var recorder = new DrawRecorder();
        var grandChild = new TestLayoutWidget("grand-child", new Size(1, 1), recorder: recorder);
        var child = new TestLayoutWidget("child", new Size(2, 1), grandChild, recorder);
        var sibling = new TestLayoutWidget("sibling", new Size(2, 1), recorder: recorder);
        var root = new TestLayoutWidget("root", new Size(8, 4), child, sibling, recorder);

        var arranged = Arrange(root, new SizeConstraint(12, 6));
        var canvas = CreateCanvas(12, 6);
        var visitor = new DrawVisitor(canvas, arranged.AsSpan());

        visitor.Visit(arranged[0].Child);

        recorder.DrawOrder.ShouldBe(["root", "child", "grand-child", "sibling"]);
    }

     static WidgetSize[] Arrange(TestLayoutWidget root, in SizeConstraint constraint)
    {
        var measureBuffer = new WidgetSizeRequest[256];
        var arrangedBuffer = new WidgetSize[256];
        var arrange = new ArrangeVisitor(in constraint, measureBuffer, arrangedBuffer);
        var measure = new MeasureVisitor(measureBuffer, in constraint, arrange);
        measure.Visit(root);
        return arrangedBuffer.AsSpan(0, measure.Count).ToArray();
    }

    static Canvas CreateCanvas(int width, int height)
    {
        var buffer = new ScreenBuffer(width, height);
        var screen = new Screen();
        var context = new RenderContext(new UiContext(screen));
        return new Canvas(buffer, new Rect(0, 0, width, height), context);
    }
}

public class LayoutPlannerTests
{
    [Fact]
    public void RedrawRunsArrangePopulatesLayoutStateAndDraws()
    {
        var recorder = new DrawRecorder();
        var child = new TestLayoutWidget("child", new Size(3, 2), recorder: recorder);
        var root = new TestLayoutWidget("root", new Size(8, 5), child, recorder);
        var planner = new LayoutPlanner();
        var layoutState = new FrameLayoutState();

        planner.Redraw(root, new SizeConstraint(12, 7), CreateCanvas(12, 7), layoutState);

        root.Layout.MeasureCount.ShouldBe(1);
        root.Layout.ArrangeCount.ShouldBe(1);
        root.Layout.DrawCount.ShouldBe(1);
        child.Layout.MeasureCount.ShouldBe(1);
        child.Layout.ArrangeCount.ShouldBe(1);
        child.Layout.DrawCount.ShouldBe(1);

        layoutState.TryGetRect(root, out var rootRect).ShouldBeTrue();
        layoutState.TryGetRect(child, out var childRect).ShouldBeTrue();
        rootRect.ShouldBe(new Rect(0, 0, 12, 7));
        childRect.ShouldBe(new Rect(0, 0, 3, 2));
        recorder.DrawOrder.ShouldBe(["root", "child"]);
    }

    [Fact]
    public void RedrawSkipsLayoutWhenRootIsCleanAndArrangeAlreadyExists()
    {
        var recorder = new DrawRecorder();
        var child = new TestLayoutWidget("child", new Size(2, 1), recorder: recorder);
        var root = new TestLayoutWidget("root", new Size(8, 5), child, recorder);
        var planner = new LayoutPlanner();
        var layoutState = new FrameLayoutState();

        planner.Redraw(root, new SizeConstraint(10, 6), CreateCanvas(10, 6), layoutState);
        planner.Redraw(root, new SizeConstraint(10, 6), CreateCanvas(10, 6), layoutState);

        root.Layout.MeasureCount.ShouldBe(1);
        root.Layout.ArrangeCount.ShouldBe(1);
        child.Layout.MeasureCount.ShouldBe(1);
        child.Layout.ArrangeCount.ShouldBe(1);

        root.Layout.DrawCount.ShouldBe(2);
        child.Layout.DrawCount.ShouldBe(2);
        recorder.DrawOrder.ShouldBe(["root", "child", "root", "child"]);
    }

    [Fact]
    public void MarkLayoutDirtyForcesLayoutRerunOnNextRedraw()
    {
        var root = new TestLayoutWidget("root", new Size(8, 5));
        var planner = new LayoutPlanner();
        var layoutState = new FrameLayoutState();

        planner.Redraw(root, new SizeConstraint(10, 6), CreateCanvas(10, 6), layoutState);
        planner.MarkLayoutDirty(root);
        planner.Redraw(root, new SizeConstraint(10, 6), CreateCanvas(10, 6), layoutState);

        root.Layout.MeasureCount.ShouldBe(2);
        root.Layout.ArrangeCount.ShouldBe(2);
        root.Layout.DrawCount.ShouldBe(2);
    }

    [Fact]
    public void ClearDirtyResultsInEmptyDirtySets()
    {
        var root = new TestLayoutWidget("root", new Size(1, 1));
        var planner = new LayoutPlanner();

        planner.MarkLayoutDirty(root);
        planner.MarkContentDirty(root);
        planner.ClearDirty();

        var dirty = planner.GetDirtyMap();
        dirty.IsLayoutDirty(root).ShouldBeFalse();
        dirty.IsContentDirty(root).ShouldBeFalse();
    }

    static Canvas CreateCanvas(int width, int height)
    {
        var buffer = new ScreenBuffer(width, height);
        var screen = new Screen();
        var context = new RenderContext(new UiContext(screen));
        return new Canvas(buffer, new Rect(0, 0, width, height), context);
    }
}

public class DirtyMapTests
{
    [Fact]
    public void AllDirtyMarksEveryWidgetDirty()
    {
        var first = new TestLayoutWidget("first", new Size(1, 1));
        var second = new TestLayoutWidget("second", new Size(1, 1));
        var allDirty = DirtyMap.AllDirty;

        allDirty.IsLayoutDirty(first).ShouldBeTrue();
        allDirty.IsLayoutDirty(second).ShouldBeTrue();
        allDirty.IsContentDirty(first).ShouldBeTrue();
        allDirty.IsContentDirty(second).ShouldBeTrue();
    }

    [Fact]
    public void ClearDirtyWithoutNewMarksReportsLayoutClean()
    {
        var planner = new LayoutPlanner();
        var widget = new TestLayoutWidget("root", new Size(1, 1));

        planner.ClearDirty();
        var dirty = planner.GetDirtyMap();

        dirty.IsLayoutDirty(widget).ShouldBeFalse();
    }
}

sealed class DrawRecorder
{
    public List<string> DrawOrder { get; } = [];
}

struct NoOpDrawVisitor : IVisitor
{
    public readonly void Visit(IWidgetWithLayout _) { }
}

sealed class TestLayoutWidget : IWidgetWithLayout
{
    readonly List<TestLayoutWidget> _children;

    public TestLayoutWidget(string name,
                            Size desired,
                            params TestLayoutWidget[] children)
    {
        Name = name;
        Layout = new TestLayoutCreator(desired);
        _children = children.ToList();
        for (var i = 0; i < _children.Count; i++)
            _children[i].Parent = this;
    }

    public TestLayoutWidget(string name,
                            Size desired,
                            DrawRecorder recorder,
                            params TestLayoutWidget[] children)
        : this(name, desired, children)
    {
        Layout.Recorder = recorder;
    }

    public TestLayoutWidget(string name,
                            Size desired,
                            TestLayoutWidget child,
                            DrawRecorder recorder)
        : this(name, desired, child)
    {
        Layout.Recorder = recorder;
    }

    public TestLayoutWidget(string name,
                            Size desired,
                            TestLayoutWidget childA,
                            TestLayoutWidget childB,
                            DrawRecorder recorder)
        : this(name, desired, childA, childB)
    {
        Layout.Recorder = recorder;
    }

    public string Name { get; }

    public TestLayoutCreator Layout { get; }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public ILayoutCreator GetLayoutCreator() => Layout;

    public IWidgetRenderer GetRenderer() => throw new NotImplementedException();

    public IWidgetScribe GetScribe() => throw new NotImplementedException();

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
        for (var i = 0; i < _children.Count; i++)
            visitor.Visit(_children[i]);
    }

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
        _ = visitor;
    }
}

sealed class TestLayoutCreator(Size desired) : ILayoutCreator
{
    public DrawRecorder? Recorder { get; set; }

    public int MeasureCount { get; private set; }

    public int ArrangeCount { get; private set; }

    public int DrawCount { get; private set; }

    public WidgetSizeRequest Measure(in IWidgetWithLayout widget,
                                     in SizeConstraint constraint,
                                     ReadOnlySpan<WidgetSizeRequest> requests)
    {
        _ = constraint;
        _ = requests;
        MeasureCount++;
        return new WidgetSizeRequest(widget, this, desired);
    }

    public void Arrange(in IWidgetWithLayout widget,
                        in WidgetSize actual,
                        ReadOnlySpan<WidgetSizeRequest> childRequests,
                        Span<WidgetSize> children)
    {
        _ = widget;
        ArrangeCount++;

        var nextX = actual.Rect.X;
        for (var i = 0; i < childRequests.Length; i++)
        {
            var request = childRequests[i];
            var childRect = new Rect(nextX,
                                     actual.Rect.Y,
                                     request.Size.Width,
                                     request.Size.Height);
            children[i] = new WidgetSize(request.Child, request.Renderer, childRect);
            nextX += request.Size.Width;
        }
    }

    public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
    {
        _ = canvas;
        DrawCount++;
        if (widget is TestLayoutWidget namedWidget)
            Recorder?.DrawOrder.Add(namedWidget.Name);
    }
}
