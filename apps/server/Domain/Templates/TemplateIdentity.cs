namespace BankersSeat.Server.Domain.Templates;

public sealed record TemplateIdentity(
    string TemplateId,
    string EditionId,
    string TemplateVersion
);
