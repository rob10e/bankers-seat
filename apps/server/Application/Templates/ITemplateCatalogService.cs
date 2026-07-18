using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

public interface ITemplateCatalogService
{
    Task<IReadOnlyList<TemplateCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken);

    Task<TemplateSnapshot?> GetTemplateSnapshotAsync(
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken cancellationToken
    );
}
