# Backend Feature Matrix

## Общий статус
- Backend реализует основные серверные сценарии InMoment: авторизацию, группы, приглашения, публикации, комментарии, реакции, воспоминания, уведомления, приватность, жалобы и модерацию.
- Локальная инфраструктура для разработки поднимается через Docker Compose: PostgreSQL и MinIO.
- основные backend-модули реализованы

---

## Auth
- Register
- Login
- Refresh
- Logout / Logout all
- Forgot password
- Reset password
- Sessions list
- Session revoke

Статус:  реализовано

---

## User Profile
- Get me
- Update profile
- Set profile photo
- Active group
- Onboarding state
- Widget data

Статус:  реализовано

---

## Groups
- Create group
- My groups list
- Manage members
- Roles / ownership
- Invite by user
- Join by code
- Group settings
- Group avatar
- Transfer ownership
- Leave group

Статус:  реализовано

---

## Feed (Group)
Канонический endpoint:
- GET /api/groups/{groupId}/feed/paged

Дополнительно:
- GET /api/groups/{groupId}/feed (не рекомендуется использовать во Flutter)

Статус:  реализовано (использовать paged)

---

## Photo Details
- GET /api/photos/{photoId}

Статус:  реализовано

---

## Comments
Канонический flow:
- GET /api/photos/{photoId}/comments/paged
- POST /api/photos/{photoId}/comments
- POST /api/photos/{photoId}/comments/reply
- PATCH /api/comments/{commentId}
- DELETE /api/comments/{commentId}

Дополнительно:
- GET /api/photos/{photoId}/comments (не рекомендуется использовать во Flutter)

Legacy:
- GetComments (не использовать, подлежит удалению)

Статус:  реализовано (использовать paged)

---

## Discussions
- GET /api/groups/{groupId}/discussions

Описание:
- список фото с активностью комментариев
- не является чатом

Статус:  реализовано

---

## Reactions
- Set reaction
- Change reaction
- Remove reaction
- Get reactions summary

Статус:  реализовано

---

## Memories
- Group memories
- Personal memories
- Calendar stats
- Photos by date

Статус:  реализовано

---

## Notifications
- Notifications list
- Unread count
- Mark read
- Mark all read
- Notification settings
- Device tokens

Статус:  реализовано (без полного production hardening)

---

## Privacy / Safety
- Privacy settings
- Blocks
- Reports
- Moderation base
- Delete account
- Sessions control

Статус:  реализовано

---

## Contacts
- Import contacts
- Match users
- External invites
- Search suggestions

Статус:  реализовано (проверить UX на фронте)

---

## Ключевые правила перед Flutter

- Использовать только paged endpoints для feed и comments
- Не использовать legacy comments flow
- Не строить UI на простых list endpoints
- Photo screen = photo details + comments paged
- Discussions экран = отдельный endpoint discussions