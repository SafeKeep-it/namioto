namespace Thoth.Eventing;

[Obsolete("OBSOLETE")]
public interface ICaptured<T> : IEventObserver<T> where T : struct { }