using Thoth.Eventing;
using Thoth.Widgets;

namespace Thoth.Bindings;

public static class ObservableBindingExtensions
{
    public static Observable<T> Bind<T>(this Observable<T> observable, IBindingWidget widget)
    {
        return observable.Bind(widget, BindingKind.Value);
    }

    public static Observable<T> Bind<T>(this Observable<T> observable,
                                        IBindingWidget widget,
                                        BindingKind kind)
    {
        BindingUpdateQueue.Register(observable, widget, kind);
        return observable;
    }

    public static void Unbind<T>(this Observable<T> observable, IBindingWidget widget)
    {
        observable.Unbind(widget, BindingKind.Value);
    }

    public static void Unbind<T>(this Observable<T> observable,
                                 IBindingWidget widget,
                                 BindingKind kind)
    {
        BindingUpdateQueue.Unregister(observable, widget, kind);
    }

    public static ObservableCollection<T> Bind<T>(this ObservableCollection<T> observable,
                                                  IBindingWidget widget)
    {
        BindingUpdateQueue.Register(observable, widget, BindingKind.Collection);
        return observable;
    }

    public static void Unbind<T>(this ObservableCollection<T> observable, IBindingWidget widget)
    {
        BindingUpdateQueue.Unregister(observable, widget, BindingKind.Collection);
    }

    public static ObservableCollectionTemplate<T> Bind<T>(this ObservableCollectionTemplate<T> template, IBindingWidget widget)
    {
        BindingUpdateQueue.Register(template.Source, widget, BindingKind.Collection);
        return template;
    }

    public static void Unbind<T>(this ObservableCollectionTemplate<T> template, IBindingWidget widget)
    {
        BindingUpdateQueue.Unregister(template.Source, widget, BindingKind.Collection);
    }

    public static void UnbindAll(this IBindingWidget widget)
    {
        BindingUpdateQueue.UnregisterWidget(widget);
    }
}
