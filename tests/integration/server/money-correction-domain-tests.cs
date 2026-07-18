using BankersSeat.Server.Domain.Ledger;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class MoneyCorrectionDomainTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid DebitAccountId = Guid.NewGuid();
    private static readonly Guid CreditAccountId = Guid.NewGuid();

    [Fact]
    public void ApplyTransfer_SucceedsWithBalancedLedgerAndUpdatedBalances()
    {
        var balances = new Dictionary<Guid, long>
        {
            [DebitAccountId] = 1200,
            [CreditAccountId] = 300
        };

        var result = MoneyMutationEngine.ApplyTransfer(
            balances,
            SessionId,
            ActorId,
            DebitAccountId,
            CreditAccountId,
            250,
            allowOverdraft: false,
            sequence: 3,
            createdAtUtc: DateTimeOffset.UtcNow,
            note: "Rent payment"
        );

        Assert.Equal(950, result.UpdatedBalances[DebitAccountId]);
        Assert.Equal(550, result.UpdatedBalances[CreditAccountId]);
        Assert.Equal(LedgerTransactionKind.Transfer, result.Transaction.Kind);
        Assert.Equal(2, result.Transaction.Postings.Count);
        Assert.Equal(0, result.Transaction.Postings.Sum(posting => posting.Amount));
    }

    [Fact]
    public void ApplyTransfer_RejectsInsufficientFundsWhenOverdraftDisabled()
    {
        var balances = new Dictionary<Guid, long>
        {
            [DebitAccountId] = 100,
            [CreditAccountId] = 0
        };

        var exception = Assert.Throws<DomainRuleViolationException>(() =>
            MoneyMutationEngine.ApplyTransfer(
                balances,
                SessionId,
                ActorId,
                DebitAccountId,
                CreditAccountId,
                150,
                allowOverdraft: false,
                sequence: 4,
                createdAtUtc: DateTimeOffset.UtcNow,
                note: "Too large"
            )
        );

        Assert.Equal("insufficient-funds", exception.Code);
        Assert.Equal(100, balances[DebitAccountId]);
        Assert.Equal(0, balances[CreditAccountId]);
    }

    [Fact]
    public void ApplyTransfer_AllowsNegativeBalanceWhenOverdraftEnabled()
    {
        var balances = new Dictionary<Guid, long>
        {
            [DebitAccountId] = 100,
            [CreditAccountId] = 200
        };

        var result = MoneyMutationEngine.ApplyTransfer(
            balances,
            SessionId,
            ActorId,
            DebitAccountId,
            CreditAccountId,
            150,
            allowOverdraft: true,
            sequence: 5,
            createdAtUtc: DateTimeOffset.UtcNow,
            note: "Allowed overdraft"
        );

        Assert.Equal(-50, result.UpdatedBalances[DebitAccountId]);
        Assert.Equal(350, result.UpdatedBalances[CreditAccountId]);
    }

    [Fact]
    public void ApplyCorrection_CreatesCompensatingTransaction()
    {
        var beforeTransfer = new Dictionary<Guid, long>
        {
            [DebitAccountId] = 700,
            [CreditAccountId] = 600
        };
        var transfer = MoneyMutationEngine.ApplyTransfer(
            beforeTransfer,
            SessionId,
            ActorId,
            DebitAccountId,
            CreditAccountId,
            200,
            allowOverdraft: false,
            sequence: 8,
            createdAtUtc: DateTimeOffset.UtcNow,
            note: "Original transfer"
        );

        var correction = MoneyMutationEngine.ApplyCorrection(
            transfer.UpdatedBalances,
            SessionId,
            ActorId,
            transfer.Transaction,
            correctedTransactionIds: new HashSet<Guid>(),
            sequence: 9,
            createdAtUtc: DateTimeOffset.UtcNow,
            reason: "Wrong target player"
        );

        Assert.Equal(700, correction.UpdatedBalances[DebitAccountId]);
        Assert.Equal(600, correction.UpdatedBalances[CreditAccountId]);
        Assert.Equal(LedgerTransactionKind.Correction, correction.Transaction.Kind);
        Assert.Equal(transfer.Transaction.Id, correction.Transaction.CorrectsTransactionId);
        Assert.Equal("Wrong target player", correction.Transaction.Note);
        Assert.Equal(0, correction.Transaction.Postings.Sum(posting => posting.Amount));
        Assert.Equal(
            -transfer.Transaction.Postings[0].Amount,
            correction.Transaction.Postings[0].Amount
        );
        Assert.Equal(
            -transfer.Transaction.Postings[1].Amount,
            correction.Transaction.Postings[1].Amount
        );
    }

    [Fact]
    public void ApplyCorrection_RejectsDuplicateCorrection()
    {
        var balances = new Dictionary<Guid, long>
        {
            [DebitAccountId] = 900,
            [CreditAccountId] = 100
        };
        var transfer = MoneyMutationEngine.ApplyTransfer(
            balances,
            SessionId,
            ActorId,
            DebitAccountId,
            CreditAccountId,
            50,
            allowOverdraft: false,
            sequence: 11,
            createdAtUtc: DateTimeOffset.UtcNow,
            note: "Original"
        );

        var exception = Assert.Throws<DomainRuleViolationException>(() =>
            MoneyMutationEngine.ApplyCorrection(
                transfer.UpdatedBalances,
                SessionId,
                ActorId,
                transfer.Transaction,
                correctedTransactionIds: new HashSet<Guid> { transfer.Transaction.Id },
                sequence: 12,
                createdAtUtc: DateTimeOffset.UtcNow,
                reason: "Duplicate attempt"
            )
        );

        Assert.Equal("duplicate-correction", exception.Code);
    }

    [Fact]
    public void ApplyCorrection_FailsAtomicallyWhenAccountIsMissing()
    {
        var transfer = MoneyMutationEngine.ApplyTransfer(
            new Dictionary<Guid, long>
            {
                [DebitAccountId] = 1000,
                [CreditAccountId] = 200
            },
            SessionId,
            ActorId,
            DebitAccountId,
            CreditAccountId,
            100,
            allowOverdraft: false,
            sequence: 13,
            createdAtUtc: DateTimeOffset.UtcNow,
            note: "Original"
        );

        var balancesWithoutCredit = new Dictionary<Guid, long>
        {
            [DebitAccountId] = transfer.UpdatedBalances[DebitAccountId]
        };

        var exception = Assert.Throws<DomainRuleViolationException>(() =>
            MoneyMutationEngine.ApplyCorrection(
                balancesWithoutCredit,
                SessionId,
                ActorId,
                transfer.Transaction,
                correctedTransactionIds: new HashSet<Guid>(),
                sequence: 14,
                createdAtUtc: DateTimeOffset.UtcNow,
                reason: "Reversal"
            )
        );

        Assert.Equal("account-not-found", exception.Code);
        Assert.Equal(900, balancesWithoutCredit[DebitAccountId]);
    }
}
