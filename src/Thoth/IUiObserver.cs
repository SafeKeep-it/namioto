namespace Thoth;

internal interface IUiObserver
{
    void On<T>(Action<T> handler) where T : struct;
}
