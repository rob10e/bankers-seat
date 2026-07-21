using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankersSeat.Server.Domain.Templates;

namespace BankersSeat.Server.Application.Templates;

/// <summary>
/// Manages template export/import operations, including ZIP packaging and validation.
/// Templates are packaged with their JSON definition, referenced assets, and metadata.
/// </summary>
public interface ITemplatePackageService
{
    /// <summary>
    /// Export a template as a ZIP package containing template.json, assets, and metadata.
    /// </summary>
    Task<ExportResult> ExportTemplateAsync(
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Import a template package from a ZIP file, validating schema and assets.
    /// Returns the imported TemplateSnapshot and location where it was stored.
    /// </summary>
    Task<ImportResult> ImportTemplateAsync(
        Stream zipStream,
        string destinationDirectory,
        CancellationToken cancellationToken
    );
}

public sealed class ExportResult
{
    private ExportResult(bool isSuccess, string? errorMessage, byte[]? packageData)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        PackageData = packageData;
    }

    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public byte[]? PackageData { get; }

    public static ExportResult Success(byte[] packageData) =>
        new(true, null, packageData);

    public static ExportResult Failure(string errorMessage) =>
        new(false, errorMessage, null);
}

public sealed class ImportResult
{
    private ImportResult(
        bool isSuccess,
        string? errorMessage,
        TemplateSnapshot? snapshot,
        string? destinationPath
    )
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Snapshot = snapshot;
        DestinationPath = destinationPath;
    }

    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public TemplateSnapshot? Snapshot { get; }
    public string? DestinationPath { get; }

    public static ImportResult Success(TemplateSnapshot snapshot, string destinationPath) =>
        new(true, null, snapshot, destinationPath);

    public static ImportResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null);
}

/// <summary>
/// Implementation of ITemplatePackageService for ZIP-based template packaging.
/// </summary>
public sealed class TemplatePackageService : ITemplatePackageService
{
    private readonly ITemplateCatalogService _catalogService;
    private readonly ILogger<TemplatePackageService> _logger;

    private const long MaxPackageSize = 100 * 1024 * 1024; // 100 MB
    private const int MaxAssetCount = 500;
    private static readonly HashSet<string> AllowedAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".svg",
        ".gif",
        ".webm",
        ".mp4"
    };

    public TemplatePackageService(
        ITemplateCatalogService catalogService,
        ILogger<TemplatePackageService> logger
    )
    {
        _catalogService = catalogService;
        _logger = logger;
    }

    public async Task<ExportResult> ExportTemplateAsync(
        string templateId,
        string editionId,
        string templateVersion,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var snapshot = await _catalogService.GetTemplateSnapshotAsync(
                templateId,
                editionId,
                templateVersion,
                cancellationToken
            );

            if (snapshot is null)
            {
                _logger.LogWarning(
                    "Template not found for export: {TemplateId}/{EditionId}/{Version}",
                    templateId,
                    editionId,
                    templateVersion
                );
                return ExportResult.Failure("Template not found");
            }

            using var memoryStream = new MemoryStream();
            using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Add template.json
                var templateJsonEntry = zipArchive.CreateEntry("template.json");
                using (var entryStream = templateJsonEntry.Open())
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    await JsonSerializer.SerializeAsync(
                        entryStream,
                        snapshot,
                        jsonOptions,
                        cancellationToken
                    );
                }

                // Add metadata.json
                var metadata = new TemplatePackageMetadata
                {
                    ExportedAt = DateTime.UtcNow,
                    TemplateId = templateId,
                    EditionId = editionId,
                    TemplateVersion = templateVersion,
                    PackageVersion = "1.0"
                };

                var metadataEntry = zipArchive.CreateEntry("metadata.json");
                using (var entryStream = metadataEntry.Open())
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    await JsonSerializer.SerializeAsync(
                        entryStream,
                        metadata,
                        jsonOptions,
                        cancellationToken
                    );
                }

                // Assets are referenced in template.json; they would need to be
                // added from the source directory. This is a placeholder for asset inclusion logic.
                // In a full implementation, you'd copy assets from the source template directory
                // into the ZIP with proper path handling and validation.
            }

            var packageBytes = memoryStream.ToArray();

            _logger.LogInformation(
                "Template exported successfully: {TemplateId}/{EditionId}/{Version}, package size: {SizeBytes}",
                templateId,
                editionId,
                templateVersion,
                packageBytes.Length
            );

            return ExportResult.Success(packageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error exporting template: {TemplateId}/{EditionId}/{Version}",
                templateId,
                editionId,
                templateVersion
            );
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    public async Task<ImportResult> ImportTemplateAsync(
        Stream zipStream,
        string destinationDirectory,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Validate package size
            if (zipStream.Length > MaxPackageSize)
            {
                _logger.LogWarning(
                    "Import rejected: package exceeds size limit ({Size} > {Limit})",
                    zipStream.Length,
                    MaxPackageSize
                );
                return ImportResult.Failure(
                    $"Package exceeds maximum size of {MaxPackageSize / (1024 * 1024)} MB"
                );
            }

            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            // Validate package structure
            var templateJsonEntry = zipArchive.Entries.FirstOrDefault(e =>
                e.Name.Equals("template.json", StringComparison.OrdinalIgnoreCase)
            );
            if (templateJsonEntry is null)
            {
                return ImportResult.Failure("Package missing template.json");
            }

            // Read and parse template.json
            TemplateSnapshot? snapshot = null;
            using (var entryStream = templateJsonEntry.Open())
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                snapshot = await JsonSerializer.DeserializeAsync<TemplateSnapshot>(
                    entryStream,
                    jsonOptions,
                    cancellationToken
                );
            }

            if (snapshot is null)
            {
                return ImportResult.Failure("Failed to parse template.json");
            }

            // Read metadata if present
            var metadataEntry = zipArchive.Entries.FirstOrDefault(e =>
                e.Name.Equals("metadata.json", StringComparison.OrdinalIgnoreCase)
            );
            TemplatePackageMetadata? metadata = null;
            if (metadataEntry is not null)
            {
                using (var entryStream = metadataEntry.Open())
                {
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    metadata = await JsonSerializer.DeserializeAsync<TemplatePackageMetadata>(
                        entryStream,
                        jsonOptions,
                        cancellationToken
                    );
                }
            }

            // Validate assets in ZIP
            var assetEntries = zipArchive.Entries.Where(e =>
                e.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                && !e.FullName.EndsWith("/", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (assetEntries.Count > MaxAssetCount)
            {
                return ImportResult.Failure(
                    $"Package contains too many assets ({assetEntries.Count} > {MaxAssetCount})"
                );
            }

            // Validate asset extensions and paths
            var validationError = ValidateAssets(assetEntries);
            if (validationError is not null)
            {
                return ImportResult.Failure(validationError);
            }

            // Extract package to destination
            var templateId = snapshot.Identity.TemplateId;
            var editionId = snapshot.Identity.EditionId;
            var destinationPath = Path.Combine(
                destinationDirectory,
                templateId,
                editionId
            );

            Directory.CreateDirectory(destinationPath);

            // Extract template.json
            var templateJsonPath = Path.Combine(destinationPath, "template.json");
            templateJsonEntry.ExtractToFile(templateJsonPath, overwrite: true);

            // Extract assets
            foreach (var assetEntry in assetEntries)
            {
                var relativePath = assetEntry.FullName;
                var targetPath = Path.Combine(destinationPath, relativePath);

                // Ensure path doesn't escape the destination directory
                var fullPath = Path.GetFullPath(targetPath);
                if (!fullPath.StartsWith(Path.GetFullPath(destinationPath), StringComparison.Ordinal))
                {
                    return ImportResult.Failure(
                        $"Invalid asset path detected: {relativePath}"
                    );
                }

                var parentDir = Path.GetDirectoryName(targetPath);
                if (parentDir is not null)
                {
                    Directory.CreateDirectory(parentDir);
                }

                assetEntry.ExtractToFile(targetPath, overwrite: true);
            }

            _logger.LogInformation(
                "Template imported successfully: {TemplateId}/{EditionId}, destination: {Path}",
                templateId,
                editionId,
                destinationPath
            );

            return ImportResult.Success(snapshot, destinationPath);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid ZIP archive structure during import");
            return ImportResult.Failure($"Invalid package format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing template package");
            return ImportResult.Failure($"Import failed: {ex.Message}");
        }
    }

    private static string? ValidateAssets(IEnumerable<ZipArchiveEntry> assetEntries)
    {
        foreach (var entry in assetEntries)
        {
            var fileName = Path.GetFileName(entry.FullName);

            // Check extension
            var extension = Path.GetExtension(fileName);
            if (!AllowedAssetExtensions.Contains(extension))
            {
                return $"Asset has disallowed extension: {fileName}";
            }

            // Check for path traversal
            if (entry.FullName.Contains("..", StringComparison.Ordinal)
                || Path.IsPathRooted(entry.FullName))
            {
                return $"Asset path escapes package directory: {entry.FullName}";
            }
        }

        return null;
    }
}

/// <summary>
/// Metadata included in exported template packages.
/// </summary>
public sealed class TemplatePackageMetadata
{
    public DateTime ExportedAt { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string EditionId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = "1.0";
}
