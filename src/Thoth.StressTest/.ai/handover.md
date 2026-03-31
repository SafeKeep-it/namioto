# Thoth StressTest Optimization — Handover

**Branch**: `f/test-clenaup`
**HEAD**: `86618306` (Optimize renderer style and glyph caching — O4)
**Date**: 2026-03-06
**Status**: P12 experiments O1–O4 rebased onto branch. Build fix uncommitted. Needs clean measurement before timeline update.

---

## What This Project Is

`Thoth` is a terminal UI rendering library (TUI). `Thoth.StressTest` is a native AOT benchmark that runs 12 rendering scenarios and measures wall-clock time, CPU hotspots, and heap allocations. The goal is to make the renderer faster — particularly the hot rendering paths that dominate CPU time.

The project is a C# / .NET 10 solution. AOT + full trim is the **primary deployment target**. JIT Release builds are optional reference only.

---

## Three Projects — Never Confuse

- **Thoth** — rendering library, built Release
- **Thoth.Tests** — unit + SVG snapshot tests. Run against Release library. **Gate only: pass/fail.** Test execution time is irrelevant.
- **Thoth.StressTest** — AOT executable. Published with `dotnet publish -r osx-arm64`. Native binary is what gets measured for performance.

---

## Current Commit Chain (f/test-clenaup)

```
d449e2d2  build: add OptimizationPreference=Speed for AOT native codegen
c0aead16  test(stress): replace scenarios 11-12 with settings_panel and split_workspace
90e8cc2e  fix: resolve 3 pre-existing test failures (tokenizer aliasing, ellipsis guard, event targeting)
ea2116bc  test: add direct textlayout unit coverage (9 tests)
332b5132  perf(O1): cache TextBarRenderer title encoding
1ece3934  perf(O2): optimize text reflow for clip and cache style resolution
84287f0b  perf(O3): reduce dirty-frame layout/render allocations
86618306  perf(O4): optimize renderer style and glyph caching  ← HEAD
```

---

## Uncommitted Changes (CRITICAL)

- `src/dotnet/Thoth/Rendering/FrameEngine.cs` — **O3 build fix**: added `_renderInvalidationBuffer` field + passed as 5th arg to `FrameLayoutPlanner.Decide`. Without this, the build fails.
- `src/dotnet/Thoth.Tests/widgets/modal_dialog/modal_dialog.layout.overflow.svg` — modified (may need review)
- `src/dotnet/Thoth.Tests/widgets/text_block/text_block_overflow.clip.svg` — modified (likely from ellipsis guard bugfix changing rendering)
- `src/dotnet/Thoth.StressTest/.ai/EXPERIMENT_PIPELINE.md` — new protocol document
- `rebase_experiments.sh` — disposable helper script
- `.sisyphus/` — evidence files from experiment agents, disposable

**First action for next session**: commit the FrameEngine.cs fix + review SVGs + commit. Without this, the combined O1-O4 code does not build.

---

## Stale Worktrees (Cleanup Needed)

10 worktrees exist in /private/tmp:
- `/private/tmp/comptatata_O1` through O4 — old renderer experiments (previous session)
- `/private/tmp/comptatata_renderer_canvas` — old audit worktree
- `/private/tmp/thoth-O1` through O4 — current O1-O4 experiment worktrees

Cleanup: `git worktree remove <path>` for each, then `git worktree prune`.

Experiment branches still exist: `experiment/O1`, `experiment/O2`, `experiment/O3`, `experiment/O4`. Can be deleted after worktree cleanup.

---

## 12 Scenarios (All Active)

| # | ID | Tree Summary |
|---|---|---|
| 1 | dashboard | Border(Rounded)→StackPanel→[TextBar,TextBlock,ProgressBar] |
| 2 | overlay_modal_multiple | OverlayWidget→[TextBlock,ModalDialog→StackPanel→[TextBlock,MultipleChoiceList]] |
| 3 | overlay_modal_single | OverlayWidget→[TextBlock,ModalDialog→StackPanel→[TextBlock,SingleChoiceList]] |
| 4 | dense_table | Viewport→Table(24×3)→[TextBlock,TextBlock,ProgressBar] |
| 5 | mixed_controls | Border(Single)→StackPanel→[Spinner(Kit),ButtonGroup→Button×2,TextBlock] |
| 6 | text_editor | Border(Outline)→StackPanel→[TextBar,TextEditor,ProgressBar] |
| 7 | dock_panel_viewport | DockPanel→[Top:TextBar,Bottom:ProgressBar,Fill:Viewport→StackPanel(16 rows)] |
| 8 | toggle_matrix | Viewport→Table(18×3)→[Toggle,TextBlock,TextBlock] |
| 9 | border_gallery | StackPanel→4×Border(styles)→TextBlock |
| 10 | text_overflow_gallery | Border(Rounded)→StackPanel→[TextBlock(Wrap/Clip/Ellipsis),Spinner(Braille)] |
| 11 | settings_panel | DockPanel→[TextBar,Align(Right)→ButtonGroup,Viewport→StackPanel→3×Border→StackPanel] |
| 12 | split_workspace | DockPanel→[TextBar,ProgressBar,Table(2col)→[sidebar StackPanel, DockPanel→Viewport→Table(8×4)]] |

Scenarios 11-12 replaced in this session. Coverage gaps filled: Align widget, standalone SingleChoiceList, nested StackPanels, TextEditor-in-Table, Table-based split panel, ButtonGroup-in-DockPanel.

---

## N Baseline (AOT, Pre-P12, All 12 Scenarios)

Captured with agents running — numbers are noisy. Use as directional reference only.

- dashboard: 293.3ms
- dense_table: 189.9ms
- toggle_matrix: 133.6ms
- dock_panel_viewport: 116.7ms
- settings_panel: 111.3ms
- overlay_modal_multiple: 106.4ms
- overlay_modal_single: 98.6ms
- text_editor: 79.8ms
- text_overflow_gallery: 79.2ms
- mixed_controls: 76.8ms
- split_workspace: 65.3ms
- border_gallery: 61.4ms
- **Total: ~1,412ms**

CSV: `tmp/per_scenario_bench_output.P11.csv` (all 12 scenarios)
Old N CSV: `tmp/per_scenario_bench_output.N.csv` (only 10 scenarios — pre-scenario-replacement)

---

## O1–O4 Experiments (All Rebased, Need Clean Measurement)

### O1: TextBarRenderer — cache title encoding
- Commit: `332b5132`
- Files: TextBarRenderer.cs
- Change: Cached UTF-8 title bytes + measured widths in mad_state. Replaced ProportionalTableLayout with inline equal-thirds arithmetic.
- Target: dashboard (79% TextBarRenderer.Draw), dock_panel_viewport

### O2: TextBlockRenderer — Clip short-circuit + style cache
- Commit: `1ece3934`
- Files: TextBlockRenderer.cs
- Change: Clip-mode Reflow hard-limited to one line. Style index caching decoupled from ContentVersion using style signature.
- Target: border_gallery, text_overflow_gallery (29-38% TextBlockRenderer.Layout)

### O3: FrameLayoutState — allocation reduction
- Commit: `84287f0b`
- Files: FrameLayoutPlanner.cs, RenderContext.cs, ScribeFrameDrawStrategy.cs, RootConsole.cs
- Change: Reusable invalidation buffer, non-allocating LINQ replacement, reusable RenderContext.
- Target: ALL scenarios (per-frame allocation reduction)
- **NOTE**: Requires uncommitted FrameEngine.cs fix to build

### O4: Border/ProgressBar/Toggle — style/glyph caching
- Commit: `86618306`
- Files: BorderRenderer.cs, ProgressBar.cs, Toggle.cs
- Change: Cached style interning + PrepareRune results in mad_state.
- Target: most scenarios
- **WARNING**: Showed dashboard regression +41% in isolated test (414ms vs 293ms baseline). May have been measurement noise. Needs clean re-measurement.

### Individual Experiment Numbers (Noisy — All Agents Running Simultaneously)

O1 (ms): dashboard:57, overlay_modal_multiple:88, overlay_modal_single:80, dense_table:175, mixed_controls:61, text_editor:63, dock_panel_viewport:100, toggle_matrix:117, border_gallery:46, text_overflow_gallery:65, settings_panel:92, split_workspace:46

O2 (ms): dashboard:70, overlay_modal_multiple:86, overlay_modal_single:77, dense_table:161, mixed_controls:59, text_editor:61, dock_panel_viewport:93, toggle_matrix:109, border_gallery:43, text_overflow_gallery:61, settings_panel:86, split_workspace:43

O3 (ms): dashboard:50, overlay_modal_multiple:80, overlay_modal_single:70, dense_table:170, mixed_controls:60, text_editor:60, dock_panel_viewport:90, toggle_matrix:110, border_gallery:40, text_overflow_gallery:60, settings_panel:80, split_workspace:40

O4 (ms): dashboard:414, overlay_modal_multiple:82, overlay_modal_single:75, dense_table:160, mixed_controls:56, text_editor:59, dock_panel_viewport:96, toggle_matrix:111, border_gallery:40, text_overflow_gallery:60, settings_panel:84, split_workspace:42

**Caveat**: Broad improvements in non-target scenarios suggest N baseline captured under heavy CPU load. Relative inter-experiment differences more reliable than absolute deltas. Clean P11-vs-P12 measurement needed.

---

## Pending Actions (Priority Order)

1. **Commit FrameEngine.cs fix** + review/commit modified SVGs — build is broken without this
2. **Clean worktrees** — remove 10 stale worktrees, delete experiment branches
3. **Build + test** — verify 381+ pass, 0 fail on combined code
4. **AOT publish** — `dotnet publish src/dotnet/Thoth.StressTest/Thoth.StressTest.csproj -c Release -r osx-arm64`
5. **Clean measurement** — time all 12 scenarios with NO background load, create `tmp/per_scenario_bench_output.O.csv`
6. **Update timeline** — `python3 tmp/append_ab_timeline.py --title "P12: O1-O4 combined" --baseline-csv tmp/per_scenario_bench_output.P11.csv --treatment-csv tmp/per_scenario_bench_output.O.csv ...`
7. **Evaluate O4 dashboard regression** — if confirmed in clean measurement, may need to revert O4

---

## Bugfixes Applied This Session

### 1. TextTokenizer aliasing (4 tests)
`Tokenize()` returned reference to internal `_scratchTokens` list. Fixed: returns snapshot copy `new List<TextToken>(_scratchTokens)`.

### 2. Ellipsis on Clip overflow (1 test)
`TextBlockRenderer.DrawTokenizedLines` drew ellipsis regardless of overflow mode. Fixed: added `widget.Overflow == TextOverflow.Ellipsis` guard.

### 3. EventContext.RaiseEvent targeting (1 test)
`RaiseEvent` used `_target` instead of `_currentWidget`. Fixed: changed to `_currentWidget`.

All in commit `90e8cc2e`. Result: 372 → 381 tests (6 fixed + 9 new TextLayout tests), 0 failures.

---

## TextLayout Test Coverage (New)

9 direct tests added in `ea2116bc` under `src/dotnet/Thoth.Tests/text/layout/`:
- Initialize (copy + reinit)
- ApplyTokenDelta (splice)
- Reflow (width wrap, newline break, maxLines limit, long-token-on-line, empty input)
- EnumerateLine (cumulative X positions)

---

## Hotspot Data (K Traces — STALE, Post-P9 JIT)

These need fresh measurement. Do not rely on exact percentages.

- dashboard: TextBarRenderer.Draw 79.0%
- dock_panel_viewport: DockPanelRenderer.Arrange 48.3%
- overlay_modal_multiple: OverlayWidget.Arrange 60.4%
- overlay_modal_single: OverlayWidget.Arrange 59.3%
- text_editor: TextEditorRenderer.EnsureFlow 55.7% (DISCUSS FIRST)
- mixed_controls: ButtonGroup.OrderedButtons 55.4% (likely fixed by P11)
- dense_table: Table.compute_widths 74.8% (SKIP)
- toggle_matrix: Table.compute_widths 71.1% (SKIP)
- border_gallery: TextBlockRenderer.Layout 29.0%
- text_overflow_gallery: TextBlockRenderer.Layout 38.6%
- settings_panel: NEW — no traces
- split_workspace: NEW — no traces

---

## A/B Workflow

### Measurement Commands
```bash
# AOT publish:
dotnet publish src/dotnet/Thoth.StressTest/Thoth.StressTest.csproj -c Release -r osx-arm64

# Binary location:
BIN=src/dotnet/.artifacts/publish/Thoth.StressTest/release_osx-arm64/Comptatata.Thoth.StressTest

# Wall-clock per scenario (time externally):
time $BIN --iterations 2 --frames 300 --warmup-iterations 1 --warmup-frames 100 --scenarios <name>

# CPU traces:
$BIN --iterations 30 --frames 300 &
dotnet-trace collect -p $PID --profile gc-verbose --duration 00:00:08 --format Speedscope

# Memory:
$BIN --iterations 300 --frames 300 &
dotnet-gcdump collect -p $PID  # at t=5s and t=15s
dotnet-gcdump report <file>
```

### Timeline Script
```bash
python3 tmp/append_ab_timeline.py \
  --title "P12: <description>" \
  --verdict keep \
  --summary "<sentence>" \
  --commit $(git rev-parse HEAD) \
  --baseline-csv tmp/per_scenario_bench_output.P11.csv \
  --treatment-csv tmp/per_scenario_bench_output.O.csv \
  --files <changed-files>
```

SCENARIO_ORDER in `append_ab_timeline.py` updated to include all 12 scenarios.

---

## Experiment Timeline (P1–P11 + N)

| # | Title | Verdict | Wall Δ | Mem Δ |
|---|---|---|---|---|
| P1 | GridBuffer grow-only buffer | KEEP | -3.8% | -20.8% |
| P2 | Canvas DrawUtf8WithStyleIndex byte-path | KEEP | -2.2% | -5.7% |
| P3 | DrawTokenLine + ASCII fast-path | KEEP | -0.9% | -0.5% |
| P4 | ButtonGroup.OrderedButtons caching | BUST | +0.3% | — |
| P5 | TextBarRenderer → Canvas UTF-8 APIs | KEEP (arch) | neutral | neutral |
| P6 | Ellipsis on tokenized path | KEEP | -3.5% | -5.3% |
| P7 | IAnimatedWidget removal + marquee tokenized | KEEP | -3.6% | -5.7% |
| P8 | Marquee bugfix (tokenized single-line draw) | KEEP | -1.0% | -9.5% |
| P9 | EnsureTokenized ContentVersion guard | KEEP | -1.7% | -9.1% |
| P10 | AOT vs JIT comparison | DATA | +3.4% | -9.7% |
| P11 | ButtonGroup EnsureOrdered dirty-flag | KEEP | -2.7% | -6.1% |
| N | AOT+Speed native binary | KEEP | -65–73% | -6.1% |
| P12 | O1-O4 combined | PENDING | — | — |

**Next prefix**: O (used for current experiments). Next available: P

---

## Architecture Decisions (Authoritative)

1. Canvas owns all rendering — UTF-8 decode, glyph measurement, cell painting
2. No IAnimatedWidget — removed (P7). AttentionManager uses concrete types.
3. TextFlowLayout being retired — tokenized path is standard
4. ButtonGroup spec: non-default left insertion order, default rightmost, gap between all
5. AOT + full trim primary target
6. No backward compatibility — internal library
7. Widget isolation: widgets must not know about each other
8. Layout invariant: Measure → Arrange → Draw every frame. No layout changes once rendering starts.
9. DockPosition: Top, Bottom, Fill only (no Left/Right)
10. StackPanel: vertical only (no horizontal)
11. Rebase only — never merge/squash

---

## Hardware

- Apple M4 Max, 12 P-cores, 4 E-cores, 128 GB unified memory
- `dotnet-trace`, `dotnet-gcdump` installed globally
- macOS, fast NVMe
