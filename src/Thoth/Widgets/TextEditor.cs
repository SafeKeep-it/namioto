using System.Globalization;
using System.Text;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Rendering;
using Thoth.Rendering.Text;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

public class TextEditor : IWidget,
                          IWidgetWithLayout,
                          IEventHandler<KeyPressedInput>,
                          IEventHandler<TextInput>,
                          IEventHandler<PasteInput>,
                          IEventHandler<OnMouseDown>,
                          IEventHandler<OnMouseMove>,
                          IEventHandler<OnMouseUp>,
                          IEventHandler<OnFocus>,
                          Navigation.Focus.IAutoFocusable
{
    readonly TextEditorScribe _scribe;
    readonly StringBuilder _text = new();
    int _caretIndex;
    int _selectionAnchor = -1;
    bool _isSelectingWithMouse;
    (int x, int y) _lastVisualCaretPos = (-1, -1);

    public string Text
    {
        get => _text.ToString();
        set
        {
            _text.Clear();
            _text.Append(value);
            _caretIndex = Math.Clamp(_caretIndex, 0, _text.Length);
            _selectionAnchor = -1;
        }
    }

    public int CaretIndex
    {
        get => _caretIndex;
        set => _caretIndex = Math.Clamp(value, 0, _text.Length);
    }

    public (int Start, int End)? SelectionRange
    {
        get
        {
            if (!HasSelection) return null;
            return (Math.Min(_selectionAnchor, _caretIndex), Math.Max(_selectionAnchor, _caretIndex));
        }
    }

    public bool HasSelection => _selectionAnchor >= 0 && _selectionAnchor != _caretIndex;

    public Style Style { get; set; } = new();
    public int MinHeight { get; set; } = 3;
    public bool AcceptsFocus { get; set; } = true;

    public TextEditor()
    {
        _scribe = new(this);
    }

    void IEventHandler<KeyPressedInput>.Handle(IEventContext ctx, in KeyPressedInput e)
    {
        var key = e.Key;
        var handled = false;

        if (key.Key == ConsoleKey.LeftArrow)
        {
            if (_caretIndex > 0)
            {
                var oldCaret = _caretIndex;
                if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
                {
                    _caretIndex = MoveCaretToPreviousWord();
                    handled = true;
                }
                else
                {
                    var text = _text.ToString();
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    var lastIndex = 0;
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.ElementIndex == _caretIndex)
                        {
                            _caretIndex = lastIndex;
                            handled = true;
                            break;
                        }

                        lastIndex = enumerator.ElementIndex;
                    }

                    if (!handled)
                    {
                        _caretIndex = lastIndex;
                        handled = true;
                    }
                }

                if (handled) UpdateSelectionForHorizontalNavigation(key.Modifiers, oldCaret);

                ctx.RaiseEvent(new OnContentChanged());
            }
            else
            {
                handled = true;
            }
        }
        else if (key.Key == ConsoleKey.RightArrow)
        {
            if (_caretIndex < _text.Length)
            {
                var oldCaret = _caretIndex;
                if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
                {
                    _caretIndex = MoveCaretToNextWord();
                    handled = true;
                }
                else
                {
                    var text = _text.ToString();
                    var enumerator = StringInfo.GetTextElementEnumerator(text);
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.ElementIndex == _caretIndex)
                        {
                            if (enumerator.MoveNext())
                                _caretIndex = enumerator.ElementIndex;
                            else
                                _caretIndex = _text.Length;
                            handled = true;
                            break;
                        }
                    }

                    if (!handled)
                    {
                        _caretIndex = _text.Length;
                        handled = true;
                    }
                }

                if (handled) UpdateSelectionForHorizontalNavigation(key.Modifiers, oldCaret);

                ctx.RaiseEvent(new OnContentChanged());
            }
            else
            {
                handled = true;
            }
        }
        else if (key.Key == ConsoleKey.UpArrow)
        {
            var oldCaret = _caretIndex;
            handled = MoveCaretVertical(ctx, -1);
            if (handled) UpdateSelectionForVerticalNavigation(key.Modifiers, oldCaret);
            if (handled) ctx.RaiseEvent(new OnContentChanged());
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
            var oldCaret = _caretIndex;
            handled = MoveCaretVertical(ctx, 1);
            if (handled) UpdateSelectionForVerticalNavigation(key.Modifiers, oldCaret);
            if (handled) ctx.RaiseEvent(new OnContentChanged());
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (HasSelection)
            {
                ApplyTextMutation(ctx, DeleteSelectionIfAny);
                handled = true;
            }
            else if (_caretIndex > 0)
            {
                if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    ApplyTextMutation(ctx, () =>
                    {
                        var start = MoveCaretToPreviousWord();
                        _text.Remove(start, _caretIndex - start);
                        _caretIndex = start;
                        _selectionAnchor = -1;
                    });
                    handled = true;
                }
                else
                {
                    ApplyTextMutation(ctx, () =>
                    {
                        var text = _text.ToString();
                        var enumerator = StringInfo.GetTextElementEnumerator(text);
                        var lastIndex = 0;
                        var lastLength = 0;
                        while (enumerator.MoveNext())
                        {
                            if (enumerator.ElementIndex == _caretIndex) break;
                            lastIndex = enumerator.ElementIndex;
                            lastLength = enumerator.GetTextElement().Length;
                        }

                        _text.Remove(lastIndex, lastLength);
                        _caretIndex = lastIndex;
                        _selectionAnchor = -1;
                    });
                    handled = true;
                }
            }
            else
            {
                handled = true;
            }
        }
        else if (key.Key == ConsoleKey.Delete)
        {
            if (HasSelection)
            {
                ApplyTextMutation(ctx, DeleteSelectionIfAny);
                handled = true;
            }
            else if (_caretIndex < _text.Length)
            {
                if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                {
                    ApplyTextMutation(ctx, () =>
                    {
                        var end = MoveCaretToNextWord();
                        if (end > _caretIndex)
                            _text.Remove(_caretIndex, end - _caretIndex);
                        _selectionAnchor = -1;
                    });
                    handled = true;
                }
                else
                {
                    ApplyTextMutation(ctx, () =>
                    {
                        var text = _text.ToString();
                        var enumerator = StringInfo.GetTextElementEnumerator(text);
                        while (enumerator.MoveNext())
                        {
                            if (enumerator.ElementIndex == _caretIndex)
                            {
                                _text.Remove(_caretIndex, enumerator.GetTextElement().Length);
                                _selectionAnchor = -1;
                                handled = true;
                                break;
                            }
                        }
                    });
                }
            }
            else
            {
                handled = true;
            }
        }
        else if (key.Key == ConsoleKey.Home)
        {
            var oldCaret = _caretIndex;
            _caretIndex = 0;
            if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                if (_selectionAnchor < 0) _selectionAnchor = oldCaret;
                if (_selectionAnchor == _caretIndex) _selectionAnchor = -1;
            }
            else
            {
                _selectionAnchor = -1;
            }
            handled = true;
            ctx.RaiseEvent(new OnContentChanged());
        }
        else if (key.Key == ConsoleKey.End)
        {
            var oldCaret = _caretIndex;
            _caretIndex = _text.Length;
            if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                if (_selectionAnchor < 0) _selectionAnchor = oldCaret;
                if (_selectionAnchor == _caretIndex) _selectionAnchor = -1;
            }
            else
            {
                _selectionAnchor = -1;
            }
            handled = true;
            ctx.RaiseEvent(new OnContentChanged());
        }
        else if (key.Key == ConsoleKey.A &&
                 (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            if (_text.Length > 0)
            {
                _selectionAnchor = 0;
                _caretIndex = _text.Length;
            }
            else
            {
                _selectionAnchor = -1;
                _caretIndex = 0;
            }

            handled = true;
            ctx.RaiseEvent(new OnContentChanged());
        }
        else if (key.Key == ConsoleKey.C &&
                 (key.Modifiers & ConsoleModifiers.Control) != 0)
        {
            ApplyTextMutation(ctx, () =>
            {
                _text.Clear();
                _caretIndex = 0;
                _selectionAnchor = -1;
            });
            handled = true;
        }
        else if (key.Key == ConsoleKey.Tab)
        {
            ApplyTextMutation(ctx, () =>
            {
                DeleteSelectionIfAny();
                _text.Insert(_caretIndex, '\t');
                _caretIndex++;
                _selectionAnchor = -1;
            });
            handled = true;
        }
        else if (key.Key == ConsoleKey.Enter &&
                 (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0)
        {
            if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
            {
                ApplyTextMutation(ctx, () =>
                {
                    DeleteSelectionIfAny();
                    _text.Insert(_caretIndex, '\n');
                    _caretIndex++;
                    _selectionAnchor = -1;
                });
            }
            else
            {
                var content = _text.ToString();
                ApplyTextMutation(ctx, () =>
                {
                    _text.Clear();
                    _caretIndex = 0;
                    _selectionAnchor = -1;
                });
                ctx.RaiseEvent(new OnContentSubmitted(content));
                ContentSubmitted?.Invoke(content);
            }

            handled = true;
        }
        else if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar) &&
                 (key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0)
        {
            ApplyTextMutation(ctx, () =>
            {
                DeleteSelectionIfAny();
                _text.Insert(_caretIndex, key.KeyChar);
                _caretIndex++;
                _selectionAnchor = -1;
            });
            handled = true;
        }

        if (handled) EnsureCaretVisible(ctx);

        ctx.IsHandled = handled;
    }

    void IEventHandler<PasteInput>.Handle(IEventContext ctx, in PasteInput e)
    {
        var text = e.Text;
        ApplyTextMutation(ctx, () =>
        {
            DeleteSelectionIfAny();
            _text.Insert(_caretIndex, (string?)text);
            _caretIndex += text.Length;
            _selectionAnchor = -1;
        });

        EnsureCaretVisible(ctx);
        ctx.IsHandled = true;
    }

    void IEventHandler<TextInput>.Handle(IEventContext ctx, in TextInput e)
    {
        var text = e.Text;
        ApplyTextMutation(ctx, () =>
        {
            DeleteSelectionIfAny();
            _text.Insert(_caretIndex, (string?)text);
            _caretIndex += text.Length;
            _selectionAnchor = -1;
        });

        EnsureCaretVisible(ctx);
        ctx.IsHandled = true;
    }

    void IEventHandler<OnMouseDown>.Handle(IEventContext ctx, in OnMouseDown e)
    {
        var (originX, originY) = GetAbsoluteOrigin(ctx.LayoutState);
        var localX = Math.Max(0, e.X - originX);
        var localY = Math.Max(0, e.Y - originY);

        _caretIndex = GetCaretIndexFromPoint(ctx.LayoutState, localX, localY);
        _selectionAnchor = _caretIndex;
        _isSelectingWithMouse = true;
        ctx.CaptureMouse();
        ctx.RaiseEvent(new OnContentChanged());
        EnsureCaretVisible(ctx);
        ctx.IsHandled = true;
    }

    void IEventHandler<OnMouseMove>.Handle(IEventContext ctx, in OnMouseMove e)
    {
        if (!_isSelectingWithMouse) return;

        var (originX, originY) = GetAbsoluteOrigin(ctx.LayoutState);
        var localX = Math.Max(0, e.X - originX);
        var localY = Math.Max(0, e.Y - originY);

        var nextCaret = GetCaretIndexFromPoint(ctx.LayoutState, localX, localY);
        if (nextCaret == _caretIndex) return;

        _caretIndex = nextCaret;
        ctx.RaiseEvent(new OnContentChanged());
        EnsureCaretVisible(ctx);
        ctx.IsHandled = true;
    }

    void IEventHandler<OnMouseUp>.Handle(IEventContext ctx, in OnMouseUp e)
    {
        _isSelectingWithMouse = false;
        if (_selectionAnchor == _caretIndex) _selectionAnchor = -1;
        ctx.ReleaseMouse();
    }

    void IEventHandler<OnFocus>.Handle(IEventContext ctx, in OnFocus e)
    {
        if (!AcceptsFocus) return;
        ctx.IsHandled = true;
    }

    public IWidget Parent { get; set; } = SentinelWidget.Instance;

    public IWidgetRenderer GetRenderer() => _scribe;

    public ILayoutCreator GetLayoutCreator() => new TextEditorLayout();

    public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
    {
    }

    public void Accept<TVisitor>(ref TVisitor visitor)
        where TVisitor : struct, IVisitor, allows ref struct
    {
    }

    public IWidgetScribe GetScribe() => _scribe;

    public event Action<string>? ContentSubmitted;

    void EnsureCaretVisible(IEventContext ctx)
    {
        var width = GetLayoutWidth(ctx.LayoutState, 80);
        var currentPos = GetVisualPosition(_text.ToString(), _caretIndex, width);

        if (currentPos != _lastVisualCaretPos)
        {
            ctx.RaiseEvent(
                new Rendering.ScrollIntoViewCommand(new(currentPos.x, currentPos.y, 1, 1), this));
            _lastVisualCaretPos = currentPos;
        }
    }

    bool MoveCaretVertical(IEventContext ctx, int direction)
    {
        var text = _text.ToString();
        var width = GetLayoutWidth(ctx.LayoutState, 80);
        (var currentX, var currentY) = GetVisualPosition(text, _caretIndex, width);
        var targetY = currentY + direction;

        if (targetY < 0)
        {
            _caretIndex = 0;
            return true;
        }

        _caretIndex = WrappedTextLayout.GetCaretIndexFromPoint(text.AsSpan(), currentX, targetY, width);
        return true;
    }

    internal (int x, int y) GetVisualPosition(string text, int caretIndex, int maxWidth)
    {
        return WrappedTextLayout.GetVisualPosition(text.AsSpan(), caretIndex, maxWidth);
    }

    int MoveCaretToPreviousWord()
    {
        var text = _text.ToString();
        return WrappedTextLayout.MoveCaretToPreviousWord(text.AsSpan(), _caretIndex);
    }

    int MoveCaretToNextWord()
    {
        var text = _text.ToString();
        return WrappedTextLayout.MoveCaretToNextWord(text.AsSpan(), _caretIndex);
    }

    void UpdateSelectionForHorizontalNavigation(ConsoleModifiers modifiers, int oldCaret)
    {
        UpdateSelectionForDirectionalNavigation(modifiers, oldCaret);
    }

    void UpdateSelectionForVerticalNavigation(ConsoleModifiers modifiers, int oldCaret)
    {
        UpdateSelectionForDirectionalNavigation(modifiers, oldCaret);
    }

    void UpdateSelectionForDirectionalNavigation(ConsoleModifiers modifiers, int oldCaret)
    {
        var shouldSelect = (modifiers & ConsoleModifiers.Shift) != 0;

        if (!shouldSelect)
        {
            _selectionAnchor = -1;
            return;
        }

        if (_selectionAnchor < 0) _selectionAnchor = oldCaret;
        if (_selectionAnchor == _caretIndex) _selectionAnchor = -1;
    }

    void DeleteSelectionIfAny()
    {
        if (!HasSelection) return;

        var (start, end) = SelectionRange!.Value;
        _text.Remove(start, end - start);
        _caretIndex = start;
        _selectionAnchor = -1;
    }

    void ApplyTextMutation(IEventContext ctx, Action mutation)
    {
        var lineCountBefore = GetLogicalLineCount();
        mutation();
        ctx.RaiseEvent(new OnContentChanged());
        if (GetLogicalLineCount() != lineCountBefore)
            ctx.RaiseEvent(new OnLayoutChanged());
    }

    int GetLogicalLineCount()
    {
        if (_text.Length == 0) return 1;

        var lines = 1;
        for (var i = 0; i < _text.Length; i++)
            if (_text[i] == '\n')
                lines++;

        return lines;
    }

    (int x, int y) GetAbsoluteOrigin(FrameLayoutState layoutState)
    {
        var x = 0;
        var y = 0;
        IWidget current = this;

        while (!ReferenceEquals(current, SentinelWidget.Instance))
        {
            if (layoutState.TryGetRect(current, out var rect))
            {
                x += rect.X;
                y += rect.Y;
            }

            var next = current.Parent;
            if (ReferenceEquals(next, current)) break;
            current = next;
        }

        return (x, y);
    }

    int GetCaretIndexFromPoint(FrameLayoutState layoutState, int x, int y)
    {
        var text = _text.ToString();
        if (text.Length == 0) return 0;
        var maxWidth = Math.Max(1, GetLayoutWidth(layoutState, 80));
        return WrappedTextLayout.GetCaretIndexFromPoint(text.AsSpan(), x, y, maxWidth);
    }

    int GetLayoutWidth(FrameLayoutState layoutState, int fallback)
    {
        if (layoutState.TryGetRect(this, out var rect))
            return rect.Width;

        return fallback;
    }
}
