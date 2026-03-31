using Shouldly;
using System.Diagnostics.CodeAnalysis;
using Thoth;
using Thoth.Internal;
using Thoth.Terminal;
using Thoth.Terminal.Bootstrap;
using Thoth.Tests.utilities;

namespace Comptatata.Tests.App.Cli.UI.Thoth.lifecycle;

public sealed class terminal_runtime_lifecycle_faults
{
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

    static TerminalRuntime CreateFaultingRuntime(dispose_counter session)
    {
        var root = new throwing_render_widget();
        var terminal = new MockTerminal();
        var attention = new AttentionManager(terminal, root);

        return new(attention,
                   terminal,
                   CancellationToken.None,
                   SessionOptions,
                   _ => session.CreateLease());
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000", Justification = "Test asserts faulted runtime behavior; cleanup in finally can rethrow and is intentionally swallowed.")]
    public async Task startup_failure_after_session_activation_disposes_session_once_and_preserves_original_exception()
    {
        var session = new dispose_counter();
        TerminalRuntime? runtime = null;
        try
        {
            runtime = CreateFaultingRuntime(session);
            var ex = await Should.ThrowAsync<InvalidOperationException>(() => runtime.WaitForExitAsync());

            ex.Message.ShouldBe(throwing_render_widget.FailureMessage);
            session.DisposeCount.ShouldBe(1);
        }
        finally
        {
            if (runtime is not null)
            {
                try
                {
                    await ((IAsyncDisposable)runtime).DisposeAsync();
                }
                catch (InvalidOperationException)
                {
                }

                runtime = null;
            }
        }
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000", Justification = "Test asserts precedence behavior on faulted runtime and intentionally swallows follow-up dispose rethrows.")]
    public async Task startup_failure_preserves_original_exception_when_session_dispose_also_fails()
    {
        var session = new dispose_counter(true,
                                          "session-dispose-failure");
        TerminalRuntime? runtime = null;
        try
        {
            runtime = CreateFaultingRuntime(session);
            var ex = await Should.ThrowAsync<InvalidOperationException>(() => runtime.WaitForExitAsync());

            ex.Message.ShouldBe(throwing_render_widget.FailureMessage);
            ex.Data.Contains("thoth.cleanup_exception").ShouldBeTrue();
            ex.Data["thoth.cleanup_exception"].ShouldBeOfType<InvalidOperationException>()
              .Message.ShouldBe(session.CleanupMessage);
            session.DisposeCount.ShouldBe(1);
        }
        finally
        {
            if (runtime is not null)
            {
                try
                {
                    await ((IAsyncDisposable)runtime).DisposeAsync();
                }
                catch (InvalidOperationException)
                {
                }

                runtime = null;
            }
        }
    }

    [Fact]
    [SuppressMessage("Reliability", "CA2000", Justification = "Test asserts idempotency under fault; runtime is disposed in finally with intentional swallow.")]
    public async Task stop_and_dispose_on_faulted_runtime_keep_session_dispose_idempotent()
    {
        var session = new dispose_counter();
        TerminalRuntime? runtime = null;
        try
        {
            runtime = CreateFaultingRuntime(session);
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.StopAsync());
            await Should.ThrowAsync<InvalidOperationException>(() => runtime.StopAsync());
            await Should.ThrowAsync<InvalidOperationException>(() => ((IAsyncDisposable)runtime).DisposeAsync().AsTask());

            session.DisposeCount.ShouldBe(1);
        }
        finally
        {
            if (runtime is not null)
            {
                try
                {
                    await ((IAsyncDisposable)runtime).DisposeAsync();
                }
                catch (InvalidOperationException)
                {
                }

                runtime = null;
            }
        }
    }

    sealed class dispose_counter(bool throwOnDispose = false, string? message = null)
    {
        readonly bool _throwOnDispose = throwOnDispose;

        readonly string _message = message ?? "session-dispose-failure";

        public int DisposeCount { get; private set; }

        public string CleanupMessage => _message;

        public Exception CleanupException => new InvalidOperationException(CleanupMessage);

        public IDisposable CreateLease() => new lease(this);

        sealed class lease(dispose_counter owner) : IDisposable
        {
            public void Dispose()
            {
                owner.DisposeCount++;
                if (owner._throwOnDispose)
                    throw owner.CleanupException;
            }
        }
    }

    sealed class throwing_render_widget : TestWidgetBase
    {
        public const string FailureMessage = "startup-failure-after-session-activation";

        public override void Render(Canvas canvas)
        {
            throw new InvalidOperationException(FailureMessage);
        }
    }
}
