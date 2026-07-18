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
    }
}
