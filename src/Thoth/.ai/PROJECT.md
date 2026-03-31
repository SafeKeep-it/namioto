# Thoth PROJECT

## Purpose

- Terminal UI runtime split into explicit boundaries:
  - raw transport (`ScreenOp`)
  - semantic widget events (`UiEvents` via `AttentionManager` and `EventDispatcher`)

## Boundary Contracts

- `ScreenOp` boundary (`src/dotnet/Thoth/Terminal/Raw/Ingress/ScreenOp.cs`): packed input/commands from ingress to processing.
- `UiEvents` boundary (`src/dotnet/Thoth/Eventing/Events/*.cs`, `Eventing/AttentionManager.cs`, `Eventing/EventDispatcher.cs`): widget-facing events for mouse, focus/hover transitions, text, paste, key, and mouse up/down/click.
- Ownership split:
  - `TerminalSession`: terminal side effects (`raw mode` + ANSI activate/deactivate).
  - `TerminalRuntime`: session lifetime, loops, start/stop/dispose, and failure-path cleanup.
- Capability resolution:
  - `HostEnvironmentProbe` gives deterministic precedence.
  - `TerminalCapabilityResolver` maps host to profile/width-profile/true-color with explicit fallback.

## Startup Path

- `TerminalHostBuilder` gathers content, focus seed, observers, title, cancel handlers.
- `TerminalBootstrapper` probes host, resolves capabilities, creates `TerminalSessionOptions`, binds width provider from `WidthProfile`, builds `AttentionManager`, applies observer registrations.
- `TerminalRuntime` starts and owns loops until stop/dispose.

## Input + Render Path

- `ScreenOpInputLoop` starts raw reader/parser threads.
- `ScreenOpIngressLoop` ingests terminal input and app `Publish` commands.
- `ScreenOpProcessingLoop` drains/coalesces/apply/dispatch/render.
- `ScreenOpApply` maps packed ops to semantic calls (`HandleKey`, `HandleText`, `HandlePaste`, mouse, scroll).
- `AttentionManager` emits semantic events through `EventDispatcher` and renders via `RootConsole`.

## Lifecycle Rules

- `TerminalRuntime` stop is exactly-once guarded.
- Startup/run failures keep original exception as primary.
- Cleanup failures attach to `Exception.Data["thoth.cleanup_exception"]`.
- Frame invariant: `Measure -> Arrange -> Render` every frame.
- `FrameEngine` owns arranged-rect and draw-order writes.
- No visual-tree/layout mutation during `Render`.

## Layout Contract

- `Measure` is declarative under parent max bounds and must be side-effect free.
- `Arrange` is authoritative placement + final-size assignment.
- Container `Measure` should stay simple; expensive precompute is reserved for `TextBlock`/`TextEditor`.
- `Viewport` clips to parent while inner content may grow independently.

## Capability Policy

- Host precedence: `tmux` → `screen` → `WT_SESSION`/`KITTY_WINDOW_ID` → `TERM_PROGRAM` → `TERM` → `unknown`.
- `unknown` fallback: profile `unknown-macos-v0`, width `unicode-default`.
- True-color: forced per selected host rules, otherwise `WantsTrueColor`.

## Current Scope Notes

- Interactive sessions and macOS support are enforced in capability resolution.
- Width-profile wiring is end-to-end: resolver → bootstrapper → provider.
- Arrange ownership is centralized for the production widget set.
- `TextEditor` drag selection remains capture-based (`OnMouseDown` capture → `OnMouseMove` update → `OnMouseUp` release).
