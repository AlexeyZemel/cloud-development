# Современные технологии разработки программного обеспечения

**Вариант:** №7 — «Программный проект»  
**Балансировка:** Weighted Random  
**Брокер:** SQS  
**Хостинг S3:** Minio  

## Что делает проект

- Генерирует тестовые данные о программных проектах с помощью Bogus.
- Возвращает данные проекта по `id` через HTTP API.
- Кеширует результаты в Redis для повторных запросов.
- Предоставляет Swagger для просмотра и проверки API в режиме разработки.
- Публикует health-check эндпоинты и телеметрию на базе OpenTelemetry через Aspire service defaults.

## Структура решения

- `ProjectApp.Api` - ASP.NET Core Web API с логикой генерации, Redis-кешем, Swagger, CORS и контроллерами.
- `ProjectApp.AppHost` - проект оркестрации на .NET Aspire, который поднимает API, Redis, Redis Commander и клиент.
- `ProjectApp.Domain` - доменные сущности, используемые в решении.
- `ProjectApp.ServiceDefaults` - общая конфигурация Aspire: телеметрия, service discovery, resilience и health checks.
- `Client.Wasm` - клиент на Blazor WebAssembly для взаимодействия с API.

## Технологии

- .NET 8
- ASP.NET Core Web API
- .NET Aspire
- Redis
- Bogus
- Swagger / OpenAPI
- OpenTelemetry
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

1. Клиент запрос в API с идентификатором проекта.
2. API пытается прочитать данные из Redis по ключу формата `software-project-{id}`.
3. Если значение найдено в кеше, возвращается сохраненный объект.
4. Если значения нет или Redis недоступен, создается новый `ProgramProject` с помощью Bogus.
5. Сгенерированный объект сохраняется в Redis на заданное время жизни и возвращается клиенту.

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

## Особенности реализации

- API использует распределенный кеш через `IDistributedCache` с хранилищем в Redis.
- Ошибки кеша не ломают обработку запроса: API логирует проблему и продолжает генерацию данных.
