using static Thoth.Terminal.Raw.Ingress.InputReader;
using Thoth.Eventing;

namespace Thoth.Terminal.Raw.Ingress;

internal class ScreenOpApply
{
    readonly AttentionManager? _attention;

    public ScreenOpApply(AttentionManager? attention = null)
    {
        _attention = attention;
    }

    public void Apply(ScreenOp op, int w, int h, CancellationToken ct)
    {
        switch (op.Kind)
        {
            case ScreenOpKind.Key:
                if (op.ReservedA != 0 || op.ReservedB != 0 || op.Text != null)
                {
                    var k = UnpackKey(op.ReservedA, op.ReservedB);
                    if (op.Text is { Length: > 0 })
                    {
                        _attention?.HandleText(op.Text);
                    }
                    else
                    {
                        _attention?.HandleKey(k);
                    }
                }

                return;
            case ScreenOpKind.Paste:
                if (op.Text != null)
                {
                    _attention?.HandlePaste(op.Text);
                }

                return;
            case ScreenOpKind.MouseScroll:
                var x = op.ReservedA;
                (var y, var delta, var _) = UnpackMouseB(op.ReservedB);
                _attention?.HandleScroll(x, y, delta);
                return;
            case ScreenOpKind.MouseDown:
                var dx = op.ReservedA;
                (var dy, var button, var __) = UnpackMouseB(op.ReservedB);
                _attention?.HandleMouseDown(dx, dy, ToMouseButton(button));
                return;
            case ScreenOpKind.MouseUp:
                var ux = op.ReservedA;
                (var uy, var upButton, var ___) = UnpackMouseB(op.ReservedB);
                _attention?.HandleMouseUp(ux, uy, ToMouseButton(upButton));
                return;
            case ScreenOpKind.MouseMove:
                var mx = op.ReservedA;
                (var my, _, _) = UnpackMouseB(op.ReservedB);
                _attention?.HandleMouseMove(mx, my);
                return;
        }
    }

    static MouseButton ToMouseButton(int button)
    {
        return button switch
        {
            0 => MouseButton.Left,
            1 => MouseButton.Middle,
            2 => MouseButton.Right,
            _ => MouseButton.Other
        };
    }
}
