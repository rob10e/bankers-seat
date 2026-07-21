using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BankersSeat.Server.Application.Templates;
using BankersSeat.Server.Api.V1.Contracts;
using System.Text.Json;

namespace BankersSeat.Server.Api.V1;

/// <summary>
/// Template package export/import endpoints.
/// Enables users to export templates as ZIP packages and import new ones.
/// </summary>
[ApiController]
[Route("api/v1/templates")]
public sealed class TemplatePackageController : ControllerBase
{
    private readonly ITemplatePackageService _packageService;
    private readonly ITemplateCatalogService _catalogService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TemplatePackageController> _logger;

    public TemplatePackageController(
        ITemplatePackageService packageService,
        ITemplateCatalogService catalogService,
        IConfiguration configuration,
        ILogger<TemplatePackageController> logger
    )
    {
        _packageService = packageService;
        _catalogService = catalogService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Export a template as a ZIP package.
    /// Package contains template.json, assets, and metadata.
    /// </summary>
    /// <remarks>
    /// Example request:
    /// POST /api/v1/templates/export
    /// {
    ///   "templateId": "monopoly-deluxe",
    ///   "editionId": "2026-edition",
    ///   "templateVersion": "2.1.0"
    /// }
    /// 
    /// Response: 200 OK with ZIP file as application/zip
    /// </remarks>
    [HttpPost("export")]
    [Produces("application/zip")]
    public async Task<IActionResult> ExportTemplate(
        [FromBody] ExportTemplateRequest request,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId)
            || string.IsNullOrWhiteSpace(request.EditionId)
            || string.IsNullOrWhiteSpace(request.TemplateVersion))
        {
            return BadRequest(new { error = "TemplateId, EditionId, and TemplateVersion are required" });
        }

        var result = await _packageService.ExportTemplateAsync(
            request.TemplateId,
            request.EditionId,
            request.TemplateVersion,
            cancellationToken
        );

        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.ErrorMessage });
        }

        var fileName = $"{request.TemplateId}-{request.TemplateVersion}.zip";
        return File(
            result.PackageData!,
            "application/zip",
            fileName
        );
    }

    /// <summary>
    /// Import a template from a ZIP package.
    /// Validates schema, assets, and stores template in installed directory.
    /// </summary>
    /// <remarks>
    /// Example: Upload a ZIP file containing template.json and assets/
    /// 
    /// Response: 200 OK
    /// {
    ///   "success": true,
    ///   "templateId": "my-template",
    ///   "editionId": "v1",
    ///   "templateVersion": "1.0.0",
    ///   "destinationPath": "/templates/installed/my-template/v1"
    /// }
    /// </remarks>
    [HttpPost("import")]
    [Authorize]
    public async Task<IActionResult> ImportTemplate(
        CancellationToken cancellationToken
    )
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new { error = "Request must be multipart/form-data" });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "File must be a ZIP package" });
        }

        // Determine destination directory from configuration
        var installedTemplatesPath = _configuration["TemplatesRoot:Installed"]
            ?? Path.Combine(AppContext.BaseDirectory, "templates", "installed");

        using (var stream = file.OpenReadStream())
        {
            var result = await _packageService.ImportTemplateAsync(
                stream,
                installedTemplatesPath,
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                return BadRequest(new ImportTemplateResponse
                {
                    Success = false,
                    Error = result.ErrorMessage
                });
            }

            _logger.LogInformation(
                "User {UserId} imported template: {TemplateId}/{EditionId}",
                User.FindFirst("sub")?.Value ?? "anonymous",
                result.Snapshot?.Identity.TemplateId,
                result.Snapshot?.Identity.EditionId
            );

            return Ok(new ImportTemplateResponse
            {
                Success = true,
                TemplateId = result.Snapshot?.Identity.TemplateId,
                EditionId = result.Snapshot?.Identity.EditionId,
                TemplateVersion = result.Snapshot?.Identity.TemplateVersion,
                DestinationPath = result.DestinationPath
            });
        }
    }

    /// <summary>
    /// Get template preview data (read-only summary).
    /// Used for visual preview without creating a session.
    /// </summary>
    /// <remarks>
    /// Example request:
    /// GET /api/v1/templates/{templateId}/preview?editionId=v1&version=1.0.0
    /// 
    /// Response: 200 OK with template metadata
    /// </remarks>
    [HttpGet("{templateId}/preview")]
    public async Task<IActionResult> GetTemplatePreview(
        [FromRoute] string templateId,
        [FromQuery] string editionId,
        [FromQuery] string version,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(templateId)
            || string.IsNullOrWhiteSpace(editionId)
            || string.IsNullOrWhiteSpace(version))
        {
            return BadRequest(new { error = "templateId, editionId, and version are required" });
        }

        var snapshot = await _catalogService.GetTemplateSnapshotAsync(
            templateId,
            editionId,
            version,
            cancellationToken
        );

        if (snapshot is null)
        {
            return NotFound(new { error = "Template not found" });
        }

        try
        {
            // Parse the stored template JSON to extract preview data
            using var jsonDoc = JsonDocument.Parse(snapshot.TemplateJson);
            var root = jsonDoc.RootElement;

            var preview = new
            {
                templateId = snapshot.Identity.TemplateId,
                templateName = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() : "Unknown",
                editionId = snapshot.Identity.EditionId,
                editionName = root.TryGetProperty("edition", out var edElem) && edElem.TryGetProperty("name", out var edNameElem) ? edNameElem.GetString() : "Unknown",
                templateVersion = snapshot.Identity.TemplateVersion,
                description = root.TryGetProperty("description", out var descElem) ? descElem.GetString() : "No description provided",
                schemaVersion = snapshot.SchemaVersion
            };

            return Ok(preview);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse template JSON for preview");
            return StatusCode(500, new { error = "Failed to parse template data" });
        }
    }
}
