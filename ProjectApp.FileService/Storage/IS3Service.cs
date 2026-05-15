using System.Text.Json.Nodes;

namespace ProjectApp.FileService.Storage;

/// <summary>
/// Интерфейс службы для манипуляции файлами в объектном хранилище Minio
/// </summary>
public interface IS3Service
{
    /// <summary>
    /// Создаёт бакет, если он ещё не существует
    /// </summary>
    public Task EnsureBucketExists();

    /// <summary>
    /// Загружает сериализованный программный проект в объектное хранилище
    /// </summary>
    /// <param name="fileData">JSON-узел с описанием программного проекта</param>
    /// <returns>Признак успешной загрузки</returns>
    public Task<bool> UploadFile(JsonNode fileData);

    /// <summary>
    /// Возвращает список ключей всех файлов из бакета
    /// </summary>
    /// <returns>Список ключей файлов</returns>
    public Task<List<string>> GetFileList();

    /// <summary>
    /// Скачивает файл из объектного хранилища и возвращает его JSON-представление
    /// </summary>
    /// <param name="key">Ключ файла</param>
    /// <returns>JSON-узел с содержимым файла</returns>
    public Task<JsonNode> DownloadFile(string key);
}
