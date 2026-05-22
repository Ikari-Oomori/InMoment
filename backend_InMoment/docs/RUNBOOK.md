# InMoment Backend Runbook

## Актуальная точка входа
- Startup project: InMoment/InMoment.API.csproj
- Solution: InMoment.slnx

## Локальный запуск
1. Поднять инфраструктуру
docker compose up -d

2. Проверить контейнеры
docker compose ps

Ожидаемое состояние:
postgres — Up / healthy
minio — Up

3. Проверить MinIO
http://localhost:9001

Bucket должен существовать: inmoment

4. Применить миграции
dotnet ef database update --project InMoment.Infrastructure --startup-project InMoment/InMoment.API.csproj

5. Запустить API
dotnet run --project InMoment/InMoment.API.csproj

6. Проверить Swagger
открыть /swagger
## Канонические endpoints для Flutter
GET /api/users/me
GET /api/groups/my
GET /api/groups/{groupId}/feed/paged
GET /api/photos/{photoId}
GET /api/photos/{photoId}/comments/paged
POST /api/photos/{photoId}/comments
POST /api/photos/{photoId}/comments/reply
PATCH /api/comments/{commentId}
DELETE /api/comments/{commentId}
GET /api/groups/{groupId}/discussions

## Ограничения
Не использовать GET /feed без paged
Не использовать GET /comments без paged
Не использовать legacy GetComments
Не смешивать paged и non-paged flow

## Проверка перед Flutter
Swagger работает
База доступна
MinIO bucket существует
Feed работает через paged
Comments работают через paged
Legacy comments endpoint существует, но не используется во Flutter