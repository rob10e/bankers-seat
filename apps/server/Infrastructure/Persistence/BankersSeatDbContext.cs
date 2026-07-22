using BankersSeat.Server.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankersSeat.Server.Infrastructure.Persistence;

public sealed class BankersSeatDbContext : DbContext
{
    public BankersSeatDbContext(DbContextOptions<BankersSeatDbContext> options)
        : base(options) { }

    public DbSet<GameSessionEntity> GameSessions => Set<GameSessionEntity>();

    public DbSet<TemplateSnapshotEntity> TemplateSnapshots => Set<TemplateSnapshotEntity>();

    public DbSet<ParticipantEntity> Participants => Set<ParticipantEntity>();

    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();

    public DbSet<IdempotencyRecordEntity> IdempotencyRecords => Set<IdempotencyRecordEntity>();

    public DbSet<LedgerTransactionEntity> LedgerTransactions => Set<LedgerTransactionEntity>();

    public DbSet<LedgerPostingEntity> LedgerPostings => Set<LedgerPostingEntity>();

    public DbSet<PlayerFieldValueEntity> PlayerFieldValues => Set<PlayerFieldValueEntity>();

    public DbSet<UserAccountEntity> UserAccounts => Set<UserAccountEntity>();

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();

    public DbSet<SessionMetadataEntity> SessionMetadata => Set<SessionMetadataEntity>();

    public DbSet<JoinLinkEntity> JoinLinks => Set<JoinLinkEntity>();

    public DbSet<SessionTtlPolicyEntity> SessionTtlPolicies => Set<SessionTtlPolicyEntity>();

    public DbSet<TemplateShareEntity> TemplateShares => Set<TemplateShareEntity>();

    public DbSet<TemplateMetadataEntity> TemplateMetadata => Set<TemplateMetadataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSessionEntity>(entity =>
        {
            entity.ToTable("game_sessions");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.RoomCode).HasMaxLength(12).IsRequired();
            entity.Property(record => record.Status).HasMaxLength(32).IsRequired();
            entity.Property(record => record.SessionVersion).IsConcurrencyToken();
            entity.HasIndex(record => record.RoomCode).IsUnique();
            entity.HasIndex(record => new { record.Id, record.SessionVersion });
            entity.HasIndex(record => record.CreatedAtUtc);
        });

        modelBuilder.Entity<TemplateSnapshotEntity>(entity =>
        {
            entity.ToTable("template_snapshots");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TemplateId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.EditionId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.TemplateVersion).HasMaxLength(50).IsRequired();
            entity.Property(record => record.ContentHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.TemplateJson).IsRequired();
            entity.HasIndex(record => new
            {
                record.TemplateId,
                record.EditionId,
                record.TemplateVersion
            });
        });

        modelBuilder.Entity<ParticipantEntity>(entity =>
        {
            entity.ToTable("participants");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(record => record.Role).HasMaxLength(32).IsRequired();
            entity.Property(record => record.IdentityKey).HasMaxLength(50).IsRequired();
            entity.Property(record => record.ReconnectSecretHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(record => new { record.SessionId, record.Id }).IsUnique();
            entity.HasIndex(record => new { record.SessionId, record.JoinOrder }).IsUnique();
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.OwnerType).HasMaxLength(32).IsRequired();
            entity.Property(record => record.Version).IsConcurrencyToken();
            entity.HasIndex(record => new { record.SessionId, record.OwnerId, record.OwnerType }).IsUnique();
        });

        modelBuilder.Entity<IdempotencyRecordEntity>(entity =>
        {
            entity.ToTable("idempotency_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Key).HasMaxLength(150).IsRequired();
            entity.Property(record => record.CommandType).HasMaxLength(80).IsRequired();
            entity.Property(record => record.RequestHash).HasMaxLength(128).IsRequired();
            entity.Property(record => record.ResultHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(record => new { record.SessionId, record.ActorParticipantId, record.Key }).IsUnique();
        });

        modelBuilder.Entity<LedgerTransactionEntity>(entity =>
        {
            entity.ToTable("ledger_transactions");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Kind).HasMaxLength(32).IsRequired();
            entity.Property(record => record.Note).HasMaxLength(1000).IsRequired();
            entity.HasIndex(record => new { record.SessionId, record.Sequence }).IsUnique();
            entity.HasIndex(record => new { record.SessionId, record.CreatedAtUtc });
            entity.HasIndex(record => new { record.SessionId, record.CorrectsTransactionId });
        });

        modelBuilder.Entity<LedgerPostingEntity>(entity =>
        {
            entity.ToTable("ledger_postings");
            entity.HasKey(record => record.Id);
            entity.HasIndex(record => new { record.TransactionId, record.Id });
            entity.HasIndex(record => new { record.SessionId, record.AccountId });
        });

        modelBuilder.Entity<PlayerFieldValueEntity>(entity =>
        {
            entity.ToTable("player_field_values");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.FieldId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.ValueJson).IsRequired();
            entity.Property(record => record.Version).IsConcurrencyToken();
            entity.HasIndex(record => new { record.SessionId, record.ParticipantId, record.FieldId }).IsUnique();
            entity.HasIndex(record => new { record.SessionId, record.ParticipantId });
        });

        modelBuilder.Entity<UserAccountEntity>(entity =>
        {
            entity.ToTable("user_accounts");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Email).HasMaxLength(256).IsRequired();
            entity.Property(record => record.DisplayName).HasMaxLength(100).IsRequired();
            entity.HasIndex(record => record.Email).IsUnique();
            entity.HasIndex(record => record.CreatedAtUtc);
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(record => new { record.UserId, record.ExpiresAtUtc });
            entity.HasIndex(record => record.CreatedAtUtc);
        });

        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasMaxLength(100).IsRequired();
            entity.Property(record => record.IpAddress).HasMaxLength(45);
            entity.HasIndex(record => record.SessionId);
            entity.HasIndex(record => record.ActorUserId);
            entity.HasIndex(record => record.CreatedAtUtc);
            entity.HasIndex(record => new { record.SessionId, record.CreatedAtUtc });
        });

        modelBuilder.Entity<SessionMetadataEntity>(entity =>
        {
            entity.ToTable("session_metadata");
            entity.HasKey(record => record.SessionId);
            entity.Property(record => record.Label).HasMaxLength(200).IsRequired();
            entity.HasIndex(record => record.OwnerUserId);
            entity.HasIndex(record => record.LastAccessedAtUtc);
        });

        modelBuilder.Entity<JoinLinkEntity>(entity =>
        {
            entity.ToTable("join_links");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.LinkToken).HasMaxLength(128).IsRequired();
            entity.HasIndex(record => record.LinkToken).IsUnique();
            entity.HasIndex(record => new { record.SessionId, record.ExpiresAtUtc });
        });

        modelBuilder.Entity<SessionTtlPolicyEntity>(entity =>
        {
            entity.ToTable("session_ttl_policies");
            entity.HasKey(record => record.SessionId);
            entity.HasIndex(record => record.ExpiresAtUtc);
            entity.HasIndex(record => record.IsArchived);
        });

        modelBuilder.Entity<TemplateShareEntity>(entity =>
        {
            entity.ToTable("template_shares");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TemplateId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.SharedWithEmail).HasMaxLength(256).IsRequired();
            entity.HasIndex(record => new { record.TemplateId, record.SharedWithEmail });
            entity.HasIndex(record => record.SharedByUserId);
            entity.HasIndex(record => record.SharedWithEmail);
            entity.HasIndex(record => record.GrantedAtUtc);
            // Unique constraint: only one active share per template per user
            entity.HasIndex(record => new { record.TemplateId, record.SharedWithEmail, record.RevokedAtUtc })
                .IsUnique()
                .HasFilter("revoked_at_utc IS NULL");
        });

        modelBuilder.Entity<TemplateMetadataEntity>(entity =>
        {
            entity.ToTable("template_metadata");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TemplateId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.EditionId).HasMaxLength(100).IsRequired();
            entity.Property(record => record.Author).HasMaxLength(200).IsRequired();
            entity.Property(record => record.AuthorEmail).HasMaxLength(256);
            entity.Property(record => record.AuthorUrl).HasMaxLength(500);
            entity.Property(record => record.License).HasMaxLength(50).IsRequired();
            entity.Property(record => record.TemplateStatus).HasMaxLength(32).IsRequired();
            entity.Property(record => record.ModerationStatus).HasMaxLength(32).IsRequired();
            entity.HasIndex(record => new { record.TemplateId, record.EditionId }).IsUnique();
            entity.HasIndex(record => record.OwnerUserId);
            entity.HasIndex(record => record.TemplateStatus);
            entity.HasIndex(record => record.ModerationStatus);
            entity.HasIndex(record => record.UpdatedAtUtc);
        });
    }
}

