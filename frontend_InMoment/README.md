# InMoment Flutter

Клиентское приложение InMoment для Android, iOS, Web и Desktop.

## Запуск

flutter pub get

flutter run 
  --dart-define=INMOMENT_FLAVOR=development 
  --dart-define=INMOMENT_API_BASE_URL=http://localhost:5293

Для Android-эмулятора используйте:
http://10.0.2.2:5293

Для production задаются:
  --dart-define=INMOMENT_WEBSITE_URL=https://your-domain
  --dart-define=INMOMENT_PRIVACY_POLICY_URL=https://your-domain/privacy
  --dart-define=INMOMENT_TERMS_URL=https://your-domain/terms
  --dart-define=INMOMENT_DATA_DELETION_URL=https://your-domain/data-deletion
  --dart-define=INMOMENT_SUPPORT_URL=https://your-domain/support
  --dart-define=INMOMENT_SUPPORT_EMAIL=support@your-domain
  --dart-define=INMOMENT_PRIVACY_EMAIL=privacy@your-domain
  --dart-define=INMOMENT_LEGAL_EMAIL=legal@your-domain
  
## Проверка

flutter analyze
flutter test