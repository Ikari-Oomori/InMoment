class AppContacts {
  const AppContacts._();

  static const String supportEmail = String.fromEnvironment(
    'INMOMENT_SUPPORT_EMAIL',
    defaultValue: 'support@inmoment.app',
  );

  static const String privacyEmail = String.fromEnvironment(
    'INMOMENT_PRIVACY_EMAIL',
    defaultValue: 'privacy@inmoment.app',
  );

  static const String legalEmail = String.fromEnvironment(
    'INMOMENT_LEGAL_EMAIL',
    defaultValue: 'legal@inmoment.app',
  );

  static const String supportLabel = String.fromEnvironment(
    'INMOMENT_SUPPORT_LABEL',
    defaultValue: 'Поддержка InMoment',
  );

  static const String privacyLabel = String.fromEnvironment(
    'INMOMENT_PRIVACY_LABEL',
    defaultValue: 'Приватность и данные',
  );

  static const String legalLabel = String.fromEnvironment(
    'INMOMENT_LEGAL_LABEL',
    defaultValue: 'Юридические вопросы',
  );

  static const String supportAvailabilityNote =
      'На текущем этапе обращения обрабатываются по электронной почте. Ответ может занять больше времени в зависимости от нагрузки и характера запроса.';

  static const String publicDocumentsNote =
      'Публичные документы размещены отдельно по внешним ссылкам. Внутри приложения доступны краткие версии и основные сведения.';

  static const String passwordResetScheme = 'inmoment';
  static const String passwordResetHost = 'reset-password';

  static bool get hasProductionContactEmails {
    return !_isPersonalOrPlaceholderEmail(supportEmail) &&
        !_isPersonalOrPlaceholderEmail(privacyEmail) &&
        !_isPersonalOrPlaceholderEmail(legalEmail);
  }

  static List<String> get productionWarnings {
    final warnings = <String>[];

    if (_isPersonalOrPlaceholderEmail(supportEmail)) {
      warnings.add('INMOMENT_SUPPORT_EMAIL points to a personal/test email.');
    }

    if (_isPersonalOrPlaceholderEmail(privacyEmail)) {
      warnings.add('INMOMENT_PRIVACY_EMAIL points to a personal/test email.');
    }

    if (_isPersonalOrPlaceholderEmail(legalEmail)) {
      warnings.add('INMOMENT_LEGAL_EMAIL points to a personal/test email.');
    }

    return warnings;
  }

  static bool _isPersonalOrPlaceholderEmail(String value) {
    final normalized = value.trim().toLowerCase();

    if (normalized.isEmpty) return true;
    if (normalized.endsWith('@gmail.com')) return true;
    if (normalized.endsWith('@example.com')) return true;
    if (normalized.contains('your-domain')) return true;
    if (normalized.contains('placeholder')) return true;
    if (!normalized.contains('@')) return true;

    return false;
  }
}