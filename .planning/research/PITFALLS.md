# Pitfalls Research

**Domain:** Event management / roster management web app (airsoft/milsim)
**Researched:** 2026-03-12
**Confidence:** HIGH (OWASP official docs, Postmark official docs, web.dev official docs; domain-specific analysis)

---

## Critical Pitfalls

### Pitfall 1: Authorization Checks Only at the Route Level

**What goes wrong:**
Permissions are checked when a user accesses a page or API endpoint, but individual database rows are not verified to belong to the requester. A Platoon Leader in Event A can fetch data from Event B by guessing IDs. A Player can read another player's roster change request by tweaking a URL parameter.

**Why it happens:**
Developers implement middleware that verifies role (e.g., "is this user a Faction Commander?") but stop there. They don't verify that the specific resource being accessed belongs to the event/org the user is permitted to see. This is OWASP's #1 vulnerability category (Broken Access Control).

**How to avoid:**
Every data query must include an ownership/scope filter. Never fetch a record by ID alone — always scope queries: `WHERE id = ? AND event_id = ? AND event.owner_id = ?`. Add object-level authorization as a layer separate from role checks. Write integration tests that prove a user in Event A cannot access data in Event B.

**Warning signs:**
- API routes accept raw record IDs without scoping to the authenticated user's event/org
- Authorization logic lives only in middleware, not in service/query layer
- No test coverage for "access record belonging to a different event"
- Sequential or predictable IDs (integers) on player/assignment records

**Phase to address:** Authentication & authorization phase (foundational — must be correct before any other feature is built on top)

---

### Pitfall 2: Role Hierarchy Implemented as Simple String Comparison

**What goes wrong:**
Roles are stored as strings (`"squad_leader"`, `"platoon_leader"`, etc.) and permission checks are scattered `if (user.role === 'faction_commander')` calls throughout the codebase. Adding a new role requires hunting down every permission check. Hierarchical permission inheritance (a Platoon Leader can do everything a Squad Leader can) is manually re-implemented in dozens of places and inevitably goes out of sync.

**Why it happens:**
It's the simplest thing that works during early development. The hierarchy only has 5 levels and seems manageable. But any change to hierarchy later (e.g., adding a "Co-Commander" role) requires touching the entire codebase.

**How to avoid:**
Define a single canonical permission matrix at app startup. Encode hierarchy explicitly: a role inherits all permissions of roles below it. All permission checks go through one `can(user, action, resource)` function — never raw role string comparisons in business logic. This can be as simple as an ordered array + a permissions map; it does not require a full ABAC framework.

```typescript
// Example: centralized permission resolution
const ROLE_HIERARCHY = ['player', 'squad_leader', 'platoon_leader', 'faction_commander', 'system_admin'];
const can = (user, action, resource) => permissionMatrix[user.role]?.[action]?.(user, resource) ?? false;
```

**Warning signs:**
- `if (role === 'faction_commander' || role === 'system_admin')` patterns repeated across the codebase
- Permission logic in route handlers, not in a dedicated authorization module
- No test that proves role X inherits permissions of role Y

**Phase to address:** Authentication & authorization phase

---

### Pitfall 3: Bulk Email Sent Synchronously in the Request/Response Cycle

**What goes wrong:**
Commander clicks "Publish Event." The server loops through 800 player emails, calls the email provider API 800 times in a for-loop, and the HTTP request times out after 30 seconds. The commander sees an error, clicks again, and players receive duplicate notifications. Or only 200 of 800 emails are sent before the timeout.

**Why it happens:**
Email sending is added inline as "just one more step" in the publish endpoint. Works fine in development with 5 test users.

**How to avoid:**
All bulk email operations must be queued and processed asynchronously. On "Publish Event," enqueue a job. A background worker processes the queue and sends emails in batches (e.g., 50 at a time) with retry logic. The UI shows a "notification delivery in progress" status, not a spinner. Use the email provider's batch send API (SendGrid's `v3/mail/send` supports up to 1000 recipients per call with personalizations).

**Warning signs:**
- Email sending code called directly inside a request handler
- No job queue or background worker in the architecture
- No "notification status" field on Event model
- Testing with ≤10 players and assuming it scales

**Phase to address:** Email notification phase

---

### Pitfall 4: CSV Import Has No Validation Preview Step

**What goes wrong:**
Commander uploads a 800-row CSV. Row 247 has a malformed email. The import runs, creates 246 players, errors on row 247, and rolls back nothing — leaving the database in a partial state. Or it silently skips malformed rows, and the commander has no idea 15 players didn't get imported. The commander re-imports, creating duplicates.

**Why it happens:**
Developers implement "upload → parse → insert" as a single transaction. Preview/validation UI is skipped as an "enhancement."

**How to avoid:**
CSV import must be a two-phase operation: **validate first, commit second.** Phase 1: parse and validate all rows, return a preview with a row-count, error list, and diff (new vs. existing players). Phase 2: commander reviews and confirms. Only Phase 2 writes to the database. Use upsert-by-email to handle re-imports gracefully. Log all import operations with row-level results.

**Warning signs:**
- Single `POST /import` endpoint that does everything
- No preview/confirmation UI in the design
- No upsert logic — pure INSERT without duplicate detection
- Import result is just "success" or "failure" with no row details

**Phase to address:** CSV import phase

---

### Pitfall 5: Uploaded Files Served Directly From a Public URL Without Access Control

**What goes wrong:**
Commanders upload event maps (PDFs, KMZ files) and operational documents. These are stored in a public S3 bucket or served directly by the API with a predictable path. Any unauthenticated user — including non-players of this event — can access the files by guessing or sharing the URL. Pre-event operational security is compromised.

**Why it happens:**
"We'll add auth to files later." Public S3 buckets are easier to set up than signed URLs. File auth is treated as separate from route auth.

**How to avoid:**
Files must be stored in a private bucket. All file access goes through an authenticated API endpoint that checks if the requesting user is a participant of the event the file belongs to, then returns a short-lived signed URL (S3 presigned URL, 15-minute expiry). Never expose the underlying storage URL. Filenames must be UUIDs, not user-supplied names.

**Warning signs:**
- Static file serving without authentication middleware
- S3 bucket or storage path configured as public
- File URLs are the direct storage provider URL, not an app-proxied route
- User-supplied filenames preserved on disk

**Phase to address:** File upload/storage phase

---

### Pitfall 6: Magic Link Tokens Are Long-Lived or Reusable

**What goes wrong:**
Players receive a magic link to access their event assignment. The link is valid for 30 days and can be used multiple times. Players forward the email to teammates. Someone bookmarks the link. The link is indexed by corporate email scanning software that auto-clicks links. Weeks later, a token is used by an unintended party.

**Why it happens:**
Long-lived tokens feel more user-friendly ("players won't have to re-request"). Single-use tokens require more implementation complexity.

**How to avoid:**
Magic link tokens must be: (1) single-use — invalidated on first use, (2) time-limited — expire in 15–60 minutes, (3) at least 32 bytes of cryptographically random entropy. After use, issue a standard session token with a reasonable expiry (e.g., 7 days). Email security scanners that auto-click links will consume a single-use token — mitigate by checking `User-Agent` or using a two-step confirmation page ("Click here to log in" rather than the token URL being the auth action itself).

**Warning signs:**
- Token expiry set to days or weeks
- No `used_at` column on token table
- Token not invalidated after successful auth
- Token entropy less than 32 bytes (e.g., UUID v4 = 16 bytes — acceptable but use 32+ for security)

**Phase to address:** Authentication phase

---

### Pitfall 7: Event Publish Is Irreversible With No Draft/Preview State

**What goes wrong:**
Commander finishes building the roster, hits "Publish," and 800 email notifications go out immediately. There was a typo in the briefing. One squad was assigned to the wrong platoon. There is no way to un-publish or re-notify. Commanders are afraid to use the "Publish" button until everything is perfect, delaying event setup. Or they publish prematurely and players receive confusing follow-up emails.

**Why it happens:**
Publish-as-a-button feels like a simple final step. Draft vs. Published state management is treated as unnecessary complexity.

**How to avoid:**
Events must have a clear state machine: `Draft → Published`. In Draft, everything is editable, no notifications sent. In Published, the event is visible to players, but content can still be updated with a separate "Send Update Notification" action (not automatic). This gives commanders control over when players are notified of changes. Include a preview mode that shows exactly what players will see before publishing.

**Warning signs:**
- No `status` field on Event model
- Publish and notification are a single atomic action
- No way to view the "player view" before publishing
- No way to update a published event without re-notifying all players

**Phase to address:** Event management phase

---

## Moderate Pitfalls

### Pitfall 8: Touch Target Sizes Too Small for Mobile Use

**What goes wrong:**
Players on the field accessing their assignment via phone can't tap the correct button or link because touch targets are too small (less than 44×44px). Admin interfaces built for desktop use are technically responsive (they don't overflow) but are unusable on mobile — buttons are tiny, tables are cramped, and hierarchical drag-and-drop doesn't work on touch.

**Prevention:**
Follow Apple HIG / Google Material minimum touch target of 44×44px. Test admin interfaces on actual phones, not just browser dev tools responsive mode. Use mobile-first CSS (design for smallest screen first, then expand). For player-facing pages (assignment view, map download) this is critical — treat them as a mobile app, not a responsive website.

**Phase to address:** UI/Frontend phase; validate during any phase that touches player-facing views

---

### Pitfall 9: CSV Import Silently Accepts the Wrong Column Order

**What goes wrong:**
Commander exports from their registration system with columns in the order `name, callsign, email, team`. The import template expects `email, name, callsign, team`. Everything imports "successfully" but emails are stored in the name field and vice versa. Players receive notifications to non-existent addresses. The problem is not caught until someone complains.

**Prevention:**
Parse CSV by column header name, not position. Require specific header names and validate them on upload, before processing any rows. Show the detected headers in the validation preview so the commander can confirm the mapping. Reject files where required headers are missing.

**Phase to address:** CSV import phase

---

### Pitfall 10: Re-Import Overwrites Manual Changes

**What goes wrong:**
Commander imports 800 players, then manually reassigns 20 players to different squads. Registration system adds 30 new registrants. Commander re-imports the CSV to add the new players — and the import resets all 20 manual squad assignments back to the CSV values.

**Prevention:**
Define a clear import strategy: imports update player metadata (name, callsign, team affiliation) but do NOT overwrite hierarchy assignments made after the initial import. Add a "protect assignments" flag per player or per import operation. Make the behavior explicit in the UI and confirmation preview.

**Phase to address:** CSV import phase + hierarchy management phase

---

### Pitfall 11: Notification Blast Triggers Sending Domain Reputation Issues

**What goes wrong:**
App sends 800 emails in a short burst from a new sending domain. ISPs treat this as a spam burst. Emails land in spam for all 800 players right before the event. SPF/DKIM/DMARC are not set up, compounding the problem.

**Prevention:**
Set up SPF, DKIM, and DMARC before sending any production emails. Use a reputable transactional email provider (SendGrid, Postmark, Resend) — do NOT send directly from a VPS SMTP server. Use the provider's batch/bulk send API to send emails at a controlled rate (not a tight loop). Use a subdomain for event notifications (`notify.yourdomain.com`) to isolate reputation from other organizational email. Test deliverability with a small batch before the first major event.

**Phase to address:** Email notification phase

---

### Pitfall 12: Markdown Renderer Allows XSS in Information Sections

**What goes wrong:**
Commanders write event briefings in Markdown. A commander (malicious or careless) includes raw HTML in the Markdown: `<script>alert(1)</script>` or a link to a phishing site styled to look like an official resource. Players viewing the briefing are compromised.

**Prevention:**
Always sanitize rendered Markdown output with an allowlist-based HTML sanitizer (e.g., DOMPurify, sanitize-html) after parsing. Never render raw Markdown to HTML without sanitization. The sanitizer must run server-side OR on the client after fetching — never trust that the stored Markdown is safe. Disallow `<script>`, `<iframe>`, `on*` event attributes, and `javascript:` URLs.

**Phase to address:** Event management / information sections phase

---

### Pitfall 13: File Upload Size Limits Not Enforced at the HTTP Layer

**What goes wrong:**
A commander tries to upload a 500MB PDF (a full-resolution map scan). The server accepts the entire file into memory before the application code can check its size. Server runs out of memory or disk. Or a malicious actor uploads a zip bomb, consuming all available server resources.

**Prevention:**
Configure maximum file upload size at the HTTP server / reverse proxy level (nginx `client_max_body_size`, or platform-level limits), not just in application code. Enforce size limits before the body is fully buffered. For this app, reasonable limits are: maps/PDFs max 50MB, images max 10MB. Show a clear client-side error when limit is exceeded, don't let the upload start.

**Phase to address:** File upload/storage phase

---

### Pitfall 14: Hierarchical Drag-and-Drop Has No Keyboard/Touch Fallback

**What goes wrong:**
Platoon/squad builder is implemented as a drag-and-drop interface that only works with mouse. Commanders on tablets or laptops with trackpads have a poor experience. Mobile commanders can't use it at all. The UX demo works beautifully in a browser but is broken in real conditions.

**Prevention:**
Every drag-and-drop hierarchy operation must have a select-and-confirm alternative (dropdown "move to squad X" + confirm button). Drag-and-drop is a progressive enhancement, not the primary interaction. This is especially important for admin interfaces where commanders may be operating in field conditions on a tablet.

**Phase to address:** Hierarchy management phase

---

## Minor Pitfalls

### Pitfall 15: Past Events Are Retained But Become Inaccessible After Role Changes

**What goes wrong:**
The PROJECT.md specifies "Past events retained indefinitely." A commander leaves the organization and their account is deactivated. The events they owned are now orphaned — no one can access or manage them. Or a player account is deleted and their roster history disappears.

**Prevention:**
Events should be owned by an organization/faction entity, not a user account. Commander accounts can be transferred or reassigned. Establish a soft-delete policy for user accounts (mark inactive, don't delete) to preserve historical roster data.

**Phase to address:** Event management phase / data model design

---

### Pitfall 16: "Looks Done" but Missing Server-Side Validation

**What goes wrong:**
Client-side form validation rejects invalid emails, empty required fields, and oversized files. But the API endpoints have no corresponding validation. A developer posting directly to the API (or a browser with JavaScript disabled) can insert malformed data. The database ends up with `null` emails, invalid role strings, or orphaned records.

**Prevention:**
All validation must exist server-side. Client-side validation is UX only. Use a validation library (Zod, Joi, Yup) with shared schemas that apply to both API layer and (optionally) frontend. Treat every API request as potentially malicious.

**Phase to address:** Every feature phase; establish as a convention in the first implementation phase

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hard-code roles as strings instead of a permission matrix | Faster to build first role check | Every permission check must be found and updated when hierarchy changes; bugs guaranteed | Never — build the matrix from day one |
| Store uploaded files with user-supplied filenames | Simpler file retrieval | Path traversal risk, name collision, extension spoofing; security vulnerability | Never — always generate UUID filenames |
| Send emails inline in request handler | No queue infrastructure needed | Timeouts, duplicates, no retry logic; catastrophic failure on event publish | Never for bulk sends; only acceptable for single transactional email (e.g., magic link to one user) |
| Public S3 bucket for event documents | Zero config, fast CDN | Any unauthenticated user can access event materials; OPSEC violation | Never — this is an event with operational security requirements |
| Skip CSV validation preview | Faster to build | Partial imports, silent data corruption, duplicate entries; commander trust destroyed | Never — two-phase import is mandatory |
| Single `admin` role check instead of hierarchical roles | Simpler for MVP | Commanders can't delegate to platoon/squad leaders without giving full admin access | Only acceptable if hierarchy is genuinely not needed (this app has an explicit hierarchy requirement) |
| `no-reply@` sender for event notifications | No reply inbox to manage | Deliverability signals degraded; players have no way to reply; some ESPs rate it lower | Only acceptable for automated system emails (password reset); never for event notifications |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| SendGrid / transactional email | Using a single sending stream for both transactional (magic links) and bulk (event notifications) | Use separate message streams / sender identities; bulk sends that bounce hurt transactional deliverability |
| SendGrid / transactional email | Not setting up DKIM/SPF before first send | Configure DNS authentication before any production email is sent; retroactive setup is disruptive |
| S3 / cloud storage | Storing the raw S3 URL in the database | Store bucket + key only; generate presigned URLs at access time; URLs should never be permanent |
| S3 / cloud storage | Not setting CORS headers | Browser-side uploads (presigned PUT) require correct CORS configuration on the bucket |
| CSV from external registration systems | Assuming consistent column headers | External systems vary; parse by header name, validate headers on every import, never by column position |
| Magic links + email security scanners | Single-use token consumed by corporate email scanner auto-clicking the link | Use a two-step confirm page: token in URL triggers "click to sign in" page, not immediate auth |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Loading entire roster into memory for hierarchy display | Slow page load when viewing large event; browser tab freezes | Paginate or virtualize large lists; lazy-load squad members on expand | ~500+ players in a single view |
| Querying files metadata on every page load | Slow event landing page; N+1 on attachment list | Eager-load attachment counts with event query; cache counts | Always; noticeable at 10+ attachments per event |
| Synchronous email sending on publish | Request timeout; partial sends; duplicate notifications | Queue all bulk email operations | Any event with >50 recipients |
| Re-fetching full player list on every roster change request approval | Slow approval workflow; race conditions | Optimistic UI updates + targeted record invalidation | >100 pending requests |
| No database index on `event_id` foreign keys | Slow roster queries as event data grows | Add indexes on all foreign keys used in WHERE clauses from day 1 | ~1000+ records per table |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| IDOR: accessing player records by ID without event scoping | Player A views Player B's assignment or personal info | Always scope queries to the authenticated user's event membership; object-level auth tests |
| Mass assignment on user import: CSV row can set `role` field | Attacker uploads CSV with `role=system_admin` in a data column | Explicit allowlist for CSV-imported fields; `role` is NEVER set from CSV input |
| File upload: serving files from webroot | Uploaded PDF with embedded JS can be executed if served as HTML | Store all uploads outside webroot; serve via authenticated proxy; never guess MIME type from extension |
| Event ID enumeration: sequential IDs allow discovering all events | Unauthorized users discover event existence and structure | Use UUIDs for all public-facing resource identifiers |
| Magic link reuse: token valid after use | Phishing: attacker captures a forwarded magic link email and authenticates as the player | Single-use tokens; invalidate on first successful auth |
| Markdown stored as-is and rendered without sanitization | XSS in event briefings; commander can inject malicious scripts visible to all players | Sanitize all Markdown output with an allowlist HTML sanitizer before rendering |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| "Publish" button triggers immediate notifications | Commander publishes accidentally; 800 players notified of incomplete event info | Separate Publish (makes event visible) from Notify (sends emails); require explicit "Send Notifications" step |
| Import result is just "success/failure" | Commander doesn't know if any rows were skipped; data integrity unclear | Show row-by-row validation results: X imported, Y skipped, Z errors with row numbers and reasons |
| Player assignment view requires login before revealing the app exists | Players who receive a link can't see what the app is before creating an account | Public landing page explaining the app; magic link should auto-authenticate, not redirect to login |
| Admin table displays 800 players with no filtering or search | Commander can't find a specific player; pagination is confusing | Search by name/callsign/squad with instant filtering; client-side for <1000 records |
| File download on mobile opens in browser instead of downloading | Player opens PDF map in mobile browser; it renders tiny and isn't saved for offline use | Use `Content-Disposition: attachment` header; provide explicit "Download for offline" instructions |
| Roster change request has no status visibility for the player | Player submits a request and gets no feedback; submits again; commander sees duplicates | Show request status (Pending / Approved / Denied) on player's dashboard |

---

## "Looks Done But Isn't" Checklist

- [ ] **RBAC:** Often missing object-level checks — verify a Platoon Leader in Event A cannot read/write data from Event B
- [ ] **CSV Import:** Often missing duplicate detection — verify re-importing the same CSV doesn't create duplicate players
- [ ] **CSV Import:** Often missing column header validation — verify import fails gracefully when headers are wrong/missing
- [ ] **Email notifications:** Often missing delivery status tracking — verify commander can see delivery progress, not just "sent"
- [ ] **File upload:** Often missing auth on file access — verify a logged-out user cannot download event documents by guessing URL
- [ ] **File upload:** Often missing server-side size validation — verify a 500MB upload is rejected before consuming server memory
- [ ] **Magic links:** Often missing single-use enforcement — verify a magic link cannot be used twice
- [ ] **Event publish:** Often missing draft state — verify unpublished events are not visible to players
- [ ] **Markdown:** Often missing output sanitization — verify `<script>alert(1)</script>` in a briefing section does not execute
- [ ] **Mobile:** Often "responsive" but not usable — verify key player flows work with one thumb on a 375px screen

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Partial CSV import corrupts player data | MEDIUM | Implement soft-delete/versioning on player records; restore from last clean import; add import preview to prevent recurrence |
| Email blast sent with wrong data | HIGH | Cannot unsend; send a follow-up correction email; add draft/preview state to event workflow immediately |
| Files exposed via public storage URL | HIGH | Rotate to private bucket with signed URLs; audit access logs; notify affected users; change all storage URLs in DB |
| Magic links reused by email scanner | LOW | Invalidate all active tokens; require re-request; implement two-step confirm page |
| Role string comparison scattered across codebase | HIGH | Audit all permission checks; extract to centralized module; add regression tests before refactoring |
| Sequential IDs exposing event structure | MEDIUM | Migrate to UUIDs in a single migration; update all foreign keys; update client-facing URLs |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Object-level authorization bypass (IDOR) | Auth & authorization phase | Integration test: User A cannot read User B's resources |
| Scattered role string comparisons | Auth & authorization phase | Code review: no `role ===` outside the permission module |
| Bulk email sent synchronously | Email notifications phase | Load test: publish event with 800 players; verify no timeout |
| CSV import partial state / no preview | CSV import phase | Manual test: upload CSV with one bad row; verify preview shows error, nothing written |
| Files served without authentication | File upload/storage phase | Manual test: log out, attempt direct file URL; verify 401 |
| Magic link reuse | Authentication phase | Test: use magic link, use same link again; verify second use fails |
| Irreversible event publish | Event management phase | Manual test: verify draft events invisible to players; verify publish ≠ notify |
| Mobile touch targets | UI/Frontend phase | Manual test on 375px phone; verify all primary actions are tappable |
| CSV column order assumption | CSV import phase | Test: upload CSV with columns in different order; verify correct mapping |
| Re-import overwrites manual assignments | CSV import + hierarchy phase | Test: assign player to squad, re-import, verify assignment preserved |
| Notification blast reputation damage | Email notifications phase | Pre-launch: verify SPF/DKIM/DMARC records; test with small batch |
| XSS in Markdown sections | Event management / info sections phase | Test: insert `<script>` tag in briefing; verify it does not execute for readers |
| File size limits not at HTTP layer | File upload/storage phase | Test: attempt 500MB upload; verify rejection before full upload completes |
| Server-side validation missing | Every feature phase | API test: POST malformed data directly bypassing client validation |

---

## Sources

- OWASP Authorization Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html (HIGH confidence)
- OWASP File Upload Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html (HIGH confidence)
- OWASP Input Validation Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html (HIGH confidence)
- OWASP Mass Assignment Cheat Sheet — https://cheatsheetseries.owasp.org/cheatsheets/Mass_Assignment_Cheat_Sheet.html (HIGH confidence)
- Postmark Transactional Email Best Practices Guide (2026 update) — https://postmarkapp.com/guides/transactional-email-best-practices (HIGH confidence)
- Google web.dev Responsive Web Design Basics — https://web.dev/articles/responsive-web-design-basics (HIGH confidence)
- Domain-specific analysis of the PROJECT.md requirements (event planning, roster management, mobile-first player access)

---
*Pitfalls research for: Airsoft/milsim event management and roster management web app*
*Researched: 2026-03-12*
