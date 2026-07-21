using BankersSeat.Server.Api.V1;
using BankersSeat.Server.Api.V1.Contracts;
using BankersSeat.Server.Application.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BankersSeat.Server.Tests.Integration;

public sealed class TemplatesControllerTests
{
    private static readonly string TemplatesRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "templates")
    );
    private readonly FileTemplateCatalogService catalogService = new(TemplatesRoot);

    [Fact]
    public async Task GetTemplateByVersionReturnsDetailedTemplateForKnownIdentity()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TemplatesController>();
        var diffService = new TemplateDiffService(loggerFactory.CreateLogger<TemplateDiffService>());
        var controller = new TemplatesController(catalogService, diffService, logger);

        var result = await controller.GetTemplateByVersion(
            "generic-property-trading",
            "standard-edition",
            "1.0.0",
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<TemplateDetailResponse>(ok.Value);
        Assert.Equal("generic-property-trading", payload.TemplateId);
        Assert.Equal("standard-edition", payload.EditionId);
        Assert.Equal("1.0.0", payload.TemplateVersion);
        Assert.Equal(1500, payload.StartingPlayerBalance);
        Assert.True(payload.Template.TryGetProperty("actions", out _));
    }

    [Fact]
    public async Task GetTemplateByVersionReturnsNotFoundForUnknownIdentity()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<TemplatesController>();
        var diffService = new TemplateDiffService(loggerFactory.CreateLogger<TemplateDiffService>());
        var controller = new TemplatesController(catalogService, diffService, logger);

        var result = await controller.GetTemplateByVersion(
            "missing-template",
            "missing-edition",
            "9.9.9",
            CancellationToken.None
        );

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("template-not-found", details.Extensions["code"]);
    }
}
