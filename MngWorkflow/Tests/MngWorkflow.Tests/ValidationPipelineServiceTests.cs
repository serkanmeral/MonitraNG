using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MngWorkflow.Application.Services;
using MngWorkflow.Infrastructure.Services;
using Xunit;

namespace MngWorkflow.Tests;

/// <summary>
/// ValidationPipelineService birim testleri — DG çalışmadan mock IDataGatewayClient ile.
/// Entegrasyon testi (gerçek DG + API) ayrıca script veya WebApplicationFactory ile yapılır.
/// </summary>
public class ValidationPipelineServiceTests
{
    private static List<Dictionary<string, object>> PipelineRow()
    {
        var steps = new List<object>
        {
            new Dictionary<string, object>
            {
                ["type"] = "fetch",
                ["dataset"] = "tm_projects",
                ["by"] = "__dataId",
                ["value"] = "projectId",
            },
            new Dictionary<string, object>
            {
                ["type"] = "assert",
                ["expr"] = "result.key == payload.projectKey",
                ["message"] = "projectKey proje kodu ile eslesmiyor",
            },
        };

        return new List<Dictionary<string, object>>
        {
            new()
            {
                ["name"] = "tm_issues_project_key",
                ["dataset"] = "tm_issues",
                ["order"] = 0,
                ["steps"] = steps,
            },
        };
    }

    private static Dictionary<string, object> ProjectRow(string key, string dataId = "proj-oid-1")
    {
        return new Dictionary<string, object>
        {
            ["__dataId"] = dataId,
            ["key"] = key,
            ["name"] = "Test Project",
        };
    }

    [Fact]
    public async Task ValidateAsync_tm_issues_projectKey_eslesince_gecerli()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg
            .Setup(d => d.GetDataAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string dataset, string? _, string _, string? _, CancellationToken _) =>
            {
                if (dataset == "@wf_validation_pipelines")
                    return PipelineRow();
                if (dataset == "tm_projects")
                    return new List<Dictionary<string, object>> { ProjectRow("DEMO") };
                return new List<Dictionary<string, object>>();
            });

        var sut = new ValidationPipelineService(mockDg.Object, NullLogger<ValidationPipelineService>.Instance);

        var payload = new Dictionary<string, object>
        {
            ["projectId"] = "proj-oid-1",
            ["projectKey"] = "DEMO",
        };

        var result = await sut.ValidateAsync("tm_issues", payload, "meral", "Bearer test", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_tm_issues_projectKey_uyusmayinca_red()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg
            .Setup(d => d.GetDataAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string dataset, string? _, string _, string? _, CancellationToken _) =>
            {
                if (dataset == "@wf_validation_pipelines")
                    return PipelineRow();
                if (dataset == "tm_projects")
                    return new List<Dictionary<string, object>> { ProjectRow("DEMO") };
                return new List<Dictionary<string, object>>();
            });

        var sut = new ValidationPipelineService(mockDg.Object, NullLogger<ValidationPipelineService>.Instance);

        var payload = new Dictionary<string, object>
        {
            ["projectId"] = "proj-oid-1",
            ["projectKey"] = "YANLIS",
        };

        var result = await sut.ValidateAsync("tm_issues", payload, "meral", "Bearer test", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_pipeline_yoksa_gecerli()
    {
        var mockDg = new Mock<IDataGatewayClient>();
        mockDg
            .Setup(d => d.GetDataAsync(
                It.Is<string>(s => s == "@wf_validation_pipelines"),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Dictionary<string, object>>());

        var sut = new ValidationPipelineService(mockDg.Object, NullLogger<ValidationPipelineService>.Instance);

        var result = await sut.ValidateAsync("baska_dataset", new Dictionary<string, object>(), "meral", null, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
