import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/config/app_contacts.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/auth_api.dart';
import 'reset_password_page.dart';

class ForgotPasswordPage extends StatefulWidget {
  final String initialEmail;

  const ForgotPasswordPage({
    super.key,
    this.initialEmail = '',
  });

  @override
  State<ForgotPasswordPage> createState() => _ForgotPasswordPageState();
}

class _ForgotPasswordPageState extends State<ForgotPasswordPage> {
  final _controller = TextEditingController();
  final _api = AuthApi();

  bool _loading = false;
  bool _done = false;
  String? _error;

  bool _isValidEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  bool get _canSubmit => !_loading && _isValidEmail(_controller.text.trim());

  @override
  void initState() {
    super.initState();
    _controller.text = widget.initialEmail;
  }

  Future<void> _submit() async {
    if (_loading) return;

    final email = _controller.text.trim();

    if (!_isValidEmail(email)) {
      setState(() {
        _error = 'Введите корректный email.';
      });
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _api.forgotPassword(email: email);

      if (!mounted) return;
      setState(() {
        _done = true;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = ApiError.normalize(
          e,
          fallback:
              'Не удалось отправить запрос на восстановление пароля. Попробуйте ещё раз.',
        );
      });
    } finally {
      if (mounted) {
        setState(() {
          _loading = false;
        });
      }
    }
  }

  void _openResetPage() {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const ResetPasswordPage(),
      ),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Восстановление пароля',
      contentPadding: EdgeInsets.fromLTRB(
        16,
        16,
        16,
        28 + MediaQuery.of(context).viewInsets.bottom,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const InMomentSection(
            title: 'Введите email',
            subtitle:
                'Мы отправим письмо для восстановления пароля. Если аккаунт существует, в письме будет ссылка и токен для сброса.',
            child: SizedBox.shrink(),
          ),
          const SizedBox(height: 12),
          InMomentSection(
            title: 'Email аккаунта',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(
                  controller: _controller,
                  enabled: !_loading,
                  keyboardType: TextInputType.emailAddress,
                  textInputAction: TextInputAction.done,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: const InputDecoration(
                    hintText: 'Email',
                    prefixIcon: Icon(Icons.mail_outline_rounded),
                  ),
                  onChanged: (_) {
                    setState(() {
                      _error = null;
                      _done = false;
                    });
                  },
                  onSubmitted: (_) {
                    if (_canSubmit) {
                      _submit();
                    }
                  },
                ),
                const SizedBox(height: 14),
                FilledButton(
                  onPressed: _canSubmit ? _submit : null,
                  child: _loading
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Отправить письмо'),
                ),
                const SizedBox(height: 10),
                OutlinedButton(
                  onPressed: _loading ? null : _openResetPage,
                  child: const Text('У меня уже есть токен'),
                ),
              ],
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            _RecoveryStateCard(
              title: 'Не удалось отправить запрос',
              text: _error!,
              tone: InMomentSurfaceTone.danger,
              icon: Icons.error_outline_rounded,
            ),
          ],
          if (_done) ...[
            const SizedBox(height: 12),
            _RecoveryStateCard(
              title: 'Запрос обработан',
              text:
                  'Проверьте почту. Если письмо открыто на другом устройстве или ссылка не сработала, можно вручную вставить токен на следующем экране.',
              tone: InMomentSurfaceTone.overlay,
              icon: Icons.mark_email_read_outlined,
            ),
          ],
          const SizedBox(height: 12),
          InMomentSection(
            title: 'Если письма нет',
            subtitle:
                'На этапе разработки письмо может не прийти из-за SMTP. В этом случае токен и ссылка будут доступны в backend-логах.',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: const [
                _HintRow(text: 'Проверьте папки «Спам» и «Промоакции».'),
                SizedBox(height: 8),
                _HintRow(text: 'Если SMTP недоступен, используйте токен из логов backend.'),
                SizedBox(height: 8),
                _HintRow(
                  text:
                      'Контакт для ручной поддержки: ${AppContacts.supportEmail}',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _RecoveryStateCard extends StatelessWidget {
  final String title;
  final String text;
  final InMomentSurfaceTone tone;
  final IconData icon;

  const _RecoveryStateCard({
    required this.title,
    required this.text,
    required this.tone,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: tone,
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: AppColors.textPrimary, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 14,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  text,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    height: 1.45,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _HintRow extends StatelessWidget {
  final String text;

  const _HintRow({
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Padding(
          padding: EdgeInsets.only(top: 2),
          child: Icon(
            Icons.check_circle_outline_rounded,
            size: 16,
            color: AppColors.textSecondary,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            text,
            style: const TextStyle(
              color: AppColors.textSecondary,
              fontSize: 13,
              height: 1.4,
            ),
          ),
        ),
      ],
    );
  }
}