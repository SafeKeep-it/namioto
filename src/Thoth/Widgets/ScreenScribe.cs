using System.Text;
using Thoth.Rendering;
using Thoth.Rendering.Layout;
using Thoth.Widgets.Layout;

namespace Thoth.Widgets;

    public sealed class ScreenScribe : IWidgetRenderer, IWidgetScribe
    {
        readonly Screen _widget;
        RenderContext? _cachedContext;
        int _styleIndex;
        bool _hasStyle;
        Style _cachedStyle;
        Canvas.PreparedGlyph _spaceGlyph;
        bool _hasSpaceGlyph;
        readonly List<Canvas.ChildPlacement> _childPlacements = [];

        public ScreenScribe(Screen widget)
        {
            _widget = widget;
        }

        public Size Measure(SizeConstraint constraint)
        {
            return new(constraint.MaxWidth, constraint.MaxHeight);
        }

        public void Arrange(Rect rect)
        {
            _childPlacements.Clear();
            var children = _widget.Children;
            for (var i = 0; i < children.Count; i++)
            {
                var childRect = new Rect(0, 0, rect.Width, rect.Height);
                children[i].GetRenderer().Arrange(childRect);
                _childPlacements.Add(new(children[i], childRect));
            }
        }

        public void Draw(Canvas canvas)
        {
            EnsureCacheContext(canvas);
            var styleIndex = ResolveStyleIndex(_widget.Style, canvas);
            if (!_hasSpaceGlyph)
            {
                _spaceGlyph = canvas.PrepareRune((Rune)' ');
                _hasSpaceGlyph = true;
            }

            canvas.FillPreparedGlyph(0, 0, canvas.Width, canvas.Height, _spaceGlyph, styleIndex);

            for (var i = 0; i < _childPlacements.Count; i++)
            {
                var placement = _childPlacements[i];
                canvas.RenderChild(_widget, in placement);
            }
        }

        void EnsureCacheContext(Canvas canvas)
        {
            if (ReferenceEquals(_cachedContext, canvas.Context)) return;
            _cachedContext = canvas.Context;
            _hasStyle = false;
            _hasSpaceGlyph = false;
        }

        int ResolveStyleIndex(Style style, Canvas canvas)
        {
            if (!_hasStyle || _cachedStyle != style)
            {
                _cachedStyle = style;
                _styleIndex = canvas.Context.Styles.Intern(style);
                _hasStyle = true;
            }

            return _styleIndex;
        }
    }
