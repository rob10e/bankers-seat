namespace BankersSeat.Server.Domain.Ledger;

public static class MoneyMutationEngine
{
    public static MoneyMutationResult ApplyTransfer(
        IReadOnlyDictionary<Guid, long> currentBalances,
        Guid sessionId,
        Guid actorParticipantId,
        Guid debitAccountId,
        Guid creditAccountId,
        long amount,
        bool allowOverdraft,
        long sequence,
        DateTimeOffset createdAtUtc,
        string note
    )
    {
        return ApplyTransferBatch(
            currentBalances,
            sessionId,
            actorParticipantId,
            [
                new TransferInstruction(debitAccountId, creditAccountId, amount, allowOverdraft)
            ],
            sequence,
            createdAtUtc,
            note
        );
    }

    public static MoneyMutationResult ApplyTransferBatch(
        IReadOnlyDictionary<Guid, long> currentBalances,
        Guid sessionId,
        Guid actorParticipantId,
        IReadOnlyList<TransferInstruction> instructions,
        long sequence,
        DateTimeOffset createdAtUtc,
        string note
    )
    {
        if (instructions.Count == 0)
        {
            throw new DomainRuleViolationException(
                "invalid-transfer-batch",
                "Transfer batch must include at least one transfer instruction."
            );
        }

        var balances = new Dictionary<Guid, long>(currentBalances);
        var postings = new List<LedgerPosting>(instructions.Count * 2);
        foreach (var instruction in instructions)
        {
            if (instruction.Amount <= 0)
            {
                throw new DomainRuleViolationException(
                    "invalid-transfer-amount",
                    "Transfer amount must be a positive integer."
                );
            }

            if (instruction.DebitAccountId == instruction.CreditAccountId)
            {
                throw new DomainRuleViolationException(
                    "invalid-transfer-accounts",
                    "Transfer requires distinct debit and credit accounts."
                );
            }

            if (!balances.TryGetValue(instruction.DebitAccountId, out var debitBefore))
            {
                throw new DomainRuleViolationException(
                    "account-not-found",
                    $"Debit account '{instruction.DebitAccountId}' does not exist in the session."
                );
            }

            if (!balances.TryGetValue(instruction.CreditAccountId, out var creditBefore))
            {
                throw new DomainRuleViolationException(
                    "account-not-found",
                    $"Credit account '{instruction.CreditAccountId}' does not exist in the session."
                );
            }

            var debitAfter = checked(debitBefore - instruction.Amount);
            if (!instruction.AllowOverdraft && debitAfter < 0)
            {
                throw new DomainRuleViolationException(
                    "insufficient-funds",
                    "Transfer would exceed available funds while overdrafts are disallowed."
                );
            }

            var creditAfter = checked(creditBefore + instruction.Amount);
            balances[instruction.DebitAccountId] = debitAfter;
            balances[instruction.CreditAccountId] = creditAfter;
            postings.Add(new LedgerPosting(instruction.DebitAccountId, -instruction.Amount, debitAfter));
            postings.Add(new LedgerPosting(instruction.CreditAccountId, instruction.Amount, creditAfter));
        }

        EnsureBalanced(postings);

        return new MoneyMutationResult(
            balances,
            new LedgerTransaction(
                Guid.NewGuid(),
                sessionId,
                sequence,
                actorParticipantId,
                LedgerTransactionKind.Transfer,
                null,
                note,
                createdAtUtc,
                postings
            )
        );
    }

    public static MoneyMutationResult ApplyCorrection(
        IReadOnlyDictionary<Guid, long> currentBalances,
        Guid sessionId,
        Guid actorParticipantId,
        LedgerTransaction originalTransaction,
        IReadOnlySet<Guid> correctedTransactionIds,
        long sequence,
        DateTimeOffset createdAtUtc,
        string reason
    )
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException(
                "correction-reason-required",
                "Corrections require a non-empty reason."
            );
        }

        if (originalTransaction.SessionId != sessionId)
        {
            throw new DomainRuleViolationException(
                "cross-session-correction",
                "Corrections must target a transaction in the same session."
            );
        }

        if (correctedTransactionIds.Contains(originalTransaction.Id))
        {
            throw new DomainRuleViolationException(
                "duplicate-correction",
                "This transaction has already been corrected."
            );
        }

        var balances = new Dictionary<Guid, long>(currentBalances);
        var postings = new List<LedgerPosting>();

        foreach (var posting in originalTransaction.Postings)
        {
            if (!balances.TryGetValue(posting.AccountId, out var balanceBefore))
            {
                throw new DomainRuleViolationException(
                    "account-not-found",
                    $"Cannot apply correction because account '{posting.AccountId}' no longer exists."
                );
            }

            var compensation = checked(-posting.Amount);
            var balanceAfter = checked(balanceBefore + compensation);
            balances[posting.AccountId] = balanceAfter;
            postings.Add(new LedgerPosting(posting.AccountId, compensation, balanceAfter));
        }

        EnsureBalanced(postings);

        return new MoneyMutationResult(
            balances,
            new LedgerTransaction(
                Guid.NewGuid(),
                sessionId,
                sequence,
                actorParticipantId,
                LedgerTransactionKind.Correction,
                originalTransaction.Id,
                reason.Trim(),
                createdAtUtc,
                postings
            )
        );
    }

    private static void EnsureBalanced(IReadOnlyList<LedgerPosting> postings)
    {
        var total = postings.Sum(posting => posting.Amount);
        if (total != 0)
        {
            throw new DomainRuleViolationException(
                "unbalanced-transaction",
                "Ledger postings must sum to zero."
            );
        }
    }
}

public sealed record TransferInstruction(
    Guid DebitAccountId,
    Guid CreditAccountId,
    long Amount,
    bool AllowOverdraft
);
