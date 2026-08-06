# Production

## What This Area Is

The pipeline. **One slice at a time** moves through six stages, and the filename says where it is.
Upstream: all four domain workspaces. Downstream: a shipped, documented slice.

```
01-requirements → 02-design → 03-architecture → 04-build → 05-verify → 06-release
                                     ▲                          │
                                     └── loop back on a failed criterion
```

Stages 01–03 are usually authored by the domain workspaces and handed in. Stages 04–06 happen here.

## What to Load

| Task | Load These | Skip These |
|------|-----------|------------|
| Implement (04) | `03-architecture/[slug]-architecture.md`, `../docs/tech-standards.md`, `../docs/conventions.md` | `01-requirements/*` — the architecture note supersedes it |
| Verify (05) | `02-design/[slug]-design.md` (criteria), `03-architecture/*`, `04-build/[slug]/**` | `../presentation/docs/art-direction.md`, all `../docs/` except conventions |
| Release (06) | `05-verify/[slug]-report.md`, `02-design/[slug]-design.md` | `01-requirements/*`, build internals |
| Check slice state | nothing — read the folder names | any file at all |

## The Process

1. **04 Build.** Implement against the architecture note. Record each non-obvious decision in
   `04-build/[slug]/build-notes.md` *as you make it*, not in a retrospective sweep.
2. Build must be green (`dotnet build`, 0 warnings) before advancing.
3. **05 Verify.** Run every acceptance criterion, plus the determinism trace diff if Core changed.
   Report each criterion as PASS / FAIL / NOT-VERIFIABLE-BY-AGENT with the evidence.
4. On any FAIL, **branch resolution**: name exactly one stage to loop back to and why —
   02 if the design was wrong, 03 if the architecture was wrong, 04 if the build was wrong.
5. **06 Release.** Only when every criterion is PASS or explicitly waived by the human. Write the
   release note, stamp `v1`.

## Skills & Tools

| Skill / Tool | When (trigger) | Purpose |
|--------------|----------------|---------|
| `dotnet build` / `dotnet test` | Before every 04 → 05 advance | Compile and unit gate |
| `Gridfall.Verify` | Stage 05, whenever Core changed | Tick-by-tick trace diff against the baseline |
| `advance_stage` (MCP) | Moving between stages | Renames to the next stage's pattern; it is a **move**, not a copy |
| `version_file` (MCP) | Before an advance you want to preserve | Keeps the previous artifact as `v[n]` |

## What NOT to Do

- Don't carry two slices at once. Finish or park one first.
- Don't load the requirements while implementing — drift between it and the spec is exactly what the
  skip rule prevents.
- Don't mark a criterion PASS because the code "looks right". Run it or mark it NOT-VERIFIABLE-BY-AGENT.
- Don't advance to 06 with an open FAIL.
