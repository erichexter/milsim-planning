# Phase 3: Content, Maps & Notifications - Research

**Researched:** 2026-03-13
**Domain:** Cloudflare R2 (AWSSDK.S3) · BackgroundService + Channels · Resend .NET SDK · react-markdown · @dnd-kit/sortable · EF Core new entities · Squad-change email trigger
**Confidence:** HIGH — all core claims verified against official Cloudflare R2 docs, Microsoft ASP.NET Core docs (aspnetcore-10.0), Resend official API reference, @dnd-kit official docs, and react-markdown GitHub (March 2026)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Markdown Editor UX (CONT-01, CONT-02):**
- Toggle view — Edit tab / Preview tab, switch between them (not split view, not textarea-only)
- Inline "Add Section" button — appears at the bottom of the event briefing page; clicking it expands a new section editor in place (no modal, no navigation)
- Explicit save button — commander clicks Save when done; no auto-save, no save-on-collapse
- Collapsed sections show title only — no content preview, no attachment count badge
- Plain text title field above the editor — always visible while editing; save is blocked if title is empty

**Information Section Reordering (CONT-04):**
- Drag-and-drop with a left-edge grip icon (`⠿` or `≡` handle) on each section card
- Saves immediately on drop — no pending state, no confirm button; drag-back to undo
- DnD library: Claude's discretion — pick whichever fits best with the existing React/shadcn/ui stack

**File Attachment UX (CONT-03, MAPS-03, MAPS-04, MAPS-05):**
- Inline per-section upload — each info section and each map resource has its own upload zone; files belong to that section only (no shared event-level file library)
- Each file requires: the file itself + a friendly display name (e.g. "Comms Plan v2.pdf" → friendly name "Comms Plan")
- Direct download — clicking the file name triggers an immediate browser download via authenticated pre-signed Cloudflare R2 URL (no preview modal, no new tab)
- Inline error in the upload zone — file errors (wrong type, too large, network failure) appear as a red message directly under the upload zone where the attempt was made
- 10 MB per file limit — enforced on both client (before upload) and server (before R2 write); files larger than 10 MB are rejected

**Notification Blast (NOTF-01, NOTF-05):**
- Free-form subject + body — commander writes their own subject line and message body; no templates
- Confirmation toast + immediate return — after Send, a toast appears ("Notification queued, emails sending...") and the UI is immediately usable; no progress indicator, no navigation away
- Simple send log — a list of past blasts per event showing: subject, date sent, recipient count; no per-recipient delivery status

**Squad Assignment Change Emails (NOTF-02):**
- Old + new assignment — email includes both where the player was and where they've been moved to (e.g. "You've been moved from Bravo Squad, 2nd Platoon to Alpha Squad, 1st Platoon")

### Claude's Discretion
- DnD library choice (suggested: @dnd-kit/sortable — React-first, accessible, shadcn/ui compatible)
- Exact markdown editor component (suggested: simple textarea with react-markdown for preview rendering — no heavyweight editor like TipTap needed)
- Pre-signed URL expiry duration (suggested: 1 hour — long enough for downloads, short enough for security)
- Notification blast recipient definition ("all event participants" = all EventPlayer records for the event with a non-null UserId)
- Email template styling for squad-change and blast emails

### Deferred Ideas (OUT OF SCOPE)
- Per-recipient delivery status (bounces, opens) — not needed in v1; simple log is sufficient
- File preview modal (images/PDFs open before download) — Phase 4 or backlog
- Notification preferences / opt-out — v2 requirement (NOTF-V2-01)
- In-app notification center — v2 requirement (NOTF-V2-02)
- Scheduled/recurring notification blasts — out of scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONT-01 | Faction Commander can create custom information sections within an event | EF Core `InfoSection` entity; inline Add Section button; POST `/events/{id}/info-sections` |
| CONT-02 | Each information section supports Markdown text content | Textarea editor + react-markdown preview; toggle Edit/Preview tabs |
| CONT-03 | Each information section supports file attachments (PDF, images) | `InfoSectionAttachment` entity; per-section upload zone; AWSSDK.S3 pre-signed PUT; R2 key stored in DB |
| CONT-04 | Faction Commander can reorder information sections | `InfoSection.Order` field; PATCH `/events/{id}/info-sections/reorder`; @dnd-kit/sortable on client |
| CONT-05 | Faction Commander can edit or delete information sections | PUT/DELETE endpoints; edit-in-place with explicit Save; ScopeGuard + RequireFactionCommander |
| MAPS-01 | Faction Commander can add external map platform links to an event | `MapResource` entity with `ExternalUrl` field; POST `/events/{id}/map-resources` |
| MAPS-02 | Faction Commander can add setup instructions for each external map link | `MapResource.Instructions` markdown text field |
| MAPS-03 | Faction Commander can upload downloadable map files (PDF, JPEG, PNG, KMZ) | `MapResource` with `R2Key` field; same upload flow as CONT-03; MIME type whitelist enforced server-side |
| MAPS-04 | Players can download map files for offline use | GET `/map-resources/{id}/download-url` returns pre-signed GET URL; RequirePlayer policy |
| MAPS-05 | Uploaded files are stored privately with authenticated time-limited download links | Private R2 bucket; pre-signed GET URL with 1-hour expiry; no public bucket access |
| NOTF-01 | Faction Commander can send an email notification blast to all event participants | POST `/events/{id}/notification-blasts`; queues `BlastNotificationJob` to Channel; returns 202 |
| NOTF-02 | Email notifications are sent when squad assignments change | `HierarchyService.AssignSquadAsync` enqueues `SquadChangeJob` after DB save |
| NOTF-03 | Email notifications are sent when a roster change request is approved or denied | RCHG phase (Phase 4) — pipeline built here, trigger added in Phase 4 |
| NOTF-04 | Notification emails are delivered via a transactional email provider | Resend .NET SDK (`IResend.EmailSendAsync`); replaces Phase 1 stub |
| NOTF-05 | Bulk notification send is processed asynchronously | `Channel<NotificationJob>` → `NotificationWorker` BackgroundService; individual `EmailSendAsync` per recipient in batches |
</phase_requirements>

---

## Summary

Phase 3 is built on four distinct technical concerns: (1) EF Core entity additions for `InfoSection`, `InfoSectionAttachment`, `MapResource`, `NotificationBlast`, and their DB migration; (2) Cloudflare R2 file upload/download using AWSSDK.S3 pre-signed URLs — upload uses a two-step PUT-then-confirm flow; (3) the BackgroundService + Channels email queue, now wired to real Resend delivery; and (4) React UI for the markdown toggle editor, drag-and-drop section reorder, and file upload zones.

**Critical batch-send finding:** Resend's batch endpoint is **100 emails max per API call**, each email is **50 recipients max**. For 800 recipients, the `NotificationWorker` must chunk the recipient list (e.g., 8 batches of 100 individual `EmailSendAsync` calls per batch-of-100). The simplest approach: send one `EmailMessage` per recipient in the BackgroundService loop — at ~100ms per call, 800 recipients ≈ 80 seconds. This is acceptable for async blast (NOTF-05). Use `resend.EmailBatchAsync` to reduce HTTP round trips: send up to 100 messages per call → 8 total API calls for 800 recipients.

**R2 key insight:** Cloudflare R2 presigned URLs max expiry is 7 days (604,800 seconds). For downloads, use 1-hour expiry (per CONTEXT.md discretion). For upload pre-signed PUT URLs, use 15-minute expiry. R2 presigned URLs **cannot** be used with custom domains — must use the `<account-id>.r2.cloudflarestorage.com` endpoint.

**BackgroundService DI scope:** `BackgroundService` is a singleton; it cannot directly inject scoped services like `AppDbContext`. Must create a new `IServiceScope` via `IServiceProvider.CreateScope()` per job execution to access EF Core context. This is the documented pattern from Microsoft's official hosted services docs.

**Primary recommendation:** Use `resend.EmailBatchAsync` in 100-message chunks inside the `NotificationWorker`; `@dnd-kit/sortable` with `verticalListSortingStrategy` for section reorder (drag handle: `setActivatorNodeRef` pattern); `react-markdown` + `remark-gfm` for preview tab; AWSSDK.S3 `GetPreSignedURL` for R2 uploads and downloads (synchronous overload, no async needed).

---

## Standard Stack

### Core (Phase 3 additions to Phase 1/2 foundation)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `AWSSDK.S3` | 3.7.x | S3-compatible client for Cloudflare R2; pre-signed URL generation | Official AWS SDK; S3-compatible with R2 via `ServiceURL` + `ForcePathStyle=true`; `GetPreSignedURL()` synchronous call requires no async overhead |
| `AWSSDK.Extensions.NETCore.Setup` | 3.7.x | DI registration for `IAmazonS3` singleton | Simplifies singleton injection with `AddSingleton<IAmazonS3>` pattern |
| `Resend` | 1.x | Transactional email delivery; replaces Phase 1 stub | `IResend.EmailSendAsync` single send; `IResend.EmailBatchAsync` batch of up to 100 messages; official .NET SDK |
| `@dnd-kit/core` | 6.x | Drag-and-drop core context, sensors, collision detection | Accessibility-first DnD; React-first (no DOM hacks); used by shadcn/ui community |
| `@dnd-kit/sortable` | 8.x | Sortable list preset on top of @dnd-kit/core | Provides `useSortable`, `SortableContext`, `arrayMove`, `verticalListSortingStrategy` |
| `@dnd-kit/utilities` | 3.x | CSS transform utilities (`CSS.Transform.toString`) | Required companion to @dnd-kit/sortable |
| `react-markdown` | 10.x | Markdown → React elements for preview tab | XSS-safe by default (no dangerouslySetInnerHTML); 455K+ npm dependents; ESM-only in v10 |
| `remark-gfm` | 4.x | GitHub Flavored Markdown plugin (tables, strikethrough, task lists) | Commanders expect GFM features in briefings |
| `react-dropzone` | 14.x | Headless file drop zone for per-section upload zones | Already in stack from Phase 2; handles file object access |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Moq` | 4.20.x | Mock `IAmazonS3`, `IResend` in integration tests | Already in test project; use for BackgroundService + R2 tests |
| `FluentAssertions` | 7.x | Readable assertions in xUnit tests | Already established |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| @dnd-kit/sortable | react-beautiful-dnd | react-beautiful-dnd is in maintenance-only mode (Atlassian deprecated it 2023); @dnd-kit is the community successor |
| @dnd-kit/sortable | @hello-pangea/dnd (rbd fork) | @hello-pangea/dnd is a maintained fork of rbd but @dnd-kit has broader community adoption and better accessibility |
| react-markdown | marked.js | marked.js requires `dangerouslySetInnerHTML`; react-markdown renders to React elements (XSS-safe) |
| react-markdown | MDX | MDX allows JSX in markdown — overkill for read-only preview; adds build-time complexity |
| Resend `EmailBatchAsync` (100/call) | Individual `EmailSendAsync` per recipient | 800 individual calls ≈ 800 × ~100ms = 80 seconds vs 8 batch calls ≈ 8 × ~200ms = 1.6 seconds. Use batch. |

### Installation (Phase 3 additions)

```bash
# .NET API
dotnet add package AWSSDK.S3
dotnet add package AWSSDK.Extensions.NETCore.Setup
dotnet add package Resend  # already in project; ensure wired (was stub in Phase 1)

# React frontend
pnpm add @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities
pnpm add react-markdown remark-gfm
# react-dropzone already installed from Phase 2
```

---

## Architecture Patterns

### New EF Core Entities

```csharp
// Data/Entities/InfoSection.cs
public class InfoSection
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; } = null!;
    public string? BodyMarkdown { get; set; }
    public int Order { get; set; }                        // used for drag-and-drop ordering
    public Event Event { get; set; } = null!;
    public ICollection<InfoSectionAttachment> Attachments { get; set; } = [];
}

// Data/Entities/InfoSectionAttachment.cs
public class InfoSectionAttachment
{
    public Guid Id { get; set; }
    public Guid InfoSectionId { get; set; }
    public string R2Key { get; set; } = null!;            // e.g. "events/{eventId}/sections/{sectionId}/files/{attachmentId}"
    public string FriendlyName { get; set; } = null!;     // display name, e.g. "Comms Plan"
    public string ContentType { get; set; } = null!;      // MIME type, validated server-side
    public long FileSizeBytes { get; set; }
    public InfoSection Section { get; set; } = null!;
}

// Data/Entities/MapResource.cs
public class MapResource
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? ExternalUrl { get; set; }              // nullable: external link OR file
    public string? Instructions { get; set; }             // markdown instructions
    public string? R2Key { get; set; }                    // nullable: set if file upload
    public string? FriendlyName { get; set; }             // display name for file
    public string? ContentType { get; set; }              // MIME type if file
    public int Order { get; set; }
    public Event Event { get; set; } = null!;
}

// Data/Entities/NotificationBlast.cs
public class NotificationBlast
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public int RecipientCount { get; set; }
    public Event Event { get; set; } = null!;
}
```

**EF Core configuration in OnModelCreating:**
```csharp
// AppDbContext.cs — add to OnModelCreating
builder.Entity<InfoSection>()
    .HasIndex(s => new { s.EventId, s.Order });

builder.Entity<MapResource>()
    .HasIndex(r => new { r.EventId, r.Order });

// InfoSectionAttachment: no unique index needed (multiple files per section OK)
```

**Migration approach:** Add a single `Phase3Schema` migration (same delta pattern as Phase 2).

---

### Pattern 1: Cloudflare R2 File Upload (Two-Step: Pre-Signed PUT + Confirm)

**What:** Commander selects file in browser → SPA calls API to get a pre-signed PUT URL → SPA uploads file directly to R2 (no API middleman) → SPA calls API to confirm the upload → API saves the attachment record to DB.

**Why two steps:** Files go directly from browser to R2 (not proxied through the API server). This avoids 10MB payloads transiting the API server and enables R2 egress-free downloads.

```csharp
// Source: Official Cloudflare R2 presigned URL docs + AWSSDK.S3 docs
// Services/FileService.cs

private static readonly HashSet<string> AllowedMimeTypes = new()
{
    "application/pdf",
    "image/jpeg",
    "image/png",
    "image/gif",
    "application/vnd.google-earth.kmz",
    "application/vnd.google-earth.kml+xml",
    "application/zip"  // KMZ is a zip variant; some browsers send this
};

public record UploadUrlResponse(Guid UploadId, string PresignedPutUrl, string R2Key);

public UploadUrlResponse GenerateUploadUrl(Guid eventId, Guid sectionId, string contentType, string fileName)
{
    // Server-side MIME validation — reject before generating URL
    if (!AllowedMimeTypes.Contains(contentType))
        throw new ValidationException($"File type '{contentType}' is not permitted.");

    var uploadId = Guid.NewGuid();
    var r2Key = $"events/{eventId}/sections/{sectionId}/files/{uploadId}/{fileName}";

    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucket,
        Key = r2Key,
        Verb = HttpVerb.PUT,
        Expires = DateTime.UtcNow.AddMinutes(15),  // short-lived PUT URL
        ContentType = contentType                   // R2 enforces this in the signature
    };

    var url = _s3.GetPreSignedURL(request);  // synchronous — no async overload in AWSSDK v3

    return new UploadUrlResponse(uploadId, url, r2Key);
}

public string GenerateDownloadUrl(string r2Key)
{
    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucket,
        Key = r2Key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddHours(1)  // 1-hour TTL per CONTEXT.md
    };

    return _s3.GetPreSignedURL(request);
}
```

**File size enforcement — client side (before requesting upload URL):**
```typescript
// web/src/components/content/SectionUploadZone.tsx
const MAX_FILE_BYTES = 10 * 1024 * 1024; // 10 MB

const { getRootProps, getInputProps } = useDropzone({
  onDrop: (acceptedFiles, rejectedFiles) => {
    const file = acceptedFiles[0];
    if (!file) return;
    if (file.size > MAX_FILE_BYTES) {
      setError("File must be 10 MB or smaller.");
      return;
    }
    handleUpload(file);
  },
  maxSize: MAX_FILE_BYTES,
  multiple: false,
});
```

**File size enforcement — server side (before generating URL):**
```csharp
// Controllers/InfoSectionsController.cs
[HttpPost("{sectionId:guid}/attachments/upload-url")]
[Authorize(Policy = "RequireFactionCommander")]
public IActionResult GetUploadUrl(Guid eventId, Guid sectionId, UploadUrlRequest request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    // Server-side 10 MB check — client sends file size in the request body
    if (request.FileSizeBytes > 10 * 1024 * 1024)
        return BadRequest(new { error = "File must be 10 MB or smaller." });

    var result = _fileService.GenerateUploadUrl(eventId, sectionId, request.ContentType, request.FileName);
    return Ok(result);
}
```

**IAmazonS3 DI registration:**
```csharp
// Program.cs
builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new AmazonS3Client(
        new BasicAWSCredentials(
            config["R2:AccessKeyId"],
            config["R2:SecretAccessKey"]
        ),
        new AmazonS3Config
        {
            ServiceURL = $"https://{config["R2:AccountId"]}.r2.cloudflarestorage.com",
            ForcePathStyle = true  // Required for R2
        }
    );
});
```

---

### Pattern 2: BackgroundService + Channels Email Queue

**Critical constraint:** `NotificationWorker` is a singleton (BackgroundService lifecycle). It CANNOT inject scoped services (`AppDbContext`, `IResend` if scoped). Must create a scope per job execution.

```csharp
// Source: Microsoft official hosted services docs (aspnetcore-10.0)
// Infrastructure/BackgroundJobs/NotificationJob.cs

public abstract record NotificationJob;

public record BlastNotificationJob(
    Guid EventId,
    Guid NotificationBlastId,
    string Subject,
    string Body,
    List<string> RecipientEmails  // pre-loaded at enqueue time
) : NotificationJob;

public record SquadChangeJob(
    string RecipientEmail,
    string RecipientName,
    string OldPlatoonName,
    string OldSquadName,
    string NewPlatoonName,
    string NewSquadName
) : NotificationJob;

// Infrastructure/BackgroundJobs/INotificationQueue.cs
public interface INotificationQueue
{
    ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default);
    IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken ct);
}

// Infrastructure/BackgroundJobs/NotificationQueue.cs
public class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationJob> _channel =
        Channel.CreateBounded<NotificationJob>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}

// Infrastructure/BackgroundJobs/NotificationWorker.cs
public class NotificationWorker : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceProvider _services;  // IServiceProvider, NOT IResend (scoped)
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        INotificationQueue queue,
        IServiceProvider services,
        ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification job {JobType}", job.GetType().Name);
                // No retry — job is dropped. For retry semantics, add Hangfire.
            }
        }
    }

    private async Task ProcessJobAsync(NotificationJob job, CancellationToken ct)
    {
        // Create a scope so we can resolve scoped services (IResend, AppDbContext)
        await using var scope = _services.CreateAsyncScope();
        var resend = scope.ServiceProvider.GetRequiredService<IResend>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (job)
        {
            case BlastNotificationJob blast:
                await ProcessBlastAsync(blast, resend, db, ct);
                break;
            case SquadChangeJob squadChange:
                await ProcessSquadChangeAsync(squadChange, resend, ct);
                break;
        }
    }

    private async Task ProcessBlastAsync(
        BlastNotificationJob blast,
        IResend resend,
        AppDbContext db,
        CancellationToken ct)
    {
        // Chunk recipients into batches of 100 (Resend batch limit)
        const int batchSize = 100;
        var chunks = blast.RecipientEmails
            .Chunk(batchSize)
            .ToList();

        foreach (var chunk in chunks)
        {
            var messages = chunk.Select(email => new EmailMessage
            {
                From = "MilSim Platform <noreply@yourdomain.com>",
                To = { email },
                Subject = blast.Subject,
                HtmlBody = FormatBlastHtml(blast.Body)
            }).ToList();

            await resend.EmailBatchAsync(messages);
            await Task.Delay(200, ct);  // rate limit buffer between batches
        }

        // Update notification blast record with actual recipient count
        var blastRecord = await db.NotificationBlasts.FindAsync(blast.NotificationBlastId);
        if (blastRecord is not null)
        {
            blastRecord.RecipientCount = blast.RecipientEmails.Count;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task ProcessSquadChangeAsync(
        SquadChangeJob job,
        IResend resend,
        CancellationToken ct)
    {
        var message = new EmailMessage
        {
            From = "MilSim Platform <noreply@yourdomain.com>",
            To = { job.RecipientEmail },
            Subject = "Your squad assignment has changed",
            HtmlBody = FormatSquadChangeHtml(job)
        };

        await resend.EmailSendAsync(message);
    }

    private static string FormatBlastHtml(string body) =>
        $"<div style='font-family:sans-serif'>{body.Replace("\n", "<br>")}</div>";

    private static string FormatSquadChangeHtml(SquadChangeJob job) =>
        $"""
        <div style='font-family:sans-serif'>
          <p>Hi {job.RecipientName},</p>
          <p>Your squad assignment has been updated:</p>
          <ul>
            <li><strong>From:</strong> {job.OldSquadName}, {job.OldPlatoonName}</li>
            <li><strong>To:</strong> {job.NewSquadName}, {job.NewPlatoonName}</li>
          </ul>
          <p>Log in to view the full roster.</p>
        </div>
        """;
}
```

**DI registration in Program.cs:**
```csharp
// Notification queue — singleton (outlives requests)
builder.Services.AddSingleton<INotificationQueue, NotificationQueue>();
builder.Services.AddHostedService<NotificationWorker>();
```

---

### Pattern 3: Squad Assignment Change Email Trigger (NOTF-02)

**Where to fire:** Inside `HierarchyService.AssignSquadAsync`, AFTER the DB save succeeds. Load the old assignment before overwriting, then enqueue the job.

```csharp
// Services/HierarchyService.cs
public async Task AssignSquadAsync(Guid eventPlayerId, Guid? newSquadId)
{
    ScopeGuard.AssertEventAccess(_currentUser, /* eventId */);

    var player = await _db.EventPlayers
        .Include(ep => ep.Squad)
            .ThenInclude(s => s!.Platoon)
        .FirstOrDefaultAsync(ep => ep.Id == eventPlayerId)
        ?? throw new NotFoundException();

    // Capture OLD assignment for email BEFORE overwriting
    var oldSquadName = player.Squad?.Name ?? "(unassigned)";
    var oldPlatoonName = player.Squad?.Platoon?.Name ?? "(unassigned)";

    // Update assignment
    player.SquadId = newSquadId;
    await _db.SaveChangesAsync();

    // Load new assignment for email
    var newSquad = newSquadId.HasValue
        ? await _db.Squads.Include(s => s.Platoon).FirstOrDefaultAsync(s => s.Id == newSquadId)
        : null;

    // Only send email if player has a user account (has accepted invitation)
    if (player.UserId is not null && player.Email is not null)
    {
        await _notificationQueue.EnqueueAsync(new SquadChangeJob(
            RecipientEmail: player.Email,
            RecipientName: player.Name,
            OldPlatoonName: oldPlatoonName,
            OldSquadName: oldSquadName,
            NewPlatoonName: newSquad?.Platoon?.Name ?? "(unassigned)",
            NewSquadName: newSquad?.Name ?? "(unassigned)"
        ));
    }
}
```

**Batch move anti-double-send:** When the commander moves multiple players at once (e.g., all of Bravo Squad to Alpha Squad), each player gets one call to `AssignSquadAsync`. Each enqueues one `SquadChangeJob`. The BackgroundService processes them sequentially — no double-send risk as long as the service layer is not called twice per player per operation. Recommend the controller take a list of `{ EventPlayerId, NewSquadId }` and call `AssignSquadAsync` once per player in a loop.

---

### Pattern 4: @dnd-kit/sortable for InfoSection Reorder (CONT-04)

**What:** Vertically sortable list of section cards, each with a left-edge grip handle. On drop, immediately calls `PATCH /events/{id}/info-sections/reorder` with the new order array.

```typescript
// Source: @dnd-kit/sortable official docs (docs.dndkit.com/presets/sortable)
// web/src/components/content/SectionList.tsx
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
  useSortable,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

// Grip handle pattern — activatorNodeRef lets grip icon be the only drag trigger
// (prevents text selection in title/body from activating drag)
function SortableSectionCard({ section }: { section: InfoSection }) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,  // attach to grip icon only
    transform,
    transition,
    isDragging,
  } = useSortable({ id: section.id });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div ref={setNodeRef} style={style}>
      <Card className="relative">
        {/* Left-edge grip icon */}
        <button
          ref={setActivatorNodeRef}
          {...attributes}
          {...listeners}
          className="absolute left-2 top-1/2 -translate-y-1/2 cursor-grab touch-none p-1 text-muted-foreground"
          aria-label="Drag to reorder"
        >
          ⠿
        </button>
        <CardContent className="ml-8">
          <SectionEditor section={section} />
        </CardContent>
      </Card>
    </div>
  );
}

// Parent component
function SectionList({ eventId, sections }: { eventId: string; sections: InfoSection[] }) {
  const [items, setItems] = useState(sections.map(s => s.id));
  const queryClient = useQueryClient();

  const reorderMutation = useMutation({
    mutationFn: (orderedIds: string[]) =>
      api.patch(`/events/${eventId}/info-sections/reorder`, { orderedIds }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['events', eventId, 'info-sections'] }),
  });

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates })
  );

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    if (!over || active.id === over.id) return;

    setItems(items => {
      const oldIndex = items.indexOf(active.id as string);
      const newIndex = items.indexOf(over.id as string);
      const reordered = arrayMove(items, oldIndex, newIndex);
      reorderMutation.mutate(reordered);  // save immediately on drop
      return reordered;
    });
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={items} strategy={verticalListSortingStrategy}>
        {items.map(id => {
          const section = sections.find(s => s.id === id)!;
          return <SortableSectionCard key={id} section={section} />;
        })}
      </SortableContext>
    </DndContext>
  );
}
```

**Reorder API endpoint:**
```csharp
// Controllers/InfoSectionsController.cs
[HttpPatch("reorder")]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> Reorder(Guid eventId, ReorderRequest request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);
    await _contentService.ReorderInfoSectionsAsync(eventId, request.OrderedIds);
    return NoContent();
}

// Services/ContentService.cs
public async Task ReorderInfoSectionsAsync(Guid eventId, List<Guid> orderedIds)
{
    var sections = await _db.InfoSections
        .Where(s => s.EventId == eventId)
        .ToListAsync();

    for (var i = 0; i < orderedIds.Count; i++)
    {
        var section = sections.First(s => s.Id == orderedIds[i]);
        section.Order = i;
    }

    await _db.SaveChangesAsync();
}
```

---

### Pattern 5: Markdown Toggle Editor (CONT-01, CONT-02)

**What:** A React component with Edit tab (plain `<textarea>`) and Preview tab (react-markdown render). Plain text title field always visible. Save button blocked if title is empty.

```typescript
// Source: react-markdown official README (github.com/remarkjs/react-markdown)
// web/src/components/content/SectionEditor.tsx
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';

interface SectionEditorProps {
  section: InfoSection;
  onSave: (updates: { title: string; bodyMarkdown: string }) => Promise<void>;
}

export function SectionEditor({ section, onSave }: SectionEditorProps) {
  const [title, setTitle] = useState(section.title);
  const [body, setBody] = useState(section.bodyMarkdown ?? '');
  const [saving, setSaving] = useState(false);

  const canSave = title.trim().length > 0;

  async function handleSave() {
    if (!canSave) return;
    setSaving(true);
    await onSave({ title: title.trim(), bodyMarkdown: body });
    setSaving(false);
  }

  return (
    <div className="space-y-3">
      {/* Title field — always visible while editing */}
      <Input
        value={title}
        onChange={e => setTitle(e.target.value)}
        placeholder="Section title (required)"
        className={!title.trim() ? 'border-destructive' : ''}
      />

      <Tabs defaultValue="edit">
        <TabsList>
          <TabsTrigger value="edit">Edit</TabsTrigger>
          <TabsTrigger value="preview">Preview</TabsTrigger>
        </TabsList>

        <TabsContent value="edit">
          <textarea
            value={body}
            onChange={e => setBody(e.target.value)}
            className="w-full min-h-[200px] rounded-md border border-input bg-background px-3 py-2 text-sm font-mono"
            placeholder="Write markdown here..."
          />
        </TabsContent>

        <TabsContent value="preview">
          <div className="prose prose-sm dark:prose-invert min-h-[200px] rounded-md border border-input p-3">
            {body.trim()
              ? <Markdown remarkPlugins={[remarkGfm]}>{body}</Markdown>
              : <p className="text-muted-foreground italic">Nothing to preview yet.</p>
            }
          </div>
        </TabsContent>
      </Tabs>

      <Button onClick={handleSave} disabled={!canSave || saving}>
        {saving ? 'Saving...' : 'Save'}
      </Button>
    </div>
  );
}
```

**Note:** `react-markdown` v10 is ESM-only. Vite handles this natively — no configuration change needed. Tailwind Typography plugin (`@tailwindcss/typography`) provides the `prose` class for markdown styling.

---

### Pattern 6: Notification Blast API + Send Log (NOTF-01, NOTF-05)

```csharp
// Controllers/NotificationBlastsController.cs
[HttpPost]
[Authorize(Policy = "RequireFactionCommander")]
public async Task<IActionResult> SendBlast(Guid eventId, SendBlastRequest request)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    // Load recipient emails at request time (not inside BackgroundService)
    var recipients = await _db.EventPlayers
        .Where(ep => ep.EventId == eventId && ep.UserId != null && ep.Email != null)
        .Select(ep => ep.Email!)
        .ToListAsync();

    // Create blast log record synchronously
    var blast = new NotificationBlast
    {
        EventId = eventId,
        Subject = request.Subject,
        Body = request.Body,
        SentAt = DateTime.UtcNow,
        RecipientCount = recipients.Count  // will be updated by worker on completion
    };
    _db.NotificationBlasts.Add(blast);
    await _db.SaveChangesAsync();

    // Enqueue to background worker — returns immediately
    await _notificationQueue.EnqueueAsync(new BlastNotificationJob(
        eventId, blast.Id, request.Subject, request.Body, recipients));

    return Accepted(new { blastId = blast.Id, recipientCount = recipients.Count });
}

[HttpGet]
[Authorize(Policy = "RequirePlayer")]
public async Task<IActionResult> GetBlastLog(Guid eventId)
{
    ScopeGuard.AssertEventAccess(_currentUser, eventId);

    var blasts = await _db.NotificationBlasts
        .Where(b => b.EventId == eventId)
        .OrderByDescending(b => b.SentAt)
        .Select(b => new { b.Id, b.Subject, b.SentAt, b.RecipientCount })
        .ToListAsync();

    return Ok(blasts);
}
```

---

### Recommended Project Structure (Phase 3 additions)

```
src/MilsimPlanning.Api/
├── Controllers/
│   ├── InfoSectionsController.cs       ← CONT-01..05
│   ├── MapResourcesController.cs       ← MAPS-01..05
│   ├── NotificationBlastsController.cs ← NOTF-01, NOTF-05 (blast + send log)
│   └── (FilesController.cs optional — upload-url + confirm endpoints)
├── Services/
│   ├── ContentService.cs               ← info section CRUD + reorder
│   ├── MapResourceService.cs           ← map resource CRUD
│   ├── FileService.cs                  ← R2 pre-signed URL generation + key management
│   └── (HierarchyService.cs — Phase 2; modified to enqueue SquadChangeJob)
├── Infrastructure/
│   └── BackgroundJobs/
│       ├── INotificationQueue.cs       ← Channel-based interface
│       ├── NotificationQueue.cs        ← Channel<NotificationJob> singleton
│       ├── NotificationWorker.cs       ← BackgroundService + Resend delivery
│       └── NotificationJob.cs          ← record types (BlastNotificationJob, SquadChangeJob)
├── Data/Entities/
│   ├── InfoSection.cs                  ← new
│   ├── InfoSectionAttachment.cs        ← new
│   ├── MapResource.cs                  ← new
│   └── NotificationBlast.cs            ← new
└── Data/Migrations/
    └── Phase3Schema.cs                 ← new migration

web/src/
├── pages/
│   └── events/
│       ├── EventDetail.tsx             ← extended with briefing sections below metadata
│       ├── BriefingPage.tsx            ← CONT-01..05 (section list + add section)
│       ├── MapResourcesPage.tsx        ← MAPS-01..05
│       └── NotificationBlastPage.tsx   ← NOTF-01 (send form + blast log)
└── components/
    └── content/
        ├── SectionList.tsx             ← DnD sortable section cards
        ├── SortableSectionCard.tsx     ← useSortable + grip handle
        ├── SectionEditor.tsx           ← Edit/Preview tabs + title + save
        ├── SectionAttachments.tsx      ← per-section upload zone + file list
        ├── MapResourceCard.tsx         ← URL/file resource display
        └── UploadZone.tsx              ← react-dropzone wrapper (reusable)
```

### Anti-Patterns to Avoid

- **Injecting scoped services directly into BackgroundService:** `NotificationWorker` is singleton — inject `IServiceProvider` and create `CreateAsyncScope()` per job. Never inject `AppDbContext` or `IResend` (scoped) directly into the constructor.
- **Generating pre-signed URLs on every page load:** Pre-signed URLs should be generated on demand (user clicks download), not eagerly on list render. Otherwise all 800 players generate 50 pre-signed URLs on opening the briefing page.
- **Storing full pre-signed URLs in DB:** Store only the `R2Key` (object path). Generate the pre-signed URL at request time. URLs expire — if stored in DB, they become stale.
- **Applying `listeners` to entire section card for drag:** Use `setActivatorNodeRef` on the grip icon only so text selection, button clicks, and form inputs inside the card don't trigger drag. Without this, any click on the card starts a drag.
- **react-markdown with `dangerouslySetInnerHTML`:** react-markdown renders to React elements — do NOT use `rehype-raw` or `dangerouslySetInnerHTML` here. Commander-authored content should not execute arbitrary HTML.
- **Sending SquadChangeJob for players without user accounts:** Only enqueue the email job if `player.UserId is not null` — players who haven't accepted their invitation have no email login, and sending to them is a broken UX.
- **Missing ContentType header on R2 PUT:** When the browser PUTs to the pre-signed URL, it MUST include `Content-Type` matching what was used to generate the URL signature. Otherwise R2 returns `SignatureDoesNotMatch`. The SPA must set `headers: { 'Content-Type': file.type }` in the fetch/PUT call.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Drag-and-drop sortable list | Custom mousedown/mousemove handlers | @dnd-kit/sortable | Handles touch, keyboard, pointer cancellation, scroll containers, aria-live regions, cross-browser quirks |
| Markdown → HTML render | Custom regex replacer | react-markdown | Handles nested syntax, XSS-safe, extensible with plugins, maintained by unified collective |
| Async email queue | `Task.Run(() => SendEmail())` per request | BackgroundService + Channel | `Task.Run` is fire-and-forget with no backpressure, no graceful shutdown, no error visibility |
| Pre-signed URL generation | Custom HMAC-SHA256 signing | AWSSDK.S3 `GetPreSignedURL` | AWS Signature V4 is complex; the SDK handles credential, region, timestamp, canonical request format |
| Content-type validation | Extension sniffing (`.pdf` check) | Server-side MIME type check against whitelist | Extensions can be spoofed; MIME is set by client (partially trustable) + server-side whitelist blocks clearly wrong types |

**Key insight:** The async email queue with `Channel<T>` is a 20-line implementation that handles backpressure, graceful shutdown, and sequential processing. The alternative (`Task.Run` on each request) has no backpressure — 800 simultaneous fire-and-forget tasks can exhaust the thread pool.

---

## Common Pitfalls

### Pitfall 1: BackgroundService Scoped Service Injection (Critical)

**What goes wrong:** Developer injects `AppDbContext` or `IResend` directly into `NotificationWorker` constructor. ASP.NET Core DI throws `InvalidOperationException: Cannot consume scoped service 'AppDbContext' from singleton 'NotificationWorker'`.

**Why it happens:** `BackgroundService` is registered as a singleton (`AddHostedService`). Scoped services (EF Core `DbContext`, `IResend` if registered as `AddTransient`) cannot be injected into singletons.

**How to avoid:**
```csharp
// WRONG — will throw at startup
public NotificationWorker(AppDbContext db, IResend resend) { ... }

// CORRECT — inject IServiceProvider, create scope per job
public NotificationWorker(IServiceProvider services) { ... }

// In job handler:
await using var scope = _services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var resend = scope.ServiceProvider.GetRequiredService<IResend>();
```

**Warning signs:** `InvalidOperationException` at app startup mentioning "Cannot consume scoped service from singleton".

---

### Pitfall 2: Pre-Signed URL Content-Type Mismatch

**What goes wrong:** SPA requests a pre-signed PUT URL specifying `contentType: "application/pdf"`. R2 embeds this in the signature. The browser then PUTs the file **without** `Content-Type: application/pdf` in the request headers. R2 returns `403 SignatureDoesNotMatch`.

**How to avoid:** When the SPA uploads to the pre-signed PUT URL, always include the Content-Type header:
```typescript
// web/src/hooks/useFileUpload.ts
await fetch(presignedPutUrl, {
  method: 'PUT',
  headers: {
    'Content-Type': file.type,  // MUST match what was used to generate URL
  },
  body: file,
});
```

**Warning signs:** Browser console shows `403 Forbidden` from `*.r2.cloudflarestorage.com`; R2 error XML contains `<Code>SignatureDoesNotMatch</Code>`.

---

### Pitfall 3: @dnd-kit Drag Activates on Form Input Clicks

**What goes wrong:** Section cards contain input fields (title, textarea). Without `setActivatorNodeRef`, the default `listeners` spread on the card root intercepts `pointerdown` events on inputs. The drag starts when the commander tries to click into the title field.

**How to avoid:** Use `setActivatorNodeRef` from `useSortable` to attach drag listeners ONLY to the grip icon element. The grip icon becomes the sole drag activation point.

```typescript
const { setNodeRef, setActivatorNodeRef, listeners, attributes, ...rest } = useSortable({ id });

// Card root — receives the droppable/ref only (no listeners)
<div ref={setNodeRef}>
  {/* Grip icon — ONLY this triggers drag */}
  <button ref={setActivatorNodeRef} {...listeners} {...attributes}>⠿</button>
  {/* Inputs, textareas, buttons inside here work normally */}
  <Input ... />
</div>
```

---

### Pitfall 4: Resend Batch Limit (100 messages/call, 50 recipients/message)

**What goes wrong:** Developer calls `resend.EmailBatchAsync` with 800 individual `EmailMessage` objects in a single call. Resend API returns `422 Unprocessable Entity` — batch limit is 100 messages per call.

**How to avoid:** Chunk the recipient list:
```csharp
var chunks = recipients.Chunk(100);  // .NET 6+ built-in
foreach (var chunk in chunks)
{
    var messages = chunk.Select(email => new EmailMessage { To = { email }, ... }).ToList();
    await resend.EmailBatchAsync(messages);
    await Task.Delay(200, ct);  // small delay between batches
}
```

**Warning signs:** `422 Unprocessable Entity` from Resend API when sending to large recipient lists.

---

### Pitfall 5: Stale Order Values After Reorder

**What goes wrong:** `InfoSection.Order` values are stored as `0, 1, 2, 3...`. After multiple reorders, the order becomes `0, 3, 1, 2...` (from partial updates). The section list renders in unexpected order.

**How to avoid:** The reorder endpoint **re-assigns order values 0..N** for all provided section IDs. Never do partial order updates — always update all sections in the event:
```csharp
// WRONG — partial update
section.Order = newIndex;  // leaves other sections with stale values

// CORRECT — full reassignment
for (var i = 0; i < orderedIds.Count; i++)
    sections[i].Order = i;  // all sections get fresh 0..N values
```

---

### Pitfall 6: R2 Presigned URL Custom Domain Limitation

**What goes wrong:** Team sets up a custom domain (`files.yourdomain.com`) for R2 and generates pre-signed URLs using the custom domain as the endpoint. R2 returns `403 Forbidden` — presigned URLs only work with the S3 API domain.

**How to avoid:** Per official Cloudflare docs: "Presigned URLs work with the S3 API domain (`<ACCOUNT_ID>.r2.cloudflarestorage.com`) and **cannot** be used with custom domains." Always use `ForcePathStyle = true` and `ServiceURL = https://{AccountId}.r2.cloudflarestorage.com`.

---

### Pitfall 7: InfoSection Duplication — Phase 2 Forward-Compat Contract

**What goes wrong:** Phase 2 already wires `DuplicateEventRequest.CopyInfoSectionIds: Guid[]` at the API layer. Phase 3 must implement the actual copy logic inside `EventService.DuplicateEventAsync`. If the Phase 3 planner misses this, info sections are silently not copied on duplication.

**How to avoid:** `EventService.DuplicateEventAsync` must implement the commented-out copy logic from Phase 2's placeholder:
```csharp
// Phase 2 placeholder (to be implemented in Phase 3):
// newEvent.InfoSections = CopySelectedSections(source, request.CopyInfoSectionIds);

// Phase 3 implementation:
if (request.CopyInfoSectionIds.Any())
{
    var sectionsToClone = await _db.InfoSections
        .Include(s => s.Attachments)
        .Where(s => request.CopyInfoSectionIds.Contains(s.Id) && s.EventId == sourceEventId)
        .ToListAsync();

    var copiedSections = sectionsToClone.Select(s => new InfoSection
    {
        Title = s.Title,
        BodyMarkdown = s.BodyMarkdown,
        Order = s.Order,
        Attachments = s.Attachments.Select(a => new InfoSectionAttachment
        {
            R2Key = a.R2Key,    // shares R2 object — no copy needed
            FriendlyName = a.FriendlyName,
            ContentType = a.ContentType,
            FileSizeBytes = a.FileSizeBytes
        }).ToList()
    }).ToList();

    newEvent.InfoSections = copiedSections;
}
```

---

## Code Examples

### R2 Pre-Signed PUT URL (AWSSDK.S3)

```csharp
// Source: Official Cloudflare R2 presigned URL docs + AWSSDK.S3 STACK.md pattern
// Infrastructure/Storage/FileService.cs

public string GenerateUploadUrl(string r2Key, string contentType, int expiryMinutes = 15)
{
    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucketName,
        Key = r2Key,
        Verb = HttpVerb.PUT,
        Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
        ContentType = contentType  // baked into the signature — browser MUST match
    };
    return _s3Client.GetPreSignedURL(request);  // synchronous, no async variant
}

public string GenerateDownloadUrl(string r2Key, int expiryHours = 1)
{
    var request = new GetPreSignedUrlRequest
    {
        BucketName = _bucketName,
        Key = r2Key,
        Verb = HttpVerb.GET,
        Expires = DateTime.UtcNow.AddHours(expiryHours)
    };
    return _s3Client.GetPreSignedURL(request);
}
```

### Resend Batch Send (.NET)

```csharp
// Source: Official Resend API reference — Send Batch Emails (.NET example)
// Used inside NotificationWorker

var messages = recipientEmails.Select(email => new EmailMessage
{
    From = "MilSim Platform <noreply@yourdomain.com>",
    To = { email },
    Subject = blast.Subject,
    HtmlBody = blastHtml
}).ToList();

// Up to 100 messages per call
var result = await resend.EmailBatchAsync(messages);
// result.Content = list of { id: Guid } per sent message
```

### @dnd-kit/sortable Minimal Setup

```typescript
// Source: @dnd-kit/sortable official docs (docs.dndkit.com/presets/sortable)
import { DndContext, closestCenter, PointerSensor, useSensor, useSensors } from '@dnd-kit/core';
import { SortableContext, arrayMove, verticalListSortingStrategy } from '@dnd-kit/sortable';

// onDragEnd — called once on drop
function handleDragEnd({ active, over }: DragEndEvent) {
  if (!over || active.id === over.id) return;
  setItems(prev => {
    const from = prev.indexOf(active.id as string);
    const to = prev.indexOf(over.id as string);
    return arrayMove(prev, from, to);
  });
}
```

### react-markdown Preview Tab

```typescript
// Source: react-markdown GitHub README (remarkjs/react-markdown)
import Markdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

<Markdown remarkPlugins={[remarkGfm]}>
  {bodyMarkdown}
</Markdown>
```

### Testing File Upload Endpoint (IFormFile multipart)

```csharp
// Source: Microsoft integration test docs (aspnetcore-10.0) — established Phase 2 pattern
[Fact]
[Trait("Category", "CONT_Attachments")]
public async Task UploadFile_Over10MB_Returns400()
{
    // Arrange
    var largeFile = new byte[11 * 1024 * 1024]; // 11 MB
    var content = new ByteArrayContent(largeFile);
    content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");

    using var form = new MultipartFormDataContent();
    form.Add(content, "file", "large.pdf");

    _client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);

    // Act
    var response = await _client.PostAsync(
        $"/api/events/{_eventId}/info-sections/{_sectionId}/attachments/upload-url",
        form);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `react-beautiful-dnd` (Atlassian) | `@dnd-kit/sortable` | 2023 (rbd deprecated) | @dnd-kit is the maintained community standard for React DnD |
| Proxied file uploads (API receives bytes) | Direct-to-R2 pre-signed PUT | 2020+ (S3 presigned pattern) | Eliminates API server from file transfer path; egress charges avoided |
| Synchronous email per request | BackgroundService + Channel queue | ASP.NET Core 3.1+ | Decouples API response time from email delivery; enables blast without timeout |
| `marked.js` + `dangerouslySetInnerHTML` | `react-markdown` (virtual DOM, XSS-safe) | 2019+ | No XSS risk; React reconciliation instead of innerHTML replacement |
| `BackgroundService` without scope | `IServiceProvider.CreateAsyncScope()` per job | ASP.NET Core 3.1+ (documented pattern) | Required for scoped service access from singleton lifetime |

**Deprecated/outdated:**
- `react-beautiful-dnd`: Maintenance-only, not updated for React 18+. @dnd-kit is the successor.
- `Task.Run(() => SendEmailAsync())` for fire-and-forget: No backpressure, no graceful shutdown, thread pool exhaustion risk. Use Channel-based BackgroundService.
- Storing pre-signed URLs in DB: URLs expire; store R2 key only, generate URL on demand.

---

## Open Questions

1. **Resend free tier sufficient for 800-recipient blasts?**
   - What we know: Resend free tier = 3,000 emails/month. 800 recipients × N blasts/month. At 3 blasts/month = 2,400 emails (under limit). At 4 blasts = 3,200 (over).
   - What's unclear: How many blasts per month are typical for an airsoft event
   - Recommendation: Accept free tier for now; monitor usage. Paid tier starts at $20/month for 50,000 emails.

2. **R2 object key structure and cleanup**
   - What we know: Objects are stored at `events/{eventId}/sections/{sectionId}/files/{id}/{filename}`
   - What's unclear: If an attachment is deleted from the DB, R2 object remains (orphaned). No cleanup trigger is planned.
   - Recommendation: Accept orphaned objects for v1. Add a cleanup job in Phase 4 or v2. R2 storage cost is negligible for this scale.

3. **Markdown preview Tailwind typography styles**
   - What we know: `react-markdown` renders standard HTML elements (`p`, `h1`, `ul`, etc.). Without styling, these are unstyled in a Tailwind reset environment.
   - What's unclear: Whether `@tailwindcss/typography` is already installed, or if a custom prose class is needed.
   - Recommendation: Install `@tailwindcss/typography` and apply `prose prose-sm dark:prose-invert` to the preview wrapper. If adding the plugin causes bundle size concern, use minimal custom styles for `h1-h6`, `ul`, `ol`, `blockquote`.

---

## Validation Architecture

> `workflow.nyquist_validation` is `true` in `.planning/config.json` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9+ (established Phases 1/2) |
| Config file | `tests/MilsimPlanning.Api.Tests/MilsimPlanning.Api.Tests.csproj` |
| Quick run command | `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` |
| Full suite command | `dotnet test tests/` |
| React tests | `pnpm test --run` (Vitest) |

### API Endpoints Needing Integration Tests

| Endpoint | Method | Auth Required | Test Category |
|----------|--------|---------------|---------------|
| `/api/events/{id}/info-sections` | POST | FactionCommander + scope | `CONT_Sections` |
| `/api/events/{id}/info-sections/{sid}` | PUT | FactionCommander + scope | `CONT_Sections` |
| `/api/events/{id}/info-sections/{sid}` | DELETE | FactionCommander + scope | `CONT_Sections` |
| `/api/events/{id}/info-sections/reorder` | PATCH | FactionCommander + scope | `CONT_Reorder` |
| `/api/events/{id}/info-sections/{sid}/attachments/upload-url` | POST | FactionCommander + scope | `CONT_Attachments` |
| `/api/events/{id}/info-sections/{sid}/attachments/{aid}/download-url` | GET | RequirePlayer + scope | `CONT_Attachments` |
| `/api/events/{id}/map-resources` | POST | FactionCommander + scope | `MAPS_Resources` |
| `/api/events/{id}/map-resources/{rid}/download-url` | GET | RequirePlayer + scope | `MAPS_Resources` |
| `/api/events/{id}/notification-blasts` | POST | FactionCommander + scope | `NOTF_Blast` |
| `/api/events/{id}/notification-blasts` | GET | RequirePlayer + scope | `NOTF_Blast` |
| `/api/event-players/{id}/squad` (modified) | PUT | FactionCommander + scope | `NOTF_SquadChange` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CONT-01 | Create info section returns 201 with new section | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| CONT-01 | Create info section without title returns 400 | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| CONT-01 | Player cannot create info section (403) | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| CONT-02 | Section body supports markdown (stored as-is, not rendered) | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| CONT-03 | Upload-url endpoint rejects file > 10 MB | Integration | `dotnet test --filter "Category=CONT_Attachments"` | ❌ Wave 0 |
| CONT-03 | Upload-url endpoint rejects disallowed MIME type | Integration | `dotnet test --filter "Category=CONT_Attachments"` | ❌ Wave 0 |
| CONT-03 | Download-url returns pre-signed GET URL for valid attachment | Integration (mock IAmazonS3) | `dotnet test --filter "Category=CONT_Attachments"` | ❌ Wave 0 |
| CONT-04 | Reorder endpoint reorders sections; GET returns new order | Integration | `dotnet test --filter "Category=CONT_Reorder"` | ❌ Wave 0 |
| CONT-04 | Reorder with IDs not belonging to event returns 403/400 | Integration | `dotnet test --filter "Category=CONT_Reorder"` | ❌ Wave 0 |
| CONT-05 | Edit section title + body; GET returns updated values | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| CONT-05 | Delete section removes it from GET response | Integration | `dotnet test --filter "Category=CONT_Sections"` | ❌ Wave 0 |
| MAPS-01 | Create map resource with external URL returns 201 | Integration | `dotnet test --filter "Category=MAPS_Resources"` | ❌ Wave 0 |
| MAPS-02 | Map resource instructions field stored and returned | Integration | `dotnet test --filter "Category=MAPS_Resources"` | ❌ Wave 0 |
| MAPS-03 | Map resource upload-url accepts PDF/JPEG/PNG/KMZ MIME types | Integration | `dotnet test --filter "Category=MAPS_Resources"` | ❌ Wave 0 |
| MAPS-04 | Player can GET download-url for map file | Integration | `dotnet test --filter "Category=MAPS_Resources"` | ❌ Wave 0 |
| MAPS-05 | Download-url is a time-limited pre-signed URL (not public) | Unit (mock IAmazonS3 verify call params) | `dotnet test --filter "Category=Unit"` | ❌ Wave 0 |
| NOTF-01 | Send blast returns 202 Accepted | Integration (mock INotificationQueue) | `dotnet test --filter "Category=NOTF_Blast"` | ❌ Wave 0 |
| NOTF-01 | Send blast enqueues job with correct recipient list | Integration (mock INotificationQueue + verify) | `dotnet test --filter "Category=NOTF_Blast"` | ❌ Wave 0 |
| NOTF-01 | GET blast log returns past blasts for event | Integration | `dotnet test --filter "Category=NOTF_Blast"` | ❌ Wave 0 |
| NOTF-02 | Assign squad enqueues SquadChangeJob with old+new assignment | Integration (mock INotificationQueue) | `dotnet test --filter "Category=NOTF_SquadChange"` | ❌ Wave 0 |
| NOTF-02 | Assign squad for player without UserId does NOT enqueue | Integration (mock INotificationQueue verify NOT called) | `dotnet test --filter "Category=NOTF_SquadChange"` | ❌ Wave 0 |
| NOTF-04 | NotificationWorker calls IResend.EmailBatchAsync (not raw SMTP) | Unit (mock IResend) | `dotnet test --filter "Category=Unit"` | ❌ Wave 0 |
| NOTF-05 | NotificationWorker chunks 800 recipients into batches of ≤100 | Unit | `dotnet test --filter "Category=Unit"` | ❌ Wave 0 |

### How to Test Pre-Signed URL Generation (Mock vs Real R2)

**Strategy:** Mock `IAmazonS3` in integration tests. Verify that `GetPreSignedURL` is called with correct parameters — do not make real R2 network calls in tests.

```csharp
// tests/MilsimPlanning.Api.Tests/Content/AttachmentTests.cs
public class AttachmentTests : IAsyncLifetime, IClassFixture<PostgreSqlFixture>
{
    private readonly Mock<IAmazonS3> _mockS3 = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    // Replace real IAmazonS3 with mock
                    services.RemoveAll<IAmazonS3>();
                    services.AddSingleton<IAmazonS3>(_mockS3.Object);

                    // Testcontainers PostgreSQL
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));
                }));

        _client = factory.CreateClient();

        // Setup mock to return predictable URL
        _mockS3.Setup(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()))
               .Returns("https://mock-presigned-url.r2.cloudflarestorage.com/test-key?X-Amz-...");
    }

    [Fact]
    [Trait("Category", "CONT_Attachments")]
    public async Task GetDownloadUrl_ForValidAttachment_ReturnsMockPresignedUrl()
    {
        // Arrange — seed attachment record directly in DB
        var attachment = await SeedAttachment(eventId: _eventId, r2Key: "events/test/file.pdf");

        _client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);

        // Act
        var response = await _client.GetAsync(
            $"/api/events/{_eventId}/info-sections/{_sectionId}/attachments/{attachment.Id}/download-url");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DownloadUrlResponse>();
        result!.Url.Should().Contain("mock-presigned-url");

        // Verify S3 client called with GET verb + correct key
        _mockS3.Verify(s => s.GetPreSignedURL(It.Is<GetPreSignedUrlRequest>(r =>
            r.Verb == HttpVerb.GET &&
            r.Key == "events/test/file.pdf"
        )), Times.Once);
    }

    [Fact]
    [Trait("Category", "CONT_Attachments")]
    public async Task GetUploadUrl_WithDisallowedMimeType_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);

        var response = await _client.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections/{_sectionId}/attachments/upload-url",
            new { ContentType = "application/x-msdownload", FileName = "evil.exe", FileSizeBytes = 1024 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _mockS3.Verify(s => s.GetPreSignedURL(It.IsAny<GetPreSignedUrlRequest>()), Times.Never);
    }
}
```

### How to Test BackgroundService Email Queue (Without Real R2 or Resend)

**Strategy:** Test `NotificationWorker` as a unit by injecting mock `IResend` via `IServiceProvider`. Test `NotificationQueue` by enqueuing and verifying the channel produces items.

```csharp
// tests/MilsimPlanning.Api.Tests/Notifications/NotificationWorkerTests.cs
[Fact]
[Trait("Category", "Unit")]
public async Task ProcessBlastAsync_With250Recipients_Sends3BatchCalls()
{
    // Arrange
    var mockResend = new Mock<IResend>();
    var capturedMessages = new List<IReadOnlyList<EmailMessage>>();

    mockResend
        .Setup(r => r.EmailBatchAsync(It.IsAny<IReadOnlyList<EmailMessage>>()))
        .Callback<IReadOnlyList<EmailMessage>>(msgs => capturedMessages.Add(msgs))
        .ReturnsAsync(new ResendResponse<IReadOnlyList<object>>(new List<object>()));

    var services = new ServiceCollection()
        .AddScoped<IResend>(_ => mockResend.Object)
        .AddScoped<AppDbContext>(/* test db */)
        .BuildServiceProvider();

    var queue = new NotificationQueue();
    var worker = new NotificationWorker(queue, services, NullLogger<NotificationWorker>.Instance);

    var recipients = Enumerable.Range(0, 250).Select(i => $"player{i}@test.com").ToList();
    var job = new BlastNotificationJob(Guid.NewGuid(), Guid.NewGuid(), "Test", "Body", recipients);
    await queue.EnqueueAsync(job);

    // Act
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await worker.StartAsync(cts.Token);
    await Task.Delay(200);  // allow processing
    cts.Cancel();

    // Assert — 250 recipients → 3 batches (100 + 100 + 50)
    capturedMessages.Should().HaveCount(3);
    capturedMessages[0].Should().HaveCount(100);
    capturedMessages[1].Should().HaveCount(100);
    capturedMessages[2].Should().HaveCount(50);
}

[Fact]
[Trait("Category", "NOTF_SquadChange")]
public async Task AssignSquad_ForPlayerWithUserId_EnqueuesSquadChangeJob()
{
    // Integration test — mock INotificationQueue, verify enqueue called
    var mockQueue = new Mock<INotificationQueue>();
    var factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationQueue>();
            services.AddSingleton<INotificationQueue>(mockQueue.Object);
            // Testcontainers DB setup...
        }));

    var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", _commanderToken);

    // Seed player WITH UserId
    var player = await SeedPlayer(email: "player@test.com", userId: "existing-user-id");

    var response = await client.PutAsJsonAsync(
        $"/api/event-players/{player.Id}/squad",
        new { SquadId = _newSquadId });

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    // Verify SquadChangeJob was enqueued with old+new assignment
    mockQueue.Verify(q => q.EnqueueAsync(
        It.Is<SquadChangeJob>(j =>
            j.RecipientEmail == "player@test.com" &&
            j.NewSquadName != null
        ),
        It.IsAny<CancellationToken>()
    ), Times.Once);
}
```

### React Component Testing Strategy

**Framework:** Vitest + @testing-library/react + MSW (established Phases 1/2)

| Component | Test Approach | What to Test |
|-----------|---------------|--------------|
| `SectionEditor` | RTL — no MSW needed (pure controlled component) | Title empty → Save disabled; Edit/Preview tab switch; textarea updates body state; Save calls onSave prop with correct values |
| `SectionList` (DnD) | RTL + MSW mock | Sections render in order; Save immediately on drop calls PATCH endpoint; keyboard drag (Tab → Space → Arrow → Space) reorders |
| `SectionAttachments` | RTL + MSW + file mock | File > 10MB shows inline error; File with bad type shows inline error; Valid file triggers upload-url request; download link rendered after confirm |
| `NotificationBlastPage` | RTL + MSW mock | Subject + body fields present; Send button calls POST; Toast shown on success; Blast log renders past blasts |
| `MapResourceCard` | RTL + MSW mock | External URL renders as link; Download button triggers download-url GET; Instructions rendered as markdown |

```typescript
// Source: @testing-library/react + Vitest pattern (established Phase 2)
// web/src/tests/SectionEditor.test.tsx

test("Save button disabled when title is empty", () => {
  render(
    <SectionEditor
      section={{ id: "s1", title: "", bodyMarkdown: "" }}
      onSave={vi.fn()}
    />
  );
  expect(screen.getByRole("button", { name: /save/i })).toBeDisabled();
});

test("Edit/Preview tab switch renders markdown in preview", async () => {
  render(
    <SectionEditor
      section={{ id: "s1", title: "Test", bodyMarkdown: "**bold text**" }}
      onSave={vi.fn()}
    />
  );
  await userEvent.click(screen.getByRole("tab", { name: /preview/i }));
  expect(screen.getByText("bold text")).toBeInTheDocument(); // rendered, not raw **
});

test("File over 10MB shows inline error without calling API", async () => {
  server.use(
    http.post("/api/events/:eventId/info-sections/:sectionId/attachments/upload-url",
      () => HttpResponse.json({ error: "too large" }, { status: 400 }))
  );

  render(<SectionAttachments eventId="e1" sectionId="s1" />);

  // Simulate dropping a 11MB file
  const largeFile = new File([new ArrayBuffer(11 * 1024 * 1024)], "large.pdf", {
    type: "application/pdf"
  });
  await userEvent.upload(screen.getByLabelText(/upload/i), largeFile);

  expect(await screen.findByText(/10 MB or smaller/i)).toBeInTheDocument();
});
```

### Sampling Rate

- **Per task commit:** `dotnet test tests/MilsimPlanning.Api.Tests/ --filter "Category=Unit"` + `pnpm test --run`
- **Per wave merge:** `dotnet test tests/` (full suite including Testcontainers)
- **Phase gate:** Full suite green + all CONT/MAPS/NOTF integration tests passing before `/gsd-verify-work`

### Wave 0 Gaps (Files That Must Exist Before Implementation)

- [ ] `tests/MilsimPlanning.Api.Tests/Content/InfoSectionTests.cs` — covers CONT-01 through CONT-05
- [ ] `tests/MilsimPlanning.Api.Tests/Content/AttachmentTests.cs` — covers CONT-03, MAPS-03, MAPS-04, MAPS-05
- [ ] `tests/MilsimPlanning.Api.Tests/Maps/MapResourceTests.cs` — covers MAPS-01, MAPS-02
- [ ] `tests/MilsimPlanning.Api.Tests/Notifications/NotificationBlastTests.cs` — covers NOTF-01, NOTF-05
- [ ] `tests/MilsimPlanning.Api.Tests/Notifications/NotificationWorkerTests.cs` — unit tests for NOTF-04, NOTF-05, batch chunking
- [ ] `tests/MilsimPlanning.Api.Tests/Notifications/SquadChangeNotificationTests.cs` — covers NOTF-02
- [ ] `web/src/tests/SectionEditor.test.tsx` — React unit tests for CONT-01, CONT-02
- [ ] `web/src/tests/SectionAttachments.test.tsx` — React tests for CONT-03 client-side validation
- [ ] `web/src/tests/SectionList.dnd.test.tsx` — React tests for CONT-04 drag-and-drop

---

## Sources

### Primary (HIGH confidence)
- `developers.cloudflare.com/r2/api/s3/presigned-urls/` — R2 presigned URL docs; confirmed max 7 days expiry; confirmed custom domain limitation; GET/PUT/HEAD/DELETE supported; POST not supported — accessed 2026-03-13
- `learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0` — BackgroundService, queued background tasks, `Channel<T>` queue pattern, scoped service scope creation — accessed 2026-03-13
- `resend.com/docs/send-with-dotnet` — Resend .NET SDK: `IResend`, `EmailSendAsync`, DI setup — accessed 2026-03-13
- `resend.com/docs/api-reference/emails/send-batch-emails` — **Batch limit: 100 messages per call; 50 recipients per message** (`.NET` `EmailBatchAsync` code example confirmed) — accessed 2026-03-13
- `docs.dndkit.com/presets/sortable` — @dnd-kit/sortable: `useSortable`, `SortableContext`, `arrayMove`, `setActivatorNodeRef` for grip handle — accessed 2026-03-13
- `github.com/remarkjs/react-markdown` — react-markdown v10.1.0 (latest March 2025); ESM-only; `remarkPlugins={[remarkGfm]}`; XSS-safe by default — accessed 2026-03-13
- `docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html` — AWS S3 presigned URL: max 7 days (IAM user credentials); expiry behavior; `SignatureDoesNotMatch` causes — accessed 2026-03-13

### Secondary (MEDIUM confidence)
- `.planning/research/STACK.md` — AWSSDK.S3 3.7.x + `IAmazonS3` + `GetPreSignedURL` pattern; confirmed compatible with Cloudflare R2 via `ForcePathStyle=true`
- `.planning/phases/01-foundation/01-RESEARCH.md` — Testcontainers + WebApplicationFactory + `IClassFixture<PostgreSqlFixture>` integration test pattern
- `.planning/phases/02-commander-workflow/02-RESEARCH.md` — `MultipartFormDataContent` IFormFile test pattern; MSW + @testing-library/react component test pattern

### Tertiary (LOW confidence — none)
No unverified WebSearch-only claims in this document.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — @dnd-kit/sortable, react-markdown, AWSSDK.S3, Resend SDK all verified from official docs
- Architecture: HIGH — BackgroundService pattern from official Microsoft docs; R2 presigned URL from official Cloudflare docs; scoped-service-from-singleton pattern is official Microsoft guidance
- Pitfalls: HIGH — Resend 100-message batch limit confirmed from official API reference; R2 custom domain limitation confirmed from official Cloudflare docs; @dnd-kit `setActivatorNodeRef` pattern confirmed from official docs
- Test approach: HIGH — extends established Phases 1/2 patterns; mock IAmazonS3/INotificationQueue approach follows Moq + WebApplicationFactory pattern already in use

**Research date:** 2026-03-13
**Valid until:** 2026-06-13 (stable APIs; R2 and Resend limits are stable; 90-day validity)
