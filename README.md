# InMoment

InMoment — мобильное приложение для обмена визуальным контентом в закрытых пользовательских группах.

Репозиторий содержит:

- backend_InMoment/ — backend на ASP.NET Core / .NET 8  
- frontend_InMoment/ — Flutter-клиент  

## Backend

cd backend_InMoment
docker compose up -d

Перед запуском backend нужно создать локальный файл:
backend_InMoment/InMoment/appsettings.Development.json

Этот файл не хранится в репозитории и должен содержать локальные строки подключения, JWT SigningKey, MinIO/S3 и SMTP/Firebase-настройки при необходимости.

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

Backend requires local configuration file:

backend_InMoment/InMoment/appsettings.Development.json

This file is not included in the repository and must contain:
- database connection string
- JWT settings
- optional integrations (MinIO, SMTP, Firebase)