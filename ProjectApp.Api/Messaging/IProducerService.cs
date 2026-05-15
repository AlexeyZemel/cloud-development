using ProjectApp.Domain.Entities;

namespace ProjectApp.Api.Messaging;

/// <summary>
/// Интерфейс службы для отправки сгенерированного программного проекта в брокер сообщений
/// </summary>
public interface IProducerService
{
    /// <summary>
    /// Отправляет сообщение с программным проектом в брокер
    /// </summary>
    /// <param name="project">Программный проект</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public Task SendMessage(ProgramProject project, CancellationToken cancellationToken = default);
}
