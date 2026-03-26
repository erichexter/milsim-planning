# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

---

## Milestone: v1.1 — Registration

**Shipped:** 2026-03-26
**Phases:** 1 | **Plans:** 2 | **Sessions:** 1

### What Was Built
- POST /api/auth/register backend endpoint with JWT response, validation (400/409), and faction_commander auto-assignment
- 5 integration tests covering all backend ACs (113 total passing, no regressions)
- RegisterPage.tsx with 4-field zod form, client-side password match validation, and auth guard
- LoginPage "Create an account" link and /auth/register public route

### What Worked
- Clear PRD (PRD-REGISTRATION.md) made requirements unambiguous — 12/12 satisfied with no scope drift
- Backend-first plan split (Plan 01 backend, Plan 02 frontend) enabled clean dependency chain
- Following LoginPage patterns exactly for RegisterPage eliminated design decisions
- Audit-first approach: audit ran before complete-milestone, caught notation mismatches before they became blockers

### What Was Inefficient
- REQUIREMENTS.md traceability table was never updated post-execution (showed "Pending" despite all 12 requirements satisfied) — audit surfaced this as documentation drift
- Nyquist VALIDATION.md was created in draft state and never signed off — functional validation was complete but documentation step was skipped
- REG-09 and REG-10 were implemented via plan task descriptions (D-12/D-13) but absent from SUMMARY.md frontmatter — notation mismatch between REQUIREMENTS.md (REG-IDs) and plans (AC-IDs from PRD)

### Patterns Established
- `Error & { status?: number }` type for API error status discrimination — satisfies ESLint no-explicit-any, same runtime behavior
- Auth guard pattern: `useEffect` checks `isAuthenticated`, navigates to `/dashboard` with `replace: true` on public auth pages
- Cross-field zod validation: `.refine()` on schema root with `path: ['confirmPassword']` attaches error to correct field
- Duplicate-email detection via sentinel string "DUPLICATE_EMAIL" in `InvalidOperationException` to distinguish 409 from other Identity errors

### Key Lessons
1. Keep requirement IDs consistent between REQUIREMENTS.md and plan ACs — using two ID systems (REG-xx vs AC-xx) creates traceability drift that requires audit to reconcile
2. Sign off VALIDATION.md during plan execution, not after — leaving it in draft state adds a cleanup step at milestone boundary
3. Small focused milestones (1 phase, 2 plans) can ship in a single day — no planning overhead needed when the PRD is clear

### Cost Observations
- Sessions: 1
- Notable: Single-session milestone — clear PRD + audit-before-complete kept overhead low

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 MVP | 4 | 20 | Initial build — established all core patterns |
| v1.1 Registration | 1 | 2 | First post-MVP milestone — PRD-driven, single day |

### Cumulative Quality

| Milestone | Tests | Zero-Dep Additions |
|-----------|-------|--------------------|
| v1.0 | 108 backend, 60 frontend | All core features |
| v1.1 | 113 backend (+5), 60 frontend | Self-service registration |

### Top Lessons (Verified Across Milestones)

1. Requirement ID consistency matters — use one ID scheme per milestone across all artifacts (REQUIREMENTS.md, plans, SUMMARYs, VERIFICATIONs)
2. Small milestones with clear PRDs ship fast — v1.1 went from start to shipped in one day
