class AppLinks {
  const AppLinks._();

  static const String websiteBaseUrl = String.fromEnvironment(
    'INMOMENT_WEBSITE_URL',
    defaultValue: 'https://sites.google.com/view/inmoment-docs',
  );

  static const String privacyPolicyUrl = String.fromEnvironment(
    'INMOMENT_PRIVACY_POLICY_URL',
    defaultValue: 'https://sites.google.com/view/inmoment-docs/privacy',
  );

  static const String termsUrl = String.fromEnvironment(
    'INMOMENT_TERMS_URL',
    defaultValue: 'https://sites.google.com/view/inmoment-docs/terms',
  );

  static const String dataDeletionUrl = String.fromEnvironment(
    'INMOMENT_DATA_DELETION_URL',
    defaultValue: 'https://sites.google.com/view/inmoment-docs/data-deletion',
  );

  static const String supportUrl = String.fromEnvironment(
    'INMOMENT_SUPPORT_URL',
    defaultValue: 'https://sites.google.com/view/inmoment-docs/support',
  );

  static const String appShareUrl = String.fromEnvironment(
    'INMOMENT_APP_SHARE_URL',
    defaultValue: websiteBaseUrl,
  );

  static String inviteDeepLink(String code) {
    final normalized = code.trim().toUpperCase();
    return 'inmoment://invite/group/$normalized';
  }

  static String invitePublicLink(String code) {
    final normalized = Uri.encodeComponent(code.trim().toUpperCase());
    final base = _withoutTrailingSlash(websiteBaseUrl);
    return '$base/invite?code=$normalized';
  }

  static const String publicDocumentsHostLabel = String.fromEnvironment(
    'INMOMENT_PUBLIC_DOCUMENTS_HOST_LABEL',
    defaultValue: 'Google Sites',
  );

  static const String publicDocumentsStatusLabel = String.fromEnvironment(
    'INMOMENT_PUBLIC_DOCUMENTS_STATUS_LABEL',
    defaultValue: 'Промежуточное публичное размещение документов',
  );

  static bool get hasProductionPublicLinks {
    return !_isTemporaryUrl(websiteBaseUrl) &&
        !_isTemporaryUrl(privacyPolicyUrl) &&
        !_isTemporaryUrl(termsUrl) &&
        !_isTemporaryUrl(dataDeletionUrl) &&
        !_isTemporaryUrl(supportUrl);
  }

  static List<String> get productionWarnings {
    final warnings = <String>[];

    if (_isTemporaryUrl(websiteBaseUrl)) {
      warnings.add('INMOMENT_WEBSITE_URL points to a temporary document host.');
    }

    if (_isTemporaryUrl(privacyPolicyUrl)) {
      warnings.add(
        'INMOMENT_PRIVACY_POLICY_URL points to a temporary document host.',
      );
    }

    if (_isTemporaryUrl(termsUrl)) {
      warnings.add('INMOMENT_TERMS_URL points to a temporary document host.');
    }

    if (_isTemporaryUrl(dataDeletionUrl)) {
      warnings.add(
        'INMOMENT_DATA_DELETION_URL points to a temporary document host.',
      );
    }

    if (_isTemporaryUrl(supportUrl)) {
      warnings.add('INMOMENT_SUPPORT_URL points to a temporary document host.');
    }

    return warnings;
  }

  static String _withoutTrailingSlash(String value) {
    var result = value.trim();

    while (result.endsWith('/')) {
      result = result.substring(0, result.length - 1);
    }

    return result;
  }

  static bool _isTemporaryUrl(String value) {
    final normalized = value.trim().toLowerCase();

    if (normalized.isEmpty) return true;
    if (normalized.contains('sites.google.com')) return true;
    if (normalized.contains('your-domain')) return true;
    if (normalized.contains('example.com')) return true;
    if (normalized.contains('placeholder')) return true;

    return false;
  }
}