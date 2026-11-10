using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SeatCharter.Api.DTOs;
using SeatCharter.Api.Helpers.Attributes;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.Data;

public class ApplicationDbContext : IdentityDbContext<User>
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor? httpContextAccessor = null)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Set by DbSeeder so seed rows don't produce audit noise.</summary>
    public bool IsSeeding { get; set; }

    // ── LAYER 1: PLATFORM ─────────────────────────────────────────────────
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CampusZone> CampusZones => Set<CampusZone>();

    // ── LAYER 2: MASTER DATA ──────────────────────────────────────────────
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Device> Devices => Set<Device>();

    // ── LAYER 3: TRANSACTIONS ─────────────────────────────────────────────
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<PresenceCheck> PresenceChecks => Set<PresenceCheck>();

    // ── LAYER 4: SUPPORTING ───────────────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity tables into dbo
        builder.Entity<User>().ToTable("Users", "dbo");
        builder.Entity<IdentityRole>().ToTable("Roles", "dbo");
        builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "dbo");
        builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "dbo");
        builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", "dbo");
        builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens", "dbo");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "dbo");

        builder.Entity<User>(e =>
        {
            e.HasIndex(x => x.MatricNo).IsUnique().HasFilter("\"MatricNo\" IS NOT NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.TokenHash });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CampusZone>(e => e.HasQueryFilter(x => x.DeletedAt == null));

        builder.Entity<Course>(e =>
        {
            e.HasOne(x => x.Lecturer).WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.JoinCode).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasIndex(x => new { x.LecturerId, x.Code }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        builder.Entity<Enrolment>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.StudentId });
            e.HasOne(x => x.Course).WithMany(c => c.Enrolments).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.StudentId);
        });

        builder.Entity<Schedule>(e =>
        {
            e.HasOne(x => x.Course).WithMany(c => c.Schedules).HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        builder.Entity<Device>(e =>
        {
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => x.PublicKey);
            e.HasIndex(x => x.HardwareId);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        builder.Entity<Session>(e =>
        {
            e.HasOne(x => x.Course).WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CourseId, x.Status });
            e.HasIndex(x => x.Status);
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        builder.Entity<CheckIn>(e =>
        {
            e.HasOne(x => x.Session).WithMany(s => s.CheckIns).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            // BR-5 / BR-6 backstops. Rejected attempts are kept for the lecturer, so they're excluded from uniqueness.
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique().HasFilter("\"Status\" <> 'Rejected'");
            e.HasIndex(x => new { x.SessionId, x.DeviceId }).IsUnique().HasFilter("\"Status\" <> 'Rejected' AND \"DeviceId\" IS NOT NULL");
            e.HasIndex(x => x.StudentId);
        });

        builder.Entity<PresenceCheck>(e =>
        {
            e.HasOne(x => x.CheckIn).WithMany(c => c.PresenceChecks).HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CheckInId, x.Kind });
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.At);
        });

        // Global enum → string converter (runs last so it sees every property)
        foreach (var et in builder.Model.GetEntityTypes())
        foreach (var p in et.GetProperties())
        {
            var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            if (!t.IsEnum) continue;
            p.SetValueConverter((ValueConverter)Activator.CreateInstance(typeof(EnumToStringConverter<>).MakeGenericType(t))!);
        }
    }

    // ── Audit + UpdatedAt stamping ────────────────────────────────────────

    private static readonly HashSet<string> AuditSkippedProperties =
    [
        "CreatedAt", "UpdatedAt", "DeletedAt", "ConcurrencyStamp",
        // Never copy secrets into the audit trail
        "PasswordHash", "SecurityStamp", "MfaSecret", "QrSecret"
    ];

    private static readonly JsonSerializerOptions AuditJson = new() { WriteIndented = false };

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BeforeSave();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BeforeSave();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void BeforeSave()
    {
        var now = DateTime.UtcNow;
        var entries = ChangeTracker.Entries().ToList();

        foreach (var entry in entries.Where(e => e.State == EntityState.Modified))
        {
            if (entry.Metadata.FindProperty("UpdatedAt") is not null)
                entry.Property("UpdatedAt").CurrentValue = now;
        }

        var actorId = CurrentActorId();
        if (IsSeeding || actorId is null) return;   // jobs and seeding: no HTTP user → no row-level audit

        foreach (var entry in entries)
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity.GetType().GetCustomAttribute<AuditableAttribute>() is null) continue;

            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ => IsSoftDelete(entry) ? "Delete" : "Update"
            };

            var (oldValues, newValues) = Snapshot(entry);
            if (action == "Update" && newValues.Count == 0) continue;   // only skipped columns changed

            AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entry.Property("Id").CurrentValue?.ToString() ?? string.Empty,
                At = now,
                DataJson = JsonSerializer.Serialize(new { old = oldValues, @new = newValues }, AuditJson)
            });
        }
    }

    private static bool IsSoftDelete(EntityEntry entry)
    {
        if (entry.Metadata.FindProperty("DeletedAt") is null) return false;
        var p = entry.Property("DeletedAt");
        return p.IsModified && p.OriginalValue is null && p.CurrentValue is not null;
    }

    private static (Dictionary<string, object?> Old, Dictionary<string, object?> New) Snapshot(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            var name = p.Metadata.Name;
            if (AuditSkippedProperties.Contains(name)) continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = p.CurrentValue;
                    break;
                case EntityState.Deleted:
                    oldValues[name] = p.OriginalValue;
                    break;
                case EntityState.Modified when p.IsModified && !Equals(p.OriginalValue, p.CurrentValue):
                    oldValues[name] = p.OriginalValue;
                    newValues[name] = p.CurrentValue;
                    break;
            }
        }
        return (oldValues, newValues);
    }

    private string? CurrentActorId()
    {
        var services = _httpContextAccessor?.HttpContext?.RequestServices;
        return services?.GetService<UserInfoToken>()?.UserId;
    }
}
