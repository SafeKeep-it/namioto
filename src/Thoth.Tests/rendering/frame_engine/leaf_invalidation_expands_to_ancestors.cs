using Shouldly;
using Thoth.Widgets;
using comptatata.tests.app.cli.thoth.rendering.frame_engine.utilities;

namespace comptatata.tests.app.cli.thoth.rendering.frame_engine;

public class leaf_invalidation_expands_to_ancestors : IAsyncLifetime
{
    readonly CapturingFrameDrawStrategy _drawStrategy = new();
    readonly FrameRenderer _frameEngine;
    readonly Screen _root = new();
    readonly Border _parent;
    readonly TextBlock _leaf = new();

    IReadOnlyDictionary<IWidget, InvalidationKind>? _capturedInvalidations;

    public leaf_invalidation_expands_to_ancestors()
    {
        _frameEngine = new(fullRender: false, drawStrategy: _drawStrategy);
        _leaf.Text = "leaf";
        _parent = new() { Content = _leaf };
        _root.Add(_parent);
    }

    public Task InitializeAsync()
    {
        var uiContext = new UiContext(_root);
        _frameEngine.RenderFrame(_root,
                                 uiContext,
                                 20,
                                 5,
                                 new Dictionary<IWidget, InvalidationKind>());

        var invalidations = new Dictionary<IWidget, InvalidationKind>
                            {
                                [_leaf] = InvalidationKind.Content
                            };

        _frameEngine.RenderFrame(_root, uiContext, 20, 5, invalidations);
        _capturedInvalidations = _drawStrategy.LastInvalidations;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void expanded_invalidations_are_present()
    {
        _capturedInvalidations.ShouldNotBeNull();
        _capturedInvalidations!.ContainsKey(_leaf).ShouldBeTrue();
        _capturedInvalidations.ContainsKey(_parent).ShouldBeTrue();
        _capturedInvalidations.ContainsKey(_root).ShouldBeTrue();
    }

    [Fact]
    public void ancestor_invalidations_are_descendant_kind()
    {
        _capturedInvalidations.ShouldNotBeNull();
        _capturedInvalidations![_leaf].ShouldBe(InvalidationKind.Content);
        _capturedInvalidations[_parent].ShouldBe(InvalidationKind.Descendant);
        _capturedInvalidations[_root].ShouldBe(InvalidationKind.Descendant);
    }
}
