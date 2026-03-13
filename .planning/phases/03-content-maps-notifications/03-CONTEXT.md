# Phase 3: Content, Maps & Notifications - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

A published event contains a complete briefing: commanders can write markdown information sections with file attachments, add map resources (external links + downloadable files), and send email notification blasts to all participants. Async email delivery infrastructure (BackgroundService + Channels) is wired up and producing real emails via Resend.

Creating, reordering, and managing sections is commander-only. Players read sections and download files. Roster change request emails (NOTF-03) are in scope here as the email pipeline is being built. Player-facing UI polish (mobile, callsign prominence) is Phase 4.

</domain>

<decisions>
## Implementation Decisions

### Markdown Editor UX (CONT-01, CONT-02)
- **Toggle view** — Edit tab / Preview tab, switch between them (not split view, not textarea-only)
- **Inline "Add Section" button** — appears at the bottom of the event briefing page; clicking it expands a new section editor in place (no modal, no navigation)
- **Explicit save button** — commander clicks Save when done; no auto-save, no save-on-collapse
- **Collapsed sections show title only** — no content preview, no attachment count badge
- **Plain text title field** above the editor — always visible while editing; save is blocked if title is empty

### Information Section Reordering (CONT-04)
- **Drag-and-drop** with a left-edge grip icon (`⠿` or `≡` handle) on each section card
- **Saves immediately on drop** — no pending state, no confirm button; drag-back to undo
- **DnD library: Claude's discretion** — pick whichever fits best with the existing React/shadcn/ui stack

### File Attachment UX (CONT-03, MAPS-03, MAPS-04, MAPS-05)
- **Inline per-section upload** — each info section and each map resource has its own upload zone; files belong to that section only (no shared event-level file library)
- **Each file requires: the file itself + a friendly display name** (e.g. "Comms Plan v2.pdf" → friendly name "Comms Plan")
- **Direct download** — clicking the file name triggers an immediate browser download via authenticated pre-signed Cloudflare R2 URL (no preview modal, no new tab)
- **Inline error in the upload zone** — file errors (wrong type, too large, network failure) appear as a red message directly under the upload zone where the attempt was made
- **10 MB per file limit** — enforced on both client (before upload) and server (before R2 write); files larger than 10 MB are rejected

### Notification Blast (NOTF-01, NOTF-05)
- **Free-form subject + body** — commander writes their own subject line and message body; no templates
- **Confirmation toast + immediate return** — after Send, a toast appears ("Notification queued, emails sending...") and the UI is immediately usable; no progress indicator, no navigation away
- **Simple send log** — a list of past blasts per event showing: subject, date sent, recipient count; no per-recipient delivery status

### Squad Assignment Change Emails (NOTF-02)
- **Old + new assignment** — email includes both where the player was and where they've been moved to (e.g. "You've been moved from Bravo Squad, 2nd Platoon to Alpha Squad, 1st Platoon")

### Claude's Discretion
- DnD library choice (suggested: @dnd-kit/sortable — React-first, accessible, shadcn/ui compatible)
- Exact markdown editor component (suggested: simple textarea with marked.js or similar for preview rendering — no heavyweight editor like TipTap needed)
- Pre-signed URL expiry duration (suggested: 1 hour — long enough for downloads, short enough for security)
- Notification blast recipient definition ("all event participants" = all EventPlayer records for the event with a non-null UserId)
- Email template styling for squad-change and blast emails

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `web/src/components/ui/accordion.tsx` — already installed; could be used for collapsed section list
- `web/src/components/ui/card.tsx` — section cards use this pattern; shadow/rounded already established
- `web/src/components/ui/dialog.tsx` — available but NOT used for section editing (inline per decisions)
- `web/src/components/ui/input.tsx`, `button.tsx`, `checkbox.tsx` — all available for section forms
- `web/src/components/ui/badge.tsx` — available for file type indicators on attachments
- `web/src/lib/api.ts` — typed API client with Bearer token injection; all new endpoints extend this
- `web/src/hooks/useAuth.ts` — auth state available in all new pages
- `milsim-platform/src/MilsimPlanning.Api/Services/EmailService.cs` — existing `IEmailService.SendAsync`; Phase 3 wires real Resend delivery and adds `BackgroundService` queue on top

### Established Patterns
- **React Router v7 Data mode** — new pages follow loader/action pattern (see EventList, HierarchyBuilder)
- **ScopeGuard.AssertEventAccess** — all new API endpoints use this for IDOR protection
- **`RequireFactionCommander` policy** — write endpoints; `RequirePlayer` policy — read endpoints
- **EF Core upsert pattern** — `FirstOrDefaultAsync` by natural key, then update or add (see RosterService)
- **Testcontainers + WebApplicationFactory** — integration test pattern for all new API tests
- **MSW (Mock Service Worker)** — React component test pattern (see `web/src/mocks/`)
- **BackgroundService + System.Threading.Channels** — stack is locked for async email; not yet implemented, Phase 3 adds it

### Integration Points
- `AppDbContext` — needs new entities: `InfoSection`, `InfoSectionAttachment`, `MapResource`, `NotificationBlast`
- `EventsController` — `DuplicateEventRequest.CopyInfoSectionIds` already wired (Phase 2 forward-compat); Phase 3 implements the actual copy logic
- `web/src/pages/events/EventDetail.tsx` — existing event detail page; Phase 3 adds the briefing content below event metadata
- `web/src/App.tsx` — new routes for section editor, map resources, notification blast

</code_context>

<specifics>
## Specific Ideas

- Event duplication with info sections: Phase 2 already accepts `CopyInfoSectionIds: Guid[]` in `DuplicateEventRequest` — Phase 3 must implement the actual copy logic using this existing contract
- Squad-change email: show both old AND new assignment ("moved from X to Y"), not just the new one
- Notification blast send log is per-event, accessible from the event detail page

</specifics>

<deferred>
## Deferred Ideas

- Per-recipient delivery status (bounces, opens) — not needed in v1; simple log is sufficient
- File preview modal (images/PDFs open before download) — Phase 4 or backlog
- Notification preferences / opt-out — v2 requirement (NOTF-V2-01)
- In-app notification center — v2 requirement (NOTF-V2-02)
- Scheduled/recurring notification blasts — out of scope

</deferred>

---

*Phase: 03-content-maps-notifications*
*Context gathered: 2026-03-13*
