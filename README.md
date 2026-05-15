# Современные технологии разработки программного обеспечения

**Вариант:** №7 — «Программный проект»  
**Балансировка:** Weighted Random  
**Брокер:** SQS  
**Хостинг S3:** Minio  

## Что делает проект

- Генерирует тестовые данные о программных проектах с помощью Bogus.
- Возвращает данные проекта по `id` через HTTP API.
- Кеширует результаты в Redis для повторных запросов.
- После генерации отправляет данные проекта в файловый сервис через SQS.
- Файловый сервис сериализует JSON в файл `project_{id}.json` и сохраняет его в Minio.
- Использует API Gateway (Ocelot) для маршрутизации запросов.
- Балансирует нагрузку между несколькими экземплярами API с помощью алгоритма **Weighted Random**.
- Поднимает всю инфраструктуру через .NET Aspire.
- Предоставляет Swagger для тестирования API.
- Публикует health-check и телеметрию через OpenTelemetry.
- Имеет интеграционные тесты, которые проверяют всю цепочку работы backend-сервисов вместе.

## Структура решения

- `ProjectApp.Api` - ASP.NET Core Web API с генерацией данных, Redis-кешем и отправкой сообщений в SQS.
- `ProjectApp.Gateway` - API Gateway на базе **Ocelot** с балансировкой нагрузки Weighted Random.
- `ProjectApp.FileService` - сервис для приема сообщений из SQS, сериализации данных в JSON и работы с Minio.
- `ProjectApp.AppHost` - проект оркестрации на .NET Aspire, который поднимает API, Gateway, FileService, Redis, LocalStack и Minio.
- `ProjectApp.AppHost.Tests` - интеграционные тесты для проверки полного backend-пайплайна.
- `ProjectApp.Domain` - доменные сущности, используемые в решении.
- `ProjectApp.ServiceDefaults` - общая конфигурация Aspire: телеметрия, service discovery, resilience и health checks.
- `Client.Wasm` - клиент на Blazor WebAssembly для взаимодействия с API.

## Технологии

- .NET 8
- ASP.NET Core Web API
- .NET Aspire
- Redis
- Amazon SQS
- Minio
- LocalStack
- Bogus
- Ocelot (API Gateway)
- Swagger / OpenAPI
- OpenTelemetry
- xUnit
- Blazor WebAssembly

## Основная сущность

API работает с моделью `ProgramProject`:

- `Id`
- `ProjectName`
- `Customer`
- `ProjectManager`
- `StartDate`
- `PlannedEndDate`
- `ActualEndDate`
- `Budget`
- `ActualCost`
- `CompletionPercentage`

## Как это работает

1. Клиент отправляет запрос в API с идентификатором проекта.
2. API пытается прочитать данные из Redis по ключу формата `software-project-{id}`.
3. Если значение найдено в кеше, возвращается сохраненный объект.
4. Если значения нет или Redis недоступен, создается новый `ProgramProject` с помощью Bogus.
5. Сгенерированный объект сохраняется в Redis на заданное время жизни.
6. После этого API сериализует проект и отправляет его в очередь SQS.
7. Файловый сервис получает сообщение из очереди, превращает JSON в файл и сохраняет его в Minio под ключом `project_{id}.json`.
8. Интеграционные тесты поднимают весь стек через AppHost и проверяют, что объект появился в Minio и совпадает с ответом API.

По умолчанию время жизни кеша составляет 10 минут и задается в `ProjectApp.Api/appsettings.json`.

## API

Основной эндпоинт:

```http
GET /api/project?id=1
```

Пример запроса:

```bash
curl "http://localhost:5179/api/project?id=1"
```

Пример структуры ответа:

```json
{
  "id": 1,
  "projectName": "...",
  "customer": "...",
  "projectManager": "...",
  "startDate": "2024-04-01",
  "plannedEndDate": "2025-01-10",
  "actualEndDate": null,
  "budget": 1200000.50,
  "actualCost": 640000.25,
  "completionPercentage": 54
}
```

Если вызвать эндпоинт несколько раз с одним и тем же `id` в пределах времени жизни кеша, API должно вернуть один и тот же объект из Redis.

## API файлового сервиса

Основные эндпоинты:

```http
GET /api/s3
GET /api/s3/project_1.json
```

Первый эндпоинт возвращает список файлов в бакете, второй - содержимое выбранного JSON-файла из Minio.

## Особенности реализации

- API использует распределенный кеш через `IDistributedCache` с хранилищем в Redis.
- Ошибки кеша не ломают обработку запроса: API логирует проблему и продолжает генерацию данных.
- Используется 5 реплик API.

## Балансировка нагрузки

Реализован алгоритм: Weighted Random

- Каждой реплике задаётся вес:
```json
"Weights": [0.4, 0.25, 0.15, 0.1, 0.1]
```

Запрос распределяется случайно, но с учётом весов.
