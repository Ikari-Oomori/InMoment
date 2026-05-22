import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../api/profile_api.dart';

class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({super.key});

  static Future<void> show(BuildContext context) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      enableDrag: true,
      useSafeArea: true,
      backgroundColor: Colors.transparent,
      builder: (_) => const ChangePasswordPage(),
    );
  }

  @override
  State<ChangePasswordPage> createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final ProfileApi _api = ProfileApi();
  final TextEditingController _currentController = TextEditingController();
  final TextEditingController _nextController = TextEditingController();
  final TextEditingController _repeatController = TextEditingController();

  bool _saving = false;
  bool _hideCurrent = true;
  bool _hideNext = true;
  bool _hideRepeat = true;

  @override
  void dispose() {
    _currentController.dispose();
    _nextController.dispose();
    _repeatController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_saving) return;

    final current = _currentController.text;
    final next = _nextController.text;
    final repeat = _repeatController.text;

    if (current.isEmpty || next.isEmpty || repeat.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Заполните все поля')),
      );
      return;
    }

    if (next.length < 8) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Новый пароль должен быть не короче 8 символов'),
        ),
      );
      return;
    }

    if (next != repeat) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Новый пароль и повтор не совпадают')),
      );
      return;
    }

    setState(() {
      _saving = true;
    });

    try {
      await _api.changePassword(
        currentPassword: current,
        newPassword: next,
      );

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Пароль изменён')),
      );

      Navigator.of(context).pop();
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _saving = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось изменить пароль. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final bottomInset = media.viewInsets.bottom;

    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.fromLTRB(12, 0, 12, 12 + bottomInset),
        child: InMomentSurface(
          tone: InMomentSurfaceTone.elevated,
          borderRadius: BorderRadius.circular(28),
          padding: const EdgeInsets.fromLTRB(18, 10, 18, 18),
          child: SingleChildScrollView(
            physics: const BouncingScrollPhysics(),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(
                  child: Container(
                    width: 42,
                    height: 4,
                    decoration: BoxDecoration(
                      color: AppColors.textSecondary.withValues(alpha: 0.35),
                      borderRadius: BorderRadius.circular(999),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                const Text(
                  'Сменить пароль',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    height: 1.12,
                  ),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Обновите пароль для входа в аккаунт.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    height: 1.35,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 18),
                _PasswordField(
                  controller: _currentController,
                  label: 'Текущий пароль',
                  obscureText: _hideCurrent,
                  onToggleVisibility: () {
                    setState(() {
                      _hideCurrent = !_hideCurrent;
                    });
                  },
                ),
                const SizedBox(height: 10),
                _PasswordField(
                  controller: _nextController,
                  label: 'Новый пароль',
                  obscureText: _hideNext,
                  onToggleVisibility: () {
                    setState(() {
                      _hideNext = !_hideNext;
                    });
                  },
                ),
                const SizedBox(height: 10),
                _PasswordField(
                  controller: _repeatController,
                  label: 'Повторите новый пароль',
                  obscureText: _hideRepeat,
                  onToggleVisibility: () {
                    setState(() {
                      _hideRepeat = !_hideRepeat;
                    });
                  },
                ),
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: _saving ? null : _submit,
                    child: _saving
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Сохранить'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _PasswordField extends StatelessWidget {
  final TextEditingController controller;
  final String label;
  final bool obscureText;
  final VoidCallback onToggleVisibility;

  const _PasswordField({
    required this.controller,
    required this.label,
    required this.obscureText,
    required this.onToggleVisibility,
  });

  @override
  Widget build(BuildContext context) {
    return TextField(
      controller: controller,
      obscureText: obscureText,
      style: const TextStyle(
        color: AppColors.textPrimary,
        fontSize: 13.5,
        fontWeight: FontWeight.w600,
        height: 1.2,
      ),
      decoration: InputDecoration(
        labelStyle: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w600,
          color: AppColors.textSecondary,
        ),
        labelText: label,
        floatingLabelBehavior: FloatingLabelBehavior.never,
        contentPadding: const EdgeInsets.fromLTRB(16, 13, 10, 13),
        suffixIcon: IconButton(
          onPressed: onToggleVisibility,
          icon: Icon(
            obscureText
                ? Icons.visibility_outlined
                : Icons.visibility_off_outlined,
          ),
        ),
      ),
    );
  }
}