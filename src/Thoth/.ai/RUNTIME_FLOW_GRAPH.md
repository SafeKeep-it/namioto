# Thoth Runtime Flow

## Startup Flow

- `TerminalHostBuilder.StartAsync()` validates config.
- `TerminalBootstrapper.Start(ct)` resolves host/capabilities/width profile, creates `TerminalSessionOptions`, constructs `AttentionManager`, applies observers, and creates `TerminalRuntime`.
- `TerminalRuntime` acquires `TerminalSession` lease and starts its run task.

## Input & Command Flow

- `ScreenOpInputLoop` starts raw reader/parser threads.
- Parsed terminal input + app `Publish<T>` commands enter `ScreenOpIngressLoop`.
- `ScreenOpProcessingLoop` drains/coalesces batches.
- `ScreenOpApply` maps each op to semantic `AttentionManager` calls.
- `AttentionManager` emits `UiEvents`; `EventDispatcher` routes to widgets.

## Render Flow

- Render is triggered by input ops, queued commands, terminal size changes, or animation ticks.
- `AttentionManager.Render` first processes queued events, then calls `RootConsole.Render(...)`.
- `RootConsole` calls `FrameEngine.RenderFrame(...)`.
- `FrameEngine` chooses full/partial draw, runs layout when needed, and expands paint invalidations to ancestors.
- `FrameEngine` writes authoritative arranged rects and draw order before widget arrange calls.
- Draw uses `IFrameDrawStrategy` (`ScribeFrameDrawStrategy`) via widget scribes (`GetScribe().Draw(...)`).
- `TerminalScribe` flushes resulting `GridBuffer` to terminal.

## Lifecycle & Failure

- `TerminalRuntime` owns start/stop/wait/dispose with exactly-once stop semantics.
- `TerminalSession` owns raw mode and ANSI lifecycle.
- On startup/run failure: original exception remains primary; cleanup failures are attached as `Exception.Data["thoth.cleanup_exception"]`.

## Capability Policy

- Host precedence: `tmux` → `screen` → (`WT_SESSION`, `KITTY_WINDOW_ID`) → `TERM_PROGRAM` → `TERM` → unknown.
- Unknown fallback: profile `unknown-macos-v0`, width profile `unicode-default`.

## Arrange Ownership

- Widgets may keep local arrange state only.
- Authoritative geometry/index ownership remains in `FrameEngine`.
