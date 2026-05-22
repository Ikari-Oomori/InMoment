# InMoment Backend

InMoment — backend мобильного приложения для обмена визуальным контентом внутри закрытых пользовательских групп.

## Основные возможности

- регистрация и авторизация пользователей
- JWT access token + refresh sessions
- восстановление пароля
- приватные группы
- приглашения в группы и join by code
- активная группа пользователя
- публикация фото и видео
- обработка и хранение медиафайлов
- контент-лента группы
- комментарии и ответы
- реакции
- memories / calendar endpoints
- privacy / blocks
- reports / moderation base
- notifications
- notification settings
- device tokens + push foundation
- import contacts
- external contact invites
- SignalR realtime hubs
- presigned uploads для S3-compatible хранилища

## Архитектура

Решение разделено на слои:

- `InMoment.Domain` — доменная модель и бизнес-правила
- `InMoment.Application` — use cases, handlers, abstractions
- `InMoment.Infrastructure` — EF Core, repositories, external services
- `InMoment.API` — HTTP API
- `InMoment.Test` — application, domain, infrastructure, integration tests

## Технологии

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT
- SignalR
- S3-compatible storage
- MinIO для локальной разработки

## Структура решения

Актуальная точка входа API:

- `InMoment/Program.cs`
- `InMoment/InMoment.API.csproj`


### Локальный запуск

1. Поднять инфраструктуру
Команда: docker compose up -d
Файл находится в корне backend-проекта: `backend_InMoment/docker-compose.yml`

Поднимаются:
- PostgreSQL: localhost:5432
- MinIO API: http://localhost:9000
- MinIO Console: http://localhost:9001

2. Создать локальный конфиг API
Создай файл: InMoment/appsettings.Development.json

И заполни его локальными dev-значениями (файл не должен попадать в репозиторий).

3. Восстановить пакеты
Команда: `dotnet restore`

4. Применить миграции

Через Visual Studio (Package Manager Console):
Startup Project: InMoment/InMoment.API.csproj
Default Project: InMoment.Infrastructure

dotnet ef database update --project InMoment.Infrastructure --startup-project InMoment/InMoment.API.csproj

Update-Database -StartupProject InMoment -Project InMoment.Infrastructure

Или через CLI: dotnet ef database update --project InMoment.Infrastructure --startup-project InMoment

5. Запустить API
Команда: dotnet run --project InMoment/InMoment.API.csproj

## Тесты

Запуск всех тестов:
dotnet test

## Coverage

Из покрытия исключены:
- EF Core migrations
- ModelSnapshot
- Designer files

Это сделано для честного отображения покрытия прикладной логики.