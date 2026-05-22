# InMoment

InMoment — мобильное приложение для обмена визуальным контентом в закрытых пользовательских группах.

Проект разработан в рамках ВКР и представляет собой production-ready систему с клиентом на Flutter и backend на ASP.NET Core.

## Структура проекта

Репозиторий содержит:

- backend_InMoment/ — backend на ASP.NET Core / .NET 8  
- frontend_InMoment/ — Flutter-клиент  

## Backend

cd backend_InMoment
docker compose up -d

Перед запуском backend нужно создать локальный файл:
backend_InMoment/InMoment/appsettings.Development.json

Этот файл не хранится в репозитории и должен содержать: 
- строку подключения к базе данных
- настройки JWT SigningKey
- при необходимости: MinIO/S3, SMTP, Firebase

Запуск:
dotnet restore
dotnet ef database update --project InMoment.Infrastructure --startup-project InMoment/InMoment.API.csproj
dotnet run --project InMoment/InMoment.API.csproj

## Frontend

cd frontend_InMoment
flutter pub get
flutter run \
  --dart-define=INMOMENT_FLAVOR=development \
  --dart-define=INMOMENT_API_BASE_URL=http://localhost:5293

Для Android-эмулятора используйте: `http://10.0.2.2:5293`

## Local setup

Backend требует локальный файл конфигурации:
backend_InMoment/InMoment/appsettings.Development.json

Этот файл должен содержать:
- database connection string
- JWT settings
- optional integrations (MinIO, SMTP, Firebase)
