using Shouldly;
using System.Collections.Concurrent;
using Thoth;
using Thoth.Eventing;
using Thoth.Eventing.Events;
using Thoth.Internal;
using Thoth.Navigation.Focus;
using Thoth.Rendering;
using Thoth.Terminal;
using Thoth.Terminal.Bootstrap;
using Thoth.Widgets;

namespace Comptatata.Tests.App.Cli.UI.Thoth.lifecycle;

public sealed class initial_layout_and_render_precede_input_processing : IAsyncLifetime
{
    readonly startup_probe_widget _probe = new();
    readonly startup_probe_terminal _terminal = new();

    TerminalRuntime? _runtime;

    static TerminalSessionOptions SessionOptions => new(_ => { },
                                                         null,
                                                         new(false,
                                                             false,
                                                             true,
                                                             false,
                                                             false,
                                                             false,
                                                             true,
                                                             false,
                                                             false,
                                                             false,
                                                             null,
                                                             null,
                                                             null,
                                                             null,
                                                             TerminalHostKind.Unknown),
                                                         new("test",
                                                             256,
                                                             false,
                                                             false,
                                                             false,
                                                             false,
                                                             false,
                                                             false,
                                                             false,
                                                             "default"));

    public async Task InitializeAsync()
    {
        var root = new Screen();
        root.Add(_probe);

        var attention = new AttentionManager(_terminal, root, _probe);
        _runtime = new(attention,
                       _terminal,
                       CancellationToken.None,
                       SessionOptions,
                       _ => new no_op_session());

        _terminal.QueueInput((byte)'a');

        await _probe.KeyHandled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    public async Task DisposeAsync()
    {
        if (_runtime is null) return;
        await _runtime.StopAsync();
        await ((IAsyncDisposable)_runtime).DisposeAsync();
        _runtime = null;
    }

    [Fact]
    public void key_processing_observes_arranged_rect()
    {
        _probe.HadArrangedRectWhenKeyHandled.ShouldBeTrue();
    }

    [Fact]
    public void key_processing_observes_completed_render()
    {
        _probe.HadRenderWhenKeyHandled.ShouldBeTrue();
    }

    sealed class no_op_session : IDisposable
    {
        public void Dispose()
        {
        }
    }

    sealed class startup_probe_terminal : ITerminal
    {
        readonly ConcurrentQueue<byte> _input = new();

        public TerminalKind Kind => TerminalKind.Visual;
        public int WindowWidth => 80;
        public int WindowHeight => 24;

        public int ReadRawInput(Span<byte> buffer)
        {
            if (!_input.TryDequeue(out var value)) return 0;
            buffer[0] = value;
            return 1;
        }

        public void WriteRaw(ReadOnlySpan<byte> buffer)
        {
        }

        public void SetClipboard(string text)
        {
        }

        public void QueueInput(byte value) => _input.Enqueue(value);
    }

    sealed class startup_probe_widget : IWidget, IAutoFocusable, IEventHandler<KeyPressedInput>
    {
        readonly IWidgetScribe _scribe;
        readonly IWidgetRenderer _renderer;
        int _renderCount;
        Rect? _arrangedRect;

        public startup_probe_widget()
        {
            _renderer = new startup_probe_renderer(this);
            _scribe = new probe_scribe(this);
        }

        public bool HadArrangedRectWhenKeyHandled { get; private set; }
        public bool HadRenderWhenKeyHandled { get; private set; }
        public TaskCompletionSource<bool> KeyHandled { get; } = new();

        public IWidget Parent { get; set; } = SentinelWidget.Instance;

        public IWidgetRenderer GetRenderer() => _renderer;

        public Size Measure(SizeConstraint constraint) => new(1, 1);

        public void Arrange(Rect rect)
        {
            _arrangedRect = rect;
        }

        public IWidgetScribe GetScribe() => _scribe;

        public void VisitChildren<TVisitor>(ref TVisitor visitor) where TVisitor : struct, IChildVisitor
        {
        }

        public IWidget? HitTest(int x, int y)
        {
            if (_arrangedRect is not { } rect) return null;
            return x >= rect.X && x < rect.X + rect.Width &&
                   y >= rect.Y && y < rect.Y + rect.Height
                ? this
                : null;
        }

        public void Handle(IEventContext ctx, in KeyPressedInput e)
        {
            HadArrangedRectWhenKeyHandled = _arrangedRect.HasValue;
            HadRenderWhenKeyHandled = _renderCount > 0;
            KeyHandled.TrySetResult(true);
            ctx.IsHandled = true;
        }

        sealed class probe_scribe(startup_probe_widget owner) : IWidgetScribe
        {
            public void Draw(Canvas canvas)
            {
                owner._renderCount++;
            }
        }

        sealed class startup_probe_renderer(startup_probe_widget owner) : IWidgetRenderer
        {
            public Size Measure(SizeConstraint constraint) => new(1, 1);

            public void Arrange(Rect rect)
            {
                owner.Arrange(rect);
            }

            public void Draw(Canvas canvas)
            {
                owner._scribe.Draw(canvas);
            }
        }
    }
}
