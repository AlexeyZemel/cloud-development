using Amazon.SQS;
using ProjectApp.Domain.Entities;
using System.Net;
using System.Text.Json;

namespace ProjectApp.Api.Messaging;

/// <summary>
/// Служба для отправки программных проектов в очередь SQS
/// </summary>
/// <param name="client">Клиент SQS</param>
/// <param name="configuration">Конфигурация приложения</param>
/// <param name="jsonOptions">Опции сериализации JSON</param>
/// <param name="logger">Логгер</param>
public class SqsProducerService(
    IAmazonSQS client,
    IConfiguration configuration,
    JsonSerializerOptions jsonOptions,
    ILogger<SqsProducerService> logger) : IProducerService
{
    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    /// <inheritdoc/>
    public async Task SendMessage(ProgramProject project, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(project, jsonOptions);
            var response = await client.SendMessageAsync(_queueName, json, cancellationToken);
            if (response.HttpStatusCode == HttpStatusCode.OK)
                logger.LogInformation("Program project {Id} was sent to file service via SQS", project.Id);
            else
                throw new Exception($"SQS returned {response.HttpStatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send program project {Id} through SQS queue", project.Id);
        }
    }
}
