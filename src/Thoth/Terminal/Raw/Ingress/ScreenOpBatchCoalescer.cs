using System.Text;

namespace Thoth.Terminal.Raw.Ingress;

public sealed class ScreenOpBatchCoalescer
{
    readonly StringBuilder _text = new(128);
    int _activeIndex = -1;

    public bool TryMerge(List<ScreenOp> opsBuffer, ScreenOp op)
    {
        if (op.Message != null || opsBuffer.Count == 0) return false;

        var lastIndex = opsBuffer.Count - 1;
        if (_activeIndex >= 0 && _activeIndex != lastIndex) Flush(opsBuffer);

        return op.Coalescence switch
        {
            ScreenOpCoalesce.Last => TryMergeLast(opsBuffer, op, lastIndex),
            ScreenOpCoalesce.AppendText => TryMergeAppendText(opsBuffer, op, lastIndex),
            ScreenOpCoalesce.SumA => TryMergeSumA(opsBuffer, op, lastIndex),
            ScreenOpCoalesce.SumB => TryMergeSumB(opsBuffer, op, lastIndex),
            ScreenOpCoalesce.SumAB => TryMergeSumAB(opsBuffer, op, lastIndex),
            _ => false
        };
    }

    public void Flush(List<ScreenOp> opsBuffer)
    {
        if (_activeIndex < 0)
        {
            return;
        }

        if (_activeIndex < opsBuffer.Count)
        {
            var op = opsBuffer[_activeIndex];
            opsBuffer[_activeIndex] = op with { Text = _text.ToString() };
        }

        _activeIndex = -1;
        _text.Clear();
    }

    static bool TryMergeLast(List<ScreenOp> opsBuffer, ScreenOp op, int lastIndex)
    {
        var last = opsBuffer[lastIndex];
        if (!CanMergePair(last, op)) return false;

        opsBuffer[lastIndex] = op;
        return true;
    }

    bool TryMergeAppendText(List<ScreenOp> opsBuffer, ScreenOp op, int lastIndex)
    {
        var last = opsBuffer[lastIndex];
        if (!CanMergePair(last, op)) return false;
        if (!TryGetAppendText(last, out var lastText, out var lastChar)) return false;
        if (!TryGetAppendText(op, out var opText, out var opChar)) return false;

        if (_activeIndex != lastIndex)
        {
            _text.Clear();
            if (lastText != null)
                _text.Append(lastText);
            else
                _text.Append(lastChar);

            last = last with { Text = string.Empty, ReservedA = 0, ReservedB = 0 };
            opsBuffer[lastIndex] = last;
            _activeIndex = lastIndex;
        }

        if (opText != null)
            _text.Append(opText);
        else
            _text.Append(opChar);

        return true;
    }

    static bool TryMergeSumA(List<ScreenOp> opsBuffer, ScreenOp op, int lastIndex)
    {
        var last = opsBuffer[lastIndex];
        if (!CanMergePair(last, op)) return false;

        opsBuffer[lastIndex] = last with { ReservedA = AddClamped(last.ReservedA, op.ReservedA) };
        return true;
    }

    static bool TryMergeSumB(List<ScreenOp> opsBuffer, ScreenOp op, int lastIndex)
    {
        var last = opsBuffer[lastIndex];
        if (!CanMergePair(last, op)) return false;

        opsBuffer[lastIndex] = last with { ReservedB = AddClamped(last.ReservedB, op.ReservedB) };
        return true;
    }

    static bool TryMergeSumAB(List<ScreenOp> opsBuffer, ScreenOp op, int lastIndex)
    {
        var last = opsBuffer[lastIndex];
        if (!CanMergePair(last, op)) return false;

        opsBuffer[lastIndex] = last with
        {
            ReservedA = AddClamped(last.ReservedA, op.ReservedA),
            ReservedB = AddClamped(last.ReservedB, op.ReservedB)
        };
        return true;
    }

    static int AddClamped(int left, int right)
    {
        var sum = (long)left + right;
        if (sum > int.MaxValue) return int.MaxValue;
        if (sum < int.MinValue) return int.MinValue;
        return (int)sum;
    }

    static bool CanMergePair(ScreenOp last, ScreenOp op)
    {
        if (last.Message != null) return false;

        return last.Target == op.Target &&
               last.Kind == op.Kind &&
               last.Coalescence == op.Coalescence;
    }

    static bool TryGetAppendText(ScreenOp op, out string? text, out char ch)
    {
        text = null;
        ch = default;

        if (!string.IsNullOrEmpty(op.Text))
        {
            text = op.Text;
            return true;
        }

        if (op.Kind != ScreenOpKind.Key || op.ReservedA < 32) return false;

        var key = (ConsoleKey)(op.ReservedB & 0xFF);
        var mods = (ConsoleModifiers)((op.ReservedB >> 8) & 0xFF);
        if ((mods & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) return false;
        if (IsNavigationKey(key)) return false;

        ch = (char)op.ReservedA;
        return true;
    }

    static bool IsNavigationKey(ConsoleKey key)
    {
        return key is ConsoleKey.LeftArrow or
            ConsoleKey.RightArrow or
            ConsoleKey.UpArrow or
            ConsoleKey.DownArrow or
            ConsoleKey.Home or
            ConsoleKey.End or
            ConsoleKey.PageUp or
            ConsoleKey.PageDown or
            ConsoleKey.Backspace or
            ConsoleKey.Delete or
            ConsoleKey.Escape or
            ConsoleKey.Tab or
            ConsoleKey.Enter;
    }
}
