using Thoth.Bindings;
using Thoth.Widgets;

namespace Thoth.Eventing;

public static class BindingUpdateQueue
{
    static readonly Lock gate = new();
    static readonly Dictionary<object, List<Registration>> registrations = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<IWidget, BindingKind> pending = new(ReferenceEqualityComparer.Instance);

    public static void Register(object source, IBindingWidget widget, BindingKind kind)
    {
        lock (gate)
        {
            if (!registrations.TryGetValue(source, out var list))
            {
                list = [];
                registrations[source] = list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (!ReferenceEquals(list[i].Widget, widget)) continue;
                list[i] = list[i] with { Kind = list[i].Kind | kind };
                return;
            }

            list.Add(new(widget, kind));
        }
    }

    public static void Unregister(object source, IBindingWidget widget, BindingKind kind)
    {
        lock (gate)
        {
            if (!registrations.TryGetValue(source, out var list)) return;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(list[i].Widget, widget)) continue;

                var remaining = list[i].Kind & ~kind;
                if (remaining == BindingKind.None)
                    list.RemoveAt(i);
                else
                    list[i] = list[i] with { Kind = remaining };
            }

            if (list.Count == 0) registrations.Remove(source);
        }
    }

    public static void NotifyChanged(object source)
    {
        lock (gate)
        {
            if (!registrations.TryGetValue(source, out var list)) return;

            for (var i = 0; i < list.Count; i++)
            {
                var registration = list[i];
                if (pending.TryGetValue(registration.Widget, out var existing))
                    pending[registration.Widget] = existing | registration.Kind;
                else
                    pending[registration.Widget] = registration.Kind;
            }
        }
    }

    public static bool Flush(EventDispatcher dispatcher)
    {
        KeyValuePair<IWidget, BindingKind>[] snapshot;

        lock (gate)
        {
            if (pending.Count == 0) return false;

            snapshot = pending.ToArray();
            pending.Clear();
        }

        var dispatched = false;
        for (var i = 0; i < snapshot.Length; i++)
            dispatched |= dispatcher.DispatchCommand(snapshot[i].Key, new UpdateBindingsCommand(snapshot[i].Value));

        return dispatched;
    }

    public static void Clear()
    {
        lock (gate)
        {
            registrations.Clear();
            pending.Clear();
        }
    }

    public static void UnregisterWidget(IBindingWidget widget)
    {
        lock (gate)
        {
            var sources = registrations.Keys.ToArray();
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (!registrations.TryGetValue(source, out var list)) continue;

                for (var i = list.Count - 1; i >= 0; i--)
                    if (ReferenceEquals(list[i].Widget, widget))
                        list.RemoveAt(i);

                if (list.Count == 0)
                    registrations.Remove(source);
            }

            pending.Remove(widget);
        }
    }

    public static void PruneDetached(IWidget root)
    {
        var liveWidgets = new HashSet<IWidget>(ReferenceEqualityComparer.Instance) { root };
        var stack = new Stack<IWidget>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var widget = stack.Pop();
            var pushToStack = new PushToStackVisitor(stack);
            WidgetTraversalExtensions.VisitChildrenReverse(widget, ref pushToStack);

            foreach (var child in stack)
                liveWidgets.Add(child);
        }

        lock (gate)
        {
            var sources = registrations.Keys.ToArray();
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (!registrations.TryGetValue(source, out var list)) continue;

                for (var i = list.Count - 1; i >= 0; i--)
                    if (!liveWidgets.Contains(list[i].Widget))
                        list.RemoveAt(i);

                if (list.Count == 0)
                    registrations.Remove(source);
            }

            var pendingTargets = pending.Keys.ToArray();
            for (var i = 0; i < pendingTargets.Length; i++)
                if (!liveWidgets.Contains(pendingTargets[i]))
                    pending.Remove(pendingTargets[i]);
        }
    }

    readonly record struct Registration(IBindingWidget Widget, BindingKind Kind);
}
