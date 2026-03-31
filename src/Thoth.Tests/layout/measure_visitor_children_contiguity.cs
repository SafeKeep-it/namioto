using Shouldly;
using Thoth.Rendering;
using Thoth.Widgets;
using Thoth.Widgets.Layout;

namespace Comptatata.Tests.App.Cli.UI.Thoth.layout.measure_visitor;

public class MeasureVisitorChildrenContiguity
{
    [Fact]
    public void LeafOnly()
    {
        var root = new FakeWidget("Root");

        Measure(root);

        ShouldHaveSingleMeasureCall(root);
    }

    [Fact]
    public void SingleChildChain()
    {
        var c = new FakeWidget("C");
        var b = new FakeWidget("B", c);
        var a = new FakeWidget("A", b);
        var root = new FakeWidget("Root", a);

        Measure(root);

        ShouldHaveSingleMeasureCall(root, "A");
        ShouldHaveSingleMeasureCall(a, "B");
        ShouldHaveSingleMeasureCall(b, "C");
        ShouldHaveSingleMeasureCall(c);
    }

    [Fact]
    public void BalancedTree()
    {
        var c = new FakeWidget("C");
        var d = new FakeWidget("D");
        var a = new FakeWidget("A", c, d);
        var b = new FakeWidget("B");
        var root = new FakeWidget("Root", a, b);

        Measure(root);

        ShouldHaveSingleMeasureCall(root, "A", "B");
        ShouldHaveSingleMeasureCall(a, "C", "D");
        ShouldHaveSingleMeasureCall(b);
        ShouldHaveSingleMeasureCall(c);
        ShouldHaveSingleMeasureCall(d);
    }

    [Fact]
    public void UnbalancedTree()
    {
        var e = new FakeWidget("E");
        var d = new FakeWidget("D", e);
        var c = new FakeWidget("C");
        var a = new FakeWidget("A", c, d);
        var b = new FakeWidget("B");
        var root = new FakeWidget("Root", a, b);

        Measure(root);

        ShouldHaveSingleMeasureCall(d, "E");
        ShouldHaveSingleMeasureCall(a, "C", "D");
        ShouldHaveSingleMeasureCall(root, "A", "B");
    }

    [Fact]
    public void ThreeSiblings()
    {
        var a = new FakeWidget("A");
        var b = new FakeWidget("B");
        var c = new FakeWidget("C");
        var root = new FakeWidget("Root", a, b, c);

        Measure(root);

        ShouldHaveSingleMeasureCall(root, "A", "B", "C");
        ShouldHaveSingleMeasureCall(a);
        ShouldHaveSingleMeasureCall(b);
        ShouldHaveSingleMeasureCall(c);
    }

    static void Measure(FakeWidget root)
    {
        var buffer = new WidgetSizeRequest[1024];
        var layoutVisitor = new NoOpLayoutVisitor();
        var visitor = new MeasureVisitor(buffer, new SizeConstraint(20, 10), layoutVisitor);
        visitor.Visit(root);
    }

    struct NoOpLayoutVisitor : ILayoutVisitor
    {
        public readonly void Visit(IWidgetWithLayout _) { }
    }

    static void ShouldHaveSingleMeasureCall(FakeWidget widget, params string[] expectedChildren)
    {
        widget.Layout.MeasureCalls.Count.ShouldBe(1);
        ShouldMatch(widget.Layout.MeasureCalls[0], expectedChildren);
    }



    static void ShouldMatch(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
    {
        actual.Count.ShouldBe(expected.Count);
        for (var i = 0; i < expected.Count; i++) actual[i].ShouldBe(expected[i]);
    }

    sealed class FakeWidget : IWidgetWithLayout
    {
        readonly FakeWidget[] _children;
        readonly FakeLayoutCreator _layout;

        public FakeWidget(string name, params FakeWidget[] children)
        {
            Name = name;
            _children = children;
            _layout = new FakeLayoutCreator();

            for (var i = 0; i < _children.Length; i++) _children[i].Parent = this;
        }

        public string Name { get; }

        public FakeLayoutCreator Layout => _layout;

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public ILayoutCreator GetLayoutCreator() => _layout;

        public void Walk<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IVisitor, allows ref struct
        {
            visitor.Visit(this);
            Accept(ref visitor);
        }

        public void Accept<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IVisitor, allows ref struct
        {
            for (var i = 0; i < _children.Length; i++) visitor.Visit(_children[i]);
        }

        public IWidgetRenderer GetRenderer() => throw new NotImplementedException();

        public IWidgetScribe GetScribe() => throw new NotImplementedException();

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
            _ = visitor;
        }
    }

    sealed class FakeLayoutCreator : ILayoutCreator
    {
        public List<IReadOnlyList<string>> MeasureCalls { get; } = [];
        public List<IReadOnlyList<string>> ArrangeCalls { get; } = [];

        public WidgetSizeRequest Measure(in IWidgetWithLayout widget, in SizeConstraint constraint,
            ReadOnlySpan<WidgetSizeRequest> desires)
        {
            _ = constraint;

            var childNames = new List<string>(desires.Length);
            for (var i = 0; i < desires.Length; i++)
                childNames.Add(((FakeWidget)desires[i].Child).Name);

            MeasureCalls.Add(childNames);
            return new WidgetSizeRequest(widget, this, new Size(1, 1));
        }

        public void Arrange(in IWidgetWithLayout widget, in WidgetSize actual,
            ReadOnlySpan<WidgetSizeRequest> childDesires,
            Span<WidgetSize> children)
        {
            _ = widget;

            var childNames = new List<string>(childDesires.Length);
            for (var i = 0; i < childDesires.Length; i++)
            {
                childNames.Add(((FakeWidget)childDesires[i].Child).Name);
                children[i] = new WidgetSize(childDesires[i].Child, childDesires[i].Renderer,
                    new Rect(actual.Rect.X, actual.Rect.Y, 1, 1));
            }

            ArrangeCalls.Add(childNames);
        }

        public void Draw(in IWidgetWithLayout widget, in Canvas canvas)
        {
            _ = widget;
            _ = canvas;
        }
    }
}
