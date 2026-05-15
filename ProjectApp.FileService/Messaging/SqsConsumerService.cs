using Amazon.SQS;
using Amazon.SQS.Model;
using ProjectApp.FileService.Storage;
using System.Text.Json.Nodes;

namespace ProjectApp.FileService.Messaging;

/// <summary>
/// Фоновая служба для приёма сообщений из очереди SQS и загрузки тел сообщений в Minio
/// </summary>
/// <param name="sqsClient">Клиент SQS</param>
/// <param name="scopeFactory">Фабрика областей действия для разрешения scoped-зависимостей</param>
/// <param name="configuration">Конфигурация приложения</param>
/// <param name="logger">Логгер</param>
public class SqsConsumerService(
    IAmazonSQS sqsClient,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    private readonly string _queueName = configuration["AWS:Resources:SQSQueueName"]
        ?? throw new KeyNotFoundException("SQS queue name was not found in configuration");

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SQS consumer service started for queue {Queue}", _queueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _queueName,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 5
                }, stoppingToken);

                if (response?.Messages == null || response.Messages.Count == 0)
                    continue;

                logger.LogInformation("Received {Count} messages from {Queue}", response.Messages.Count, _queueName);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        logger.LogInformation("Processing message {MessageId}", message.MessageId);

                        var node = JsonNode.Parse(message.Body)
                            ?? throw new InvalidOperationException("Message body is not a valid JSON");

                        using var scope = scopeFactory.CreateScope();
                        var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
                        await s3Service.UploadFile(node);

                        await sqsClient.DeleteMessageAsync(_queueName, message.ReceiptHandle, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error while polling SQS queue {Queue}", _queueName);
            }
        }
    }
}
