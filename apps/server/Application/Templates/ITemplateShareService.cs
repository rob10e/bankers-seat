namespace BankersSeat.Server.Application.Templates;

public interface ITemplateShareService
{
    /// <summary>
    /// Grant access to a template for a specific user.
    /// </summary>
    Task<Guid> GrantShareAsync(
        string templateId,
        Guid sharingUserId,
        string recipientEmail,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Revoke a share.
    /// </summary>
    Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken);

    /// <summary>
    /// Get all shares for a template.
    /// </summary>
    Task<IReadOnlyList<TemplateShareInfo>> GetSharesAsync(
        string templateId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get templates shared with the given user.
    /// </summary>
    Task<IReadOnlyList<SharedTemplateInfo>> GetSharedWithMeAsync(
        string userEmail,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Check if a user has access to a template (owner or shared).
    /// </summary>
    Task<bool> HasAccessAsync(
        string userEmail,
        string templateId,
        CancellationToken cancellationToken
    );
}

public sealed record TemplateShareInfo(
    Guid ShareId,
    string TemplateId,
    string SharedWithEmail,
    string SharedByName,
    DateTime GrantedAtUtc,
    bool IsActive
);

public sealed record SharedTemplateInfo(
    string TemplateId,
    string TemplateName,
    string EditionId,
    string SharedByName,
    DateTime GrantedAtUtc
);
