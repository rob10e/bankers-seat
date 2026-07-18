namespace BankersSeat.Server.Domain.Ledger;

public sealed class DomainRuleViolationException : InvalidOperationException
{
    public DomainRuleViolationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
