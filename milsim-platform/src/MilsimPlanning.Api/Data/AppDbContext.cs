using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data.Entities;

namespace MilsimPlanning.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventMembership> EventMemberships => Set<EventMembership>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    // Phase 2 DbSets
    public DbSet<Faction> Factions => Set<Faction>();
    public DbSet<Platoon> Platoons => Set<Platoon>();
    public DbSet<Squad> Squads => Set<Squad>();
    public DbSet<EventPlayer> EventPlayers => Set<EventPlayer>();

    // Phase 3 DbSets
    public DbSet<InfoSection> InfoSections => Set<InfoSection>();
    public DbSet<InfoSectionAttachment> InfoSectionAttachments => Set<InfoSectionAttachment>();
    public DbSet<MapResource> MapResources => Set<MapResource>();
    public DbSet<NotificationBlast> NotificationBlasts => Set<NotificationBlast>();

    // Phase 4 DbSets
    public DbSet<RosterChangeRequest> RosterChangeRequests => Set<RosterChangeRequest>();

    // Phase 5 (Briefing Board) DbSets
    public DbSet<Briefing> Briefings => Set<Briefing>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // MUST be first — sets up Identity tables

        // UserProfile 1:1 with AppUser
        builder.Entity<UserProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.Profile)
            .HasForeignKey<UserProfile>(p => p.UserId);

        // EventMembership composite unique index (one membership per user per event)
        builder.Entity<EventMembership>()
            .HasIndex(m => new { m.UserId, m.EventId })
            .IsUnique();

        // EventMembership FK relations
        builder.Entity<EventMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.EventMemberships)
            .HasForeignKey(m => m.UserId);

        // EventMembership → Event (Event no longer has Memberships nav, configure explicitly)
        builder.Entity<EventMembership>()
            .HasOne(m => m.Event)
            .WithMany()
            .HasForeignKey(m => m.EventId);

        // MagicLinkToken index on (UserId, TokenHash) for fast lookup
        builder.Entity<MagicLinkToken>()
            .HasIndex(t => new { t.UserId, t.TokenHash });

        builder.Entity<MagicLinkToken>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId);

        // === Phase 2 Configuration ===

        // Event → Faction (1:1 in v1; Faction owns the FK to Event)
        builder.Entity<Faction>()
            .HasOne(f => f.Event)
            .WithOne(e => e.Faction)
            .HasForeignKey<Faction>(f => f.EventId);

        // EventPlayer: natural key unique index
        builder.Entity<EventPlayer>()
            .HasIndex(ep => new { ep.EventId, ep.Email })
            .IsUnique();

        // EventPlayer → Platoon/Squad (optional FKs — nullable)
        builder.Entity<EventPlayer>()
            .HasOne(ep => ep.Platoon)
            .WithMany(p => p.Players)
            .HasForeignKey(ep => ep.PlatoonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<EventPlayer>()
            .HasOne(ep => ep.Squad)
            .WithMany(s => s.Players)
            .HasForeignKey(ep => ep.SquadId)
            .OnDelete(DeleteBehavior.SetNull);

        // Platoon ordering
        builder.Entity<Platoon>()
            .HasIndex(p => new { p.FactionId, p.Order });

        // Squad ordering
        builder.Entity<Squad>()
            .HasIndex(s => new { s.PlatoonId, s.Order });

        // === Phase 3 Configuration ===

        // InfoSection ordering per event
        builder.Entity<InfoSection>()
            .HasIndex(s => new { s.EventId, s.Order });

        // MapResource ordering per event
        builder.Entity<MapResource>()
            .HasIndex(r => new { r.EventId, r.Order });

        // === Phase 4 Configuration ===

        // RosterChangeRequest: cascade delete from EventPlayer (Pitfall 3 — orphan prevention)
        builder.Entity<RosterChangeRequest>()
            .HasOne(r => r.EventPlayer)
            .WithMany()
            .HasForeignKey(r => r.EventPlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // RosterChangeRequest: cascade delete from Event
        builder.Entity<RosterChangeRequest>()
            .HasOne(r => r.Event)
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // RosterChangeRequest indexes: fast lookup for "player's pending request"
        builder.Entity<RosterChangeRequest>()
            .HasIndex(r => new { r.EventPlayerId, r.Status });

        // RosterChangeRequest indexes: fast lookup for "all pending for event"
        builder.Entity<RosterChangeRequest>()
            .HasIndex(r => new { r.EventId, r.Status });

        // === Phase 5 (Briefing Board) Configuration ===

        // Briefing: channelIdentifier must be unique across all briefings
        builder.Entity<Briefing>()
            .HasIndex(b => b.ChannelIdentifier)
            .IsUnique();
    }
}
