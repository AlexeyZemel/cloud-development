using ProjectApp.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Options;
using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace ProjectApp.Api.Services;

/// <summary>
/// Сервис получения программного проекта с использованием кэша и генерации при отсутствии данны
/// </summary>
public class ProgramProjectGeneratorService(
    IDistributedCache cache,
    ProgramProjectGenerator generator,
    IOptions<CacheSettings> cacheSettings,
    JsonSerializerOptions jsonSerializerOptions,
    ILogger<ProgramProjectGeneratorService> logger)
{
    private static readonly Meter _meter = new("ProjectApp.Api");
    private static readonly Counter<long> _cacheErrorCounter = _meter.CreateCounter<long>("cache.errors");
    private static readonly Histogram<double> _projectGenerationDuration = _meter.CreateHistogram<double>("project.generation.duration.ms");
    private readonly int _expirationMinutes = cacheSettings.Value.ExpirationMinutes;

    /// <summary>
    /// Возвращает проект по идентификатору из кэша или генерирует новый и сохраняет его в кэш
    /// </summary>
    /// <param name="id">Идентификатор проекта</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Программный проект</returns>
    public async Task<ProgramProject> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Attempting to retrieve software project {Id} from cache", id);

        var cacheKey = $"software-project-{id}";

        ProgramProject? project = null;
        try
        {
            var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

            if (!string.IsNullOrEmpty(cachedData))
            {
                project = JsonSerializer.Deserialize<ProgramProject>(cachedData, jsonSerializerOptions);

                if (project != null)
                {
                    logger.LogInformation("Software project {Id} found in cache", id);
                    return project;
                }

                logger.LogWarning("Project {Id} was found in cache but could not be deserialized. Generating a new one", id);
            }
        }
        catch (Exception ex)
        {
            _cacheErrorCounter.Add(1, new KeyValuePair<string, object?>("operation", "get"));
            logger.LogWarning(ex, "Failed to retrieve project {Id} from cache (error ignored)", id);
        }

        logger.LogInformation("Project {Id} not found in cache or cache unavailable, generating a new one", id);
        var stopwatch = Stopwatch.StartNew();
        project = generator.Generate();
        stopwatch.Stop();
        _projectGenerationDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
        project.Id = id;

        try
        {
            logger.LogInformation("Saving project {Id} to cache", id);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_expirationMinutes)
            };

            await cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(project, jsonSerializerOptions),
                cacheOptions,
                cancellationToken);

            logger.LogInformation(
                "Software project generated and cached: Id={Id}, Name={ProjectName}, Customer={Customer}, Budget={Budget}, Completion={CompletionPercent}",
                project.Id,
                project.ProjectName,
                project.Customer,
                project.Budget,
                project.CompletionPercentage);
        }
        catch (Exception ex)
        {
            _cacheErrorCounter.Add(1, new KeyValuePair<string, object?>("operation", "set"));
            logger.LogWarning(ex, "Failed to save project {Id} to cache (error ignored)", id);
        }

        return project;
    }
}