namespace Thoth.Terminal.Raw.Ingress;

public enum ScreenOpCoalesce : byte
{
    None,
    Last,
    AppendText,
    SumA,
    SumB,
    SumAB
}
