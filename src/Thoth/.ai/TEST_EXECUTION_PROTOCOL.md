# Test and Experiment Execution Protocol

## Purpose

Prevent experiment drift, mixed datasets, and false conclusions.

## Mandatory Rules

- One optimization change per experiment.
- No stacked experiments unless explicitly requested.
- Keep baseline and treatment command lines identical except for the code change.
- Run baseline and treatment each at least 2 times.
- Compare medians, not single-run values.
- Do not mix artifact families in one analysis.
- If canonical and legacy artifact families both exist, legacy must be excluded explicitly in the report.
- Wall-clock ranking always has precedence over hotspot ranking when they diverge.

## Canonical Experiment Loop

1. Build release binary once for baseline.
2. Capture baseline artifacts (run #1 and run #2).
3. Apply exactly one code change.
4. Rebuild release binary.
5. Capture treatment artifacts (run #1 and run #2).
6. Compute median deltas and declare pass/fail.

## Canonical Artifact Families

- Wall-clock ranking:
  - `tmp/per_scenario_bench_output.csv`
- CPU traces:
  - only `tmp/scenario-*-cpu.speedscope.json`
- Live-heap snapshots:
  - `tmp/scenario-*-t5.report.txt`
  - `tmp/scenario-*-t15.report.txt`

Ignore:
- legacy `tmp/scenario-*.speedscope.json` files that do not end with `-cpu.speedscope.json`.

## Interpretation Rules

- Wall-clock is the primary scenario ranking authority.
- CPU hotspot metrics are supplemental and scenario-scoped.
- `gcdump` snapshots represent live heap at sampling points, not allocation throughput.
- Do not infer per-frame allocation churn from `t5/t15` live-heap snapshots.

## Pass/Fail Gate (Default)

Experiment is pass only if all are true:

- median wall-clock improves for targeted scenarios,
- primary target hotspot improves or remains neutral,
- no unacceptable memory regression at t15.

Any failure => mark experiment failed, do not auto-stack next change on top, discuss next step first.

## Report Format

Each experiment report must include:

- hypothesis,
- files changed,
- exact commands,
- baseline medians,
- treatment medians,
- delta table,
- pass/fail verdict,
- artifact paths.
