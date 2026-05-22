import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import '../widgets/inmoment_feedback.dart';

class AppExternalActions {
  const AppExternalActions._();

  static Future<void> openMail(
    BuildContext context, {
    required String email,
    required String subject,
    String? body,
  }) async {
    final normalizedEmail = email.trim();

    if (normalizedEmail.isEmpty || !normalizedEmail.contains('@')) {
      if (!context.mounted) return;

      InMomentFeedback.showError(
        context,
        'Некорректный адрес почты.',
      );
      return;
    }

    final query = _mailtoQuery({
      'subject': subject.trim(),
      if (body != null && body.trim().isNotEmpty) 'body': body.trim(),
    });

    final uri = Uri.parse(
      'mailto:${Uri.encodeComponent(normalizedEmail)}${query.isEmpty ? '' : '?$query'}',
    );

    await _launchExternal(
      context,
      uri: uri,
      errorMessage: 'Не удалось открыть почтовое приложение.',
    );
  }

  static String _mailtoQuery(Map<String, String> values) {
    return values.entries
        .where((entry) => entry.value.trim().isNotEmpty)
        .map(
          (entry) =>
              '${Uri.encodeQueryComponent(entry.key)}=${Uri.encodeQueryComponent(entry.value).replaceAll('+', '%20')}',
        )
        .join('&');
  }

  static Future<void> openUrl(
    BuildContext context, {
    required String url,
    String? errorMessage,
  }) async {
    final uri = Uri.tryParse(url.trim());

    if (uri == null || !uri.hasScheme) {
      if (!context.mounted) return;

      InMomentFeedback.showError(
        context,
        errorMessage ?? 'Некорректная ссылка.',
      );
      return;
    }

    await _launchExternal(
      context,
      uri: uri,
      errorMessage: errorMessage ?? 'Не удалось открыть ссылку.',
    );
  }

  static Future<void> copyText(
    BuildContext context, {
    required String text,
    String successMessage = 'Скопировано.',
  }) async {
    await Clipboard.setData(ClipboardData(text: text));

    if (!context.mounted) return;

    InMomentFeedback.showSuccess(context, successMessage);
  }

  static Future<void> _launchExternal(
    BuildContext context, {
    required Uri uri,
    required String errorMessage,
  }) async {
    var launched = false;

    try {
      launched = await launchUrl(
        uri,
        mode: LaunchMode.externalApplication,
      );
    } catch (_) {
      launched = false;
    }

    if (!context.mounted) return;

    if (!launched) {
      InMomentFeedback.showError(context, errorMessage);
    }
  }
}