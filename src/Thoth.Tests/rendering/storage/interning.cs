using Shouldly;
using Thoth;
using Thoth.Rendering;

namespace Comptatata.Tests.App.Cli.UI.Thoth;

public class interning : IAsyncLifetime
{
    int _index1;
    int _index2;
    int _index3;
    InterningStore<string> _store = null!;

    public Task InitializeAsync()
    {
        _store = new();
        _index1 = _store.Intern("A");
        _index2 = _store.Intern("B");
        _index3 = _store.Intern("A");
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void interns_duplicate_values_to_same_index()
    {
        _index1.ShouldBe(_index3);
    }

    [Fact]
    public void assigns_different_index_to_different_values()
    {
        _index1.ShouldNotBe(_index2);
    }

    [Fact]
    public void retrieves_correct_value_by_index()
    {
        _store.Get(_index1).ShouldBe("A");
        _store.Get(_index2).ShouldBe("B");
    }

    [Fact]
    public void clear_removes_all_values()
    {
        _store.Clear();
        var newIndex = _store.Intern("A");
        newIndex.ShouldBe(0);
    }
}