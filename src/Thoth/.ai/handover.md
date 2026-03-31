# Thoth Handover

## Checkpoint 2026-03-05 (Stress test optimization — P1 through P11)

### Branch and commits

- Branch: `f/test-clenaup`
- HEAD: `fe18febb` (perf(P11): ButtonGroup.EnsureOrdered dirty-flag cache)

| # | Experiment | Commit | Wall Δ | Mem Δ | Verdict |
|---|---|---|---|---|---|
| P1 | GridBuffer grow-only buffer | `fc0a1739` | -4.8% | neutral | KEEP |
| P2 | Canvas DrawUtf8WithStyleIndex byte-span API | `3454407c` | -2.2% | -5.7% | KEEP |
| P3 | DrawTokenLine batch + ASCII fast-path | `606a9c6e` | -0.9% | neutral | KEEP |
| P4 | ButtonGroup.OrderedButtons cache | reverted | +0.3% | n/a | BUST |
| P5 | TextBarRenderer to Canvas UTF-8 APIs | `2f2f4da2` | neutral | neutral | KEEP (arch) |
| P6 | Ellipsis on tokenized path | `96e89201` | -3.5% | -5.3% | KEEP |
| P7 | IAnimatedWidget removal + marquee tokenized | `970d7512` | -3.6% | identical | KEEP |
| P8 | Marquee bugfix (full token span) | `847e265e` | -1.0% | -9.5% | KEEP |
| P9 | EnsureTokenized ContentVersion guard | `2ba03253` | -1.7% | -9.1% | KEEP |
| P10 | AOT build comparison | `cfe45ee2` | +3.4% slower | -9.7% t5 | DATA |
| P11 | ButtonGroup.EnsureOrdered dirty-flag cache | `fe18febb` | -2.7% | -6.1% t15 | KEEP |

### A/B test protocol

Located in `src/dotnet/Thoth/.ai/TEST_EXECUTION_PROTOCOL.md`. Key rules:
- One change per experiment, no stacking.
- Full scenario matrix (all 10 scenarios), full render only.
- Wall-clock 1+ run per scenario, CPU traces via `dotnet-trace` (gc-verbose profile), memory via `dotnet-gcdump` (t5 and t15 snapshots).
- Pass/fail gate: wall improvement on top scenarios + CPU neutral/improved + no t15 regression.
- Dashboard scenario is bimodal due to JIT cold start (255-790ms variance) — always exclude from totals.

### Profiling scripts in `tmp/`

| Script | Purpose |
|---|---|
| `run_ab_treatment.sh` | Unified A/B capture: wall-clock (sequential), then CPU+memory in parallel per scenario. Accepts prefix arg. BIN overridable via env var. |
| `per_scenario_bench.sh` | Wall-clock only capture (process-level timing). |
| `parse_scenario.py` | CPU hotspot extraction from speedscope JSON traces. |
| `parse_mem_reports.py` | Memory type breakdown from gcdump report files. |
| `ab_compare.py` | Single A/B comparison between two treatment prefixes. |
| `build_canonical_scenario_summary.py` | Generates summary from wall CSV + CPU traces + memory reports. |
| `append_ab_timeline.py` | Appends experiment entry to `ab_timeline_entries.json`, regenerates `ab_timeline.html`. Supports `--files` for changed file list with inline SVG previews. `--rebuild-only` to regenerate HTML from existing entries. |

### Baseline and treatment artifacts in `tmp/`

- **D2**: Latest clean JIT baseline (post P3). Wall, CPU, t5/t15 for all 10 scenarios.
- **K**: Post P9 JIT treatment. Wall, CPU, t5/t15 for all 10 scenarios.
- **L**: AOT treatment. Wall, CPU, t5/t15 for all 10 scenarios.
- **M**: Post P11 JIT treatment. Wall, CPU, t5/t15 for all 10 scenarios.
- **A2, B, C, D, E, F, F2, G, H, I, J**: Earlier treatments (some partial, some superseded).
- Timeline report: `tmp/ab_timeline.html` with 11 entries (viewable in browser). Data: `tmp/ab_timeline_entries.json`.

### Architecture changes applied (cumulative)

**Canvas rendering centralization:**
- Canvas owns all rendering: UTF-8 decode, grapheme clustering, glyph interning, token batch rendering.
- New APIs: `DrawUtf8WithStyleIndex`, `DrawUtf8ClippedWithStyleIndex`, `MeasureUtf8Width`, `DrawTokenLine` (batch with ASCII fast-path + boundary-token full-decode), `DrawTokenComfortable` (ASCII byte-to-cell blast).
- Obsolete markers on old Canvas string APIs (`Fill`, `PutGlyph`, `DrawString` overloads taking `Style`).

**TextBlockRenderer fully on tokenized path:**
- All 4 overflow modes (Wrap, Clip, Ellipsis, Marquee) use tokenized pipeline via `Canvas.DrawTokenLine`.
- No longer depends on `TextFlowLayout`. Reduced from 438 lines to 275 lines across P2-P9.
- `DrawMarquee`, `DrawLineSegments`, `MeasuredLines`, `FlowLine` references all removed.
- `EnsureTokenized` uses `widget.ContentVersion` guard (no per-frame string hashing).
- `TextTokenizer.Tokenize` reuses scratch list (zero allocation on repeated calls).

**TextBarRenderer** rewritten to use Canvas UTF-8 APIs (P5).

**GridBuffer** grow-only buffer with `Resize()` method (P1). Shrinks only at less than 25% utilization.

**TextLayout** exposes `List<TextToken>` and `List<TextLine>` directly for zero-copy span access via `CollectionsMarshal.AsSpan`.

**IAnimatedWidget deleted** (P7). `AttentionManager.TickAnimations` uses concrete type pattern match (`Spinner`/`TextBlock`) instead of interface dispatch. `ChatComponent.UpdateAnimation` kept as method but not ticked until future `OnFrameRenderStarted` event architecture.

**AOT build** enabled for StressTest (P10). `CodeAnalysis.csproj` marked `IsAotCompatible=false`.

**ButtonGroup.EnsureOrdered dirty-flag cache** (P11). `_orderedButtons` list reused in-place; rebuilt only when `_buttons` or `DefaultButton` changes. `DefaultButton` promoted to manual property setter to dirty the cache. Zero per-frame allocation for `VisitChildren` and `EnsureMeasured`. Adds `button_group_ordering_contract` tests + SVGs for no-default and default-rightmost scenarios.

### Architecture decisions (locked, user-approved)

1. **TextTokenizer + TextLayout** = fast, byte-level, approximate layout planner. NOT grapheme-perfect.
2. **Canvas** = single authority on rendering. Owns the only grapheme-perfect pass.
3. **TextFlowLayout** = legacy, to be retired. Layout role goes to TextLayout, rendering role goes to Canvas.
4. **Token trust model**: comfortable tokens (fit in line) use ASCII blast or trust estimated width. Boundary tokens (edge of canvas) get full grapheme decode. At most one boundary token per line.
5. **Overflow ownership**: Canvas clips at bounds (free). Ellipsis = layout concern (renderer places U+2026). Marquee = renderer offset.
6. **IAnimatedWidget** = unwanted. Future: `OnFrameRenderStarted` event fires in layout phase, widgets decide internally if tick interval elapsed.
7. **Visitor pattern with type erasure** is intentional (hides children lists).
8. **Canvas should NOT have string-based operations** (legacy, to be removed).
9. **Approximate width OK for text editor** (CJK wrapping one position early is acceptable). Grapheme integrity is non-negotiable.
10. **AOT**: JIT Tier-1 is faster on hot loops. AOT has smaller memory footprint. Both are valid deployment targets.
11. **ButtonGroup spec**: non-default buttons left in insertion order, default button rightmost, gap between all. Keyboard nav cycles. No alignment property. This is the dialog footer button row spec.

### Remaining CPU hotspots (K traces, post P9 — need new M traces for updated picture)

| Scenario | Top hotspot | CPU % |
|---|---|---|
| dense_table | Table.compute_widths | 74.8% |
| toggle_matrix | Table.compute_widths | 71.1% |
| dashboard | TextBarRenderer.Draw | 79.0% |
| text_editor | TextEditorRenderer.EnsureFlow (TextFlowLayout) | 55.7% |
| mixed_controls | ButtonGroup.OrderedButtons (inclusive, work is underneath) | 55.4% |
| overlay_modal_single | OverlayWidget.Arrange | 59.3% |
| overlay_modal_multiple | OverlayWidget.Arrange | 60.4% |
| dock_panel_viewport | DockPanelRenderer.Arrange | 48.0% |
| border_gallery | TextBlockRenderer.Layout | 29.0% |
| text_overflow_gallery | TextBlockRenderer.Layout | 38.6% |

Note: These are K (post-P9) traces. Next traces should use M prefix. ButtonGroup hotspot should be reduced in overlay/mixed_controls scenarios after P11.

### Remaining TextFlowLayout callers

Only `TextEditorRenderer` (full dependency). User said discuss before touching. P8 attempted rewrite to `WrappedTextLayout` but regressed +31% wall-clock (text_editor scenario), was reverted.

### Known items not yet addressed

- **Canvas unused parameters** from previous refactors — user noted, worth investigating.
- **Canvas obsolete string APIs** — 43 warnings in Thoth.Tests (CS0618), tracked but not cleaned up.
- **Table.compute_widths** — dominates 2 of top 3 scenarios. User said skip for now ("will add variety of code later").
- **TextEditorRenderer** — needs different approach than direct TextFlowLayout replacement. Selection rendering complicates migration (width-2 chars need both base + continuation cell selected).
- **SVG test snapshots** — canonical visual test artifacts. Always include updated/created SVGs when changing widgets. New widgets must get SVGs created.
- **New CPU traces needed** — run `bash tmp/run_ab_treatment.sh N` after next change to get fresh hotspot data from M baseline.

### Test status

- 370 passed (368 + 2 new button_group_ordering_contract tests), pre-existing failures unchanged:
  - `clip_overflow_truncates_without_suffix`
  - `layout_change_bubble`
  - 4 `text_tokenizer_apply_edit*` failures (pre-existing on HEAD before P11)
- Build: 0 warnings, 0 errors (Thoth + StressTest).
- Thoth.Tests: 43 CS0618 warnings (tracked obsolete API usage), 0 errors.

### Working tree

Clean. No uncommitted changes.
