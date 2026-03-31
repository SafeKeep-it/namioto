# Thoth Architecture Map

## Composition

- `TerminalHostBuilder`: app-facing config (content, focus seed, observers, title, cancellation).
- `TerminalBootstrapper`: probes host, resolves capabilities, builds session options/width provider, creates `AttentionManager`, applies observers, creates runtime.
- `TerminalRuntime`: owns ingress/processing loops and lifecycle (`start/stop/wait`, exactly-once stop, cleanup symmetry).
- `TerminalSession`: owns terminal mode side effects (`raw mode`, ANSI init/teardown, ANSI option enablement).
- `ScreenOp*`: raw transport ingress.
- `AttentionManager` + `EventDispatcher`: semantic UI event boundary.
- `FrameEngine`: frame/render orchestration owner.

## Layer Responsibilities

- `TerminalHostBuilder` is orchestration input only; it does not own runtime loops.
- `TerminalBootstrapper` resolves capability state and constructs runtime dependencies.
- `TerminalRuntime` owns both loops:
  - `ScreenOpIngressLoop`
  - `ScreenOpProcessingLoop`
- `TerminalSession` owns terminal I/O side effects.
- Transport pipeline:
  - `ScreenOpInputLoop` starts reader/parser threads.
  - `ScreenOpIngressLoop` ingests terminal input and app `Publish<T>` commands.
  - `ScreenOpProcessingLoop` drains/coalesces/apply ops and dispatches rendering/animation steps.
  - `ScreenOpApply` maps each packed op to semantic handler calls.
- Semantic pipeline:
  - `AttentionManager` receives mapped calls, handles focus/hover/click transitions, and emits `UiEvents`.
  - `EventDispatcher` routes events to widgets.
  - Focus traversal is `Ctrl+Tab` / `Ctrl+Shift+Tab`; focus changes require dynamic `OnFocus` handling.
- Render pipeline:
  - `RootConsole` delegates to `FrameEngine.RenderFrame(...)`.
  - `FrameEngine` owns frame/buffer lifecycle and invalidation expansion.
  - `FrameEngine.ArrangeWidget(...)` is authoritative for geometry writes; production widgets do not write rects.
  - Rendering uses `IFrameDrawStrategy` (`ScribeFrameDrawStrategy` by default).

## Boundary Rules

- Raw transport boundary: `ScreenOp` carries packed input/command payloads.
- Semantic boundary: `UiEvents` for widget interaction (`OnMouseDown/Up/Click`, focus/hover, text/paste/key).
- Lifecycle split: runtime owns orchestration; session owns terminal-mode effects.
- `Measure -> Arrange -> Render` is mandatory each frame; child geometry authority stays with `FrameEngine.ArrangeWidget(...)`.

## Render Cadence Rules

- Canonical cadence semantics are defined in `src/dotnet/Thoth/.ai/RENDER_CADENCE_ARCHITECTURE.md`.
- Controls do not use `ScreenOp*` raw transport for control-to-runtime communication.
- Control communication is command/event based only.
- `OnContentChanged` and render-rate requests are separate signals and must not be conflated.

## Profiling Protocol

- Canonical per-scenario profiling method is defined in `src/dotnet/Thoth/.ai/SCENARIO_PROFILING_PROTOCOL.md`.
- Test and A/B execution guardrails are defined in `src/dotnet/Thoth/.ai/TEST_EXECUTION_PROTOCOL.md`.

## Capability Policy

- Host precedence: `tmux` → `screen` → dedicated markers (`WT_SESSION`, `KITTY_WINDOW_ID`) → `TERM_PROGRAM` → `TERM` → unknown.
- Unknown fallback: profile `unknown-macos-v0`, width profile `unicode-default`.
- True-color follows forced host rules, else `WantsTrueColor`.
