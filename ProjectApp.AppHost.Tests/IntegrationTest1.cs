using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace ProjectApp.AppHost.Tests;

/// <summary>
/// Интеграционные тесты
/// </summary>
/// <param name="output">Служба журналирования юнит-тестов</param>
public class IntegrationTest1(ITestOutputHelper output) : IAsyncLifetime
{
    private DistributedApplication? _app;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ProjectApp_AppHost>(cancellationToken);
        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";
        builder.Services.AddLogging(logging =>
        {
            logging.AddXUnit(output);
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting", LogLevel.Debug);
        });
        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Проверяет, что вызов гейтвея:
    /// <list type="bullet">
    /// <item><description>в ответ возвращает сгенерированный программный проект</description></item>
    /// <item><description>сериализует его в объектное хранилище Minio под ключом project_{id}.json</description></item>
    /// <item><description>содержимое в API-ответе и в S3 идентично</description></item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task TestPipeline()
    {
        var cancellationToken = CancellationToken.None;

        var id = Random.Shared.Next(1, 100);
        using var gatewayClient = _app!.CreateHttpClient("projectapp-gateway");
        using var gatewayResponse = await gatewayClient.GetAsync($"/api/project?id={id}", cancellationToken);
        var apiProject = JsonNode.Parse(await gatewayResponse.Content.ReadAsStringAsync(cancellationToken));

        await Task.Delay(5000, cancellationToken);
        using var fileServiceClient = _app!.CreateHttpClient("projectapp-fileservice");
        using var listResponse = await fileServiceClient.GetAsync("/api/s3", cancellationToken);
        var projectList = JsonSerializer.Deserialize<List<string>>(
            await listResponse.Content.ReadAsStringAsync(cancellationToken));
        using var s3Response = await fileServiceClient.GetAsync($"/api/s3/project_{id}.json", cancellationToken);
        var s3Project = JsonNode.Parse(await s3Response.Content.ReadAsStringAsync(cancellationToken));

        Assert.NotNull(projectList);
        Assert.Contains($"project_{id}.json", projectList);
        Assert.NotNull(apiProject);
        Assert.NotNull(s3Project);
        Assert.Equal(id, s3Project!["id"]!.GetValue<int>());
        Assert.Equal(apiProject!.ToJsonString(), s3Project!.ToJsonString());
    }

    /// <summary>
    /// Проверяет идемпотентность гейтвея по идентификатору:
    /// два последовательных запроса с одним и тем же id должны вернуть
    /// один и тот же объект (значение берётся из Redis-кэша)
    /// </summary>
    [Fact]
    public async Task TestIdempotency()
    {
        var cancellationToken = CancellationToken.None;

        var id = Random.Shared.Next(100, 200);
        using var gatewayClient = _app!.CreateHttpClient("projectapp-gateway");

        using var firstResponse = await gatewayClient.GetAsync($"/api/project?id={id}", cancellationToken);
        var firstProject = JsonNode.Parse(await firstResponse.Content.ReadAsStringAsync(cancellationToken));

        using var secondResponse = await gatewayClient.GetAsync($"/api/project?id={id}", cancellationToken);
        var secondProject = JsonNode.Parse(await secondResponse.Content.ReadAsStringAsync(cancellationToken));

        Assert.NotNull(firstProject);
        Assert.NotNull(secondProject);
        Assert.Equal(id, firstProject!["id"]!.GetValue<int>());
        Assert.Equal(id, secondProject!["id"]!.GetValue<int>());
        Assert.Equal(firstProject!.ToJsonString(), secondProject!.ToJsonString());
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
