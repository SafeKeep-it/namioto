namespace Thoth.Terminal.Raw.Ingress;

public readonly record struct ScreenOp(ScreenOpTarget Target,
                                       ScreenOpKind Kind,
                                       ScreenOpCoalesce Coalescence,
                                       int ReservedA,
                                       int ReservedB,
                                       string? Text = null,
                                       object? Message = null);