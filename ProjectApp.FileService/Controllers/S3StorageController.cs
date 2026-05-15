using Microsoft.AspNetCore.Mvc;
using ProjectApp.FileService.Storage;
using System.Text.Json.Nodes;

namespace ProjectApp.FileService.Controllers;

/// <summary>
/// Контроллер для взаимодействия с объектным хранилищем Minio
/// </summary>
/// <param name="s3Service">Служба для работы с S3/Minio</param>
/// <param name="logger">Логгер</param>
[ApiController]
[Route("api/s3")]
public class S3StorageController(IS3Service s3Service, ILogger<S3StorageController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает список ключей файлов, хранящихся в бакете
    /// </summary>
    /// <returns>Список ключей файлов</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<string>>> ListFiles()
    {
        logger.LogInformation("Listing files in bucket");
        try
        {
            var list = await s3Service.GetFileList();
            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list files");
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Возвращает содержимое файла из объектного хранилища по ключу
    /// </summary>
    /// <param name="key">Ключ файла в бакете</param>
    /// <returns>JSON содержимое файла</returns>
    [HttpGet("{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JsonNode>> GetFile(string key)
    {
        logger.LogInformation("Downloading {Key}", key);
        try
        {
            var node = await s3Service.DownloadFile(key);
            return Ok(node);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download {Key}", key);
            return NotFound(ex.Message);
        }
    }
}
