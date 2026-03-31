# Thoth Render Cadence Architecture

## Canonical Boundaries

- `ScreenOp*` is for external input translation only.
- Controls do not communicate through raw transport messages.
- Control communication stays command/event driven.
- `AttentionManager` requires a dedicated rewrite; do not modify it in this phase.

## Cadence Intent

- Rendering cadence must adapt to control demand.
- Controls request desired frames-per-second through events.
- Scheduler policy aggregates requests and uses the maximum requested FPS.
- If no control requests updates and no content changes exist, cadence can drop to near-idle.

## Current Policy Targets

- Spinner class updates: 6-12 FPS.
- Resize responsiveness: 30 FPS.
- High-responsiveness class: 60 FPS.

These are policy targets, not a replacement for invalidation rules.

## Command Contract (Current Phase)

- Start/stop animation behavior is command-based.
- `IHandleCommand<T>` receives `ICommandContext` as first parameter.
- `ICommandContext` allows `RaiseEvent(...)` so command handlers can bubble events.

Spinner command behavior in current phase:
- Start command raises:
  - `OnContentChanged`
  - `OnRenderRateRequested(clampedSpinnerFps)`
- Stop command raises:
  - `OnContentChanged`
  - `OnRenderRateRequested(0)`
- Stop command also clears spinner visual output.

## Separation of Concerns

- `OnContentChanged` drives invalidation/render semantics.
- Render-rate request events express desired cadence only.
- Do not encode cadence requests inside content-change semantics.

## Deferred Work

- Frame-start broadcast/event plumbing across the widget tree.
- Final scheduler integration for adaptive cadence.
- Full `AttentionManager` rewrite.
