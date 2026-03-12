# Phase 2 Context: Commander Workflow

**Phase**: 02-commander-workflow
**Date**: 2026-03-12
**Requirements**: EVNT-01..06, ROST-01..06, HIER-01..06

## Decisions

These are locked. Do not revisit during planning or execution.

### CSV Import Preview (ROST-02, ROST-03)

**Decision**: Errors-only preview (Option B).

Show only rows with errors or warnings. Valid rows are summarized as a count at the top
("N valid, N errors, N warnings"). Errors block commit; warnings allow it. Commander
re-uploads a corrected CSV to clear errors — no in-app row editing.

**Scale note**: Max ~400 players per faction (events are 800 total across 2 factions).
A full-table preview of 400 valid rows adds no value.

---

### Team Affiliation Grouping in Hierarchy Builder (HIER-01..05)

**Decision**: Show affiliation groups as-is from the CSV. No fuzzy matching, no merge UI.

Team Affiliation is free-text entered by individual players — typos and variants are
expected ("Alpha Squad", "Alpha Sqd", "Alpha"). The commander knows their players and
will sort out mismatches manually during squad assignment. The system displays all unique
affiliation strings as groups; players in the wrong bucket just get manually assigned
inline.

**Hierarchy builder UX**: Players are grouped by Team Affiliation. Within each group,
the commander assigns players to a squad via inline editing of a Squad column (click cell
→ pick squad from dropdown). This handles the natural workflow: affiliation ≈ intended
squad, commander corrects exceptions inline.

---

### Event Duplication (EVNT-02)

**Decision**: Commander selects which information sections to carry over at duplication time.

Duplication copies:
- Platoon/squad structure (names and nesting) — always included
- Information sections — commander chooses via checkboxes which sections to copy
  (e.g. comms plan yes, last event's briefing no)

Duplication does NOT copy:
- Player roster or assignments
- Map resources (too event-specific)
- Event dates (cleared, must be re-entered)
- Published status (always resets to Draft)

---

### Roster View (HIER-06)

**Decision**: Grouped by platoon → squad (accordion/tree), with cross-squad search.

Primary use case is "I'm a squad leader looking for players on other squads to
consolidate with." The view must support:
- Browsing the full hierarchy (platoon → squad → players)
- Searching/filtering by name or callsign across all groups
- Callsign displayed prominently (PLAY-06 applies here even though it's Phase 4 req)

---

## Deferred Ideas

Do NOT include these in Phase 2 plans.

- Fuzzy affiliation matching / merge UI — commander sorts it out manually
- In-app CSV row editing — re-upload to fix errors
- Map resource duplication — too event-specific, excluded from duplication
- Any player-facing features (player dashboard, mobile optimization) — Phase 4

## Claude's Discretion

Make reasonable choices for these without blocking:

- Exact shadcn/ui components for the hierarchy builder table (CommandTable, DataTable, etc.)
- Pagination strategy for the roster view (server-side vs client-side — 400 rows is fine client-side)
- Exact column set for the player roster table beyond: Name, Callsign, Team Affiliation, Platoon, Squad
- API endpoint structure for hierarchy mutations (REST vs batch operations)
- Whether event duplication is a modal dialog or a separate page/step

## Scale & Performance Notes

- Max 400 players per faction roster (800-player events are split across 2 factions)
- 400 rows is well within client-side rendering limits — no virtualization needed
- CSV imports will be at most ~400 rows — in-memory validation is fine, no streaming needed
- Background jobs not needed for this phase (those come in Phase 3 for email blasts)
- Invitation emails after CSV import (ROST-06) ARE in scope — use the existing email
  infrastructure from Phase 1 (Resend SDK, hand-rolled invite flow)
