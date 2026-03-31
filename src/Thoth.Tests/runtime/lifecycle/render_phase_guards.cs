using Shouldly;
using Thoth.Eventing;
using Thoth.Rendering;
using Thoth.Tests.utilities;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.lifecycle;

public sealed class render_phase_guards : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void visual_tree_mutation_during_render_is_blocked()
    {
        var root = new Screen();
        root.Add(new render_mutates_tree_widget(root));

        var engine = new FrameRenderer(fullRender: false);
        var ex = Should.Throw<InvalidOperationException>(
            () => engine.RenderFrame(root,
                                     new(root),
                                     20,
                                     4,
                                     new Dictionary<IWidget, InvalidationKind>()));

        ex.Message.ShouldContain("Screen.Add");
    }

    [Fact]
    public void input_dispatch_during_render_is_blocked()
    {
        var root = new Screen();
        AttentionManager? attention = null;
        root.Add(new render_dispatches_input_widget(() => attention!));

        var terminal = new Comptatata.Tests.App.Cli.MockTerminal();
        attention = new(terminal, root);

        var ex = Should.Throw<InvalidOperationException>(() => attention.Render());
        ex.Message.ShouldContain("AttentionManager.HandleKey");
    }

    sealed class render_mutates_tree_widget(Screen root) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(1, 1);

        public override void Render(Canvas canvas)
        {
            root.Add(new TextBar());
        }
    }

    sealed class render_dispatches_input_widget(Func<AttentionManager> getAttention) : TestWidgetBase
    {
        public override Size Measure(SizeConstraint constraint) => new(1, 1);

        public override void Render(Canvas canvas)
        {
            getAttention().HandleKey(new('a', ConsoleKey.A, false, false, false));
        }
    }
}
