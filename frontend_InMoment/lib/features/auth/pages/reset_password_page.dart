import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_section.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/auth_api.dart';

class ResetPasswordPage extends StatefulWidget {
  final String initialToken;
  final bool openedFromLink;

  const ResetPasswordPage({
    super.key,
    this.initialToken = '',
    this.openedFromLink = false,
  });

  @override
  State<ResetPasswordPage> createState() => _ResetPasswordPageState();
}

class _ResetPasswordPageState extends State<ResetPasswordPage> {
  final _tokenController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _api = AuthApi();

  bool _loading = false;
  bool _done = false;
  bool _obscurePassword = true;
  bool _obscureConfirm = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _tokenController.text = widget.initialToken;
  }

  bool get _canSubmit {
    return !_loading &&
        _tokenController.text.trim().isNotEmpty &&
        _passwordController.text.isNotEmpty &&
        _confirmPasswordController.text.isNotEmpty;
  }

  String? _validate() {
    final token = _tokenController.text.trim();
    final password = _passwordController.text;
    final confirm = _confirmPasswordController.text;

    if (token.isEmpty) {
      return 'Введите токен из письма или откройте ссылку восстановления.';
    }

    if (password.length < 6) {
      return 'Новый пароль должен содержать не менее 6 символов.';
    }

    if (password != confirm) {
      return 'Пароли не совпадают.';
    }

    return null;
  }

  Future<void> _submit() async {
    if (_loading) return;

    final validationError = _validate();
    if (validationError != null) {
      setState(() {
        _error = validationError;
      });
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      await _api.resetPassword(
        token: _tokenController.text.trim(),
        newPassword: _passwordController.text,
      );

      if (!mounted) return;

      setState(() {
        _done = true;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось сбросить пароль. Проверьте данные и попробуйте ещё раз.',
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

  @override
  void dispose() {
    _tokenController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Сброс пароля',
      contentPadding: EdgeInsets.fromLTRB(
        16,
        16,
        16,
        28 + MediaQuery.of(context).viewInsets.bottom,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          InMomentSection(
            title: 'Создайте новый пароль',
            subtitle: widget.openedFromLink
                ? 'Ссылка восстановления распознана. Осталось задать новый пароль.'
                : 'Введите токен из письма и задайте новый пароль для аккаунта.',
            child: const SizedBox.shrink(),
          ),
          const SizedBox(height: 12),
          InMomentSection(
            title: 'Данные для сброса',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(
                  controller: _tokenController,
                  enabled: !_loading,
                  textInputAction: TextInputAction.next,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: const InputDecoration(
                    hintText: 'Токен восстановления',
                    prefixIcon: Icon(Icons.key_rounded),
                  ),
                  onChanged: (_) {
                    setState(() {
                      _error = null;
                      _done = false;
                    });
                  },
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: _passwordController,
                  enabled: !_loading,
                  obscureText: _obscurePassword,
                  textInputAction: TextInputAction.next,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: InputDecoration(
                    hintText: 'Новый пароль',
                    prefixIcon: const Icon(Icons.lock_outline_rounded),
                    suffixIcon: IconButton(
                    onPressed: _loading
                        ? null
                        : () {
                            setState(() {
                              _obscurePassword = !_obscurePassword;
                            });
                      },
                      icon: Icon(
                        _obscurePassword
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                    ),
                  ),
                  onChanged: (_) {
                    setState(() {
                      _error = null;
                      _done = false;
                    });
                  },
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: _confirmPasswordController,
                  enabled: !_loading,
                  obscureText: _obscureConfirm,
                  textInputAction: TextInputAction.done,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: InputDecoration(
                    hintText: 'Повторите новый пароль',
                    prefixIcon: const Icon(Icons.verified_user_outlined),
                    suffixIcon: IconButton(
                      onPressed: _loading
                        ? null
                        : () {
                            setState(() {
                               _obscureConfirm = !_obscureConfirm;
                            });
                      },
                      icon: Icon(
                        _obscureConfirm
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                    ),
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
                      : const Text('Сохранить новый пароль'),
                ),
                if (_done) ...[
                  const SizedBox(height: 10),
                  OutlinedButton(
                    onPressed: () {
                      Navigator.of(context).popUntil((route) => route.isFirst);
                    },
                    child: const Text('Вернуться ко входу'),
                  ),
                ],
              ],
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            _RecoveryStateCard(
              title: 'Сброс не выполнен',
              text: _error!,
              tone: InMomentSurfaceTone.danger,
              icon: Icons.error_outline_rounded,
            ),
          ],
          if (_done) ...[
            const SizedBox(height: 12),
            const _RecoveryStateCard(
              title: 'Пароль обновлён',
              text:
                  'Пароль успешно изменён. Теперь можно вернуться на экран входа и войти с новым паролем.',
              tone: InMomentSurfaceTone.overlay,
              icon: Icons.check_circle_outline_rounded,
            ),
          ],
          const SizedBox(height: 12),
          const InMomentSection(
            title: 'Важно',
            subtitle:
                'После успешного сброса backend отзывает все активные refresh-сессии пользователя. Это ожидаемое поведение безопасности.',
            child: SizedBox.shrink(),
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