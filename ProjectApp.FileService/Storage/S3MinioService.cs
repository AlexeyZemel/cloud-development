using Minio;
using Minio.DataModel.Args;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace ProjectApp.FileService.Storage;

/// <summary>
/// Служба для манипуляции файлами программных проектов в объектном хранилище Minio
/// </summary>
/// <param name="client">Клиент Minio</param>
/// <param name="configuration">Конфигурация приложения</param>
/// <param name="logger">Логгер</param>
public class S3MinioService(
    IMinioClient client,
    IConfiguration configuration,
    ILogger<S3MinioService> logger) : IS3Service
{
    private readonly string _bucketName = configuration["AWS:Resources:MinioBucketName"]
        ?? throw new KeyNotFoundException("Minio bucket name was not found in configuration");

    /// <inheritdoc/>
    public async Task EnsureBucketExists()
    {
        logger.LogInformation("Checking whether {Bucket} exists", _bucketName);
        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await client.BucketExistsAsync(existsArgs);
            if (!exists)
            {
                logger.LogInformation("Creating {Bucket}", _bucketName);
                var createArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await client.MakeBucketAsync(createArgs);
                return;
            }
            logger.LogInformation("{Bucket} already exists", _bucketName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred during {Bucket} existence check", _bucketName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UploadFile(JsonNode fileData)
    {
        var id = fileData["id"]?.GetValue<int>()
            ?? throw new ArgumentException("Passed JSON has invalid structure (id missing)");

        var bytes = Encoding.UTF8.GetBytes(fileData.ToJsonString());
        using var stream = new MemoryStream(bytes);
        stream.Seek(0, SeekOrigin.Begin);

        var objectKey = $"project_{id}.json";
        logger.LogInformation("Began uploading program project {Id} as {Key} into {Bucket}", id, objectKey, _bucketName);

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithContentType("application/json");

        var response = await client.PutObjectAsync(args);

        if (response.ResponseStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to upload {Key}: {Code}", objectKey, response.ResponseStatusCode);
            return false;
        }
        logger.LogInformation("Finished uploading program project {Id} into {Bucket}", id, _bucketName);
        return true;
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetFileList()
    {
        var list = new List<string>();
        var args = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithPrefix("")
            .WithRecursive(true);

        logger.LogInformation("Began listing files in {Bucket}", _bucketName);
        var items = client.ListObjectsEnumAsync(args);
        await foreach (var item in items)
            list.Add(item.Key);
        return list;
    }

    /// <inheritdoc/>
    public async Task<JsonNode> DownloadFile(string key)
    {
        logger.LogInformation("Began downloading {Key} from {Bucket}", key, _bucketName);
        try
        {
            var memoryStream = new MemoryStream();

            var args = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(async (stream, ct) =>
                {
                    await stream.CopyToAsync(memoryStream, ct);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                });

            var response = await client.GetObjectAsync(args) ?? throw new InvalidOperationException($"Error occurred downloading {key}: response is null");
            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            return JsonNode.Parse(reader.ReadToEnd())
                ?? throw new InvalidOperationException("Downloaded document is not a valid JSON");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during downloading {Key}", key);
            throw;
        }
    }
}
