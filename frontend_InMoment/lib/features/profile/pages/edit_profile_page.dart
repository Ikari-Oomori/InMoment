import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../api/profile_api.dart';
import '../models/user_profile.dart';

class EditProfilePage extends StatefulWidget {
  final UserProfile profile;

  const EditProfilePage({
    super.key,
    required this.profile,
  });

  @override
  State<EditProfilePage> createState() => _EditProfilePageState();
}

class _EditProfilePageState extends State<EditProfilePage> {
  final _api = ProfileApi();

  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;
  late final TextEditingController _userNameController;
  late final TextEditingController _phoneController;

  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();

    _firstNameController = TextEditingController(text: widget.profile.firstName);
    _lastNameController = TextEditingController(text: widget.profile.lastName);
    _userNameController = TextEditingController(text: widget.profile.userName);
    _phoneController =
        TextEditingController(text: widget.profile.phoneNumber ?? '');
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _userNameController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  bool get _canSave {
    if (_saving) return false;
    if (_firstNameController.text.trim().isEmpty) return false;
    if (_lastNameController.text.trim().isEmpty) return false;
    if (_validateUserName(_userNameController.text.trim()) != null) {
      return false;
    }
    if (_validatePhone(_phoneController.text) != null) {
      return false;
    }
    return true;
  }

  String? _validateUserName(String value) {
    final userName = value.trim();

    if (userName.isEmpty) {
      return 'Никнейм обязателен.';
    }

    if (userName.length < 3) {
      return 'Минимум 3 символа.';
    }

    if (userName.length > 20) {
      return 'Максимум 20 символов.';
    }

    final regex = RegExp(r'^[a-zA-Z0-9._]+$');
    if (!regex.hasMatch(userName)) {
      return 'Только латинские буквы, цифры, точка и _.';
    }

    return null;
  }

  String? _validatePhone(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return null;

    final cleaned = trimmed.replaceAll(RegExp(r'[^\d+]'), '');
    final regex = RegExp(r'^\+?\d{7,15}$');

    if (!regex.hasMatch(cleaned)) {
      return 'Введите корректный номер телефона.';
    }

    return null;
  }

  Future<void> _save() async {
    if (!_canSave) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final updated = await _api.updateProfile(
        firstName: _firstNameController.text.trim(),
        lastName: _lastNameController.text.trim(),
        userName: _userNameController.text.trim(),
        phoneNumber: _phoneController.text.trim(),
      );

      if (!mounted) return;
      Navigator.of(context).pop<UserProfile>(updated);
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _saving = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось сохранить профиль. Попробуйте ещё раз.',
        );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final userNameError = _userNameController.text.trim().isEmpty
        ? null
        : _validateUserName(_userNameController.text.trim());

    final phoneError = _phoneController.text.trim().isEmpty
        ? null
        : _validatePhone(_phoneController.text.trim());

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Редактировать профиль'),
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(24),
              border: Border.all(color: AppColors.border),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Основные данные',
                  style: TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 18,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Изменения сразу применятся к профилю, комментариям и отображению аккаунта.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 13,
                    height: 1.4,
                  ),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _firstNameController,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: const InputDecoration(
                    labelText: 'Имя',
                    hintText: 'Введите имя',
                  ),
                  onChanged: (_) => setState(() {}),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _lastNameController,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: const InputDecoration(
                    labelText: 'Фамилия',
                    hintText: 'Введите фамилию',
                  ),
                  onChanged: (_) => setState(() {}),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _userNameController,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: InputDecoration(
                    labelText: 'Никнейм',
                    hintText: 'никнейм',
                    errorText: userNameError,
                  ),
                  onChanged: (_) => setState(() {}),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _phoneController,
                  keyboardType: TextInputType.phone,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: InputDecoration(
                    labelText: 'Телефон',
                    hintText: '+79991234567',
                    errorText: phoneError,
                  ),
                  onChanged: (_) => setState(() {}),
                ),
              ],
            ),
          ),
          if (_error != null) ...[
            const SizedBox(height: 14),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.redAccent.withValues(alpha: 0.08),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: Colors.redAccent.withValues(alpha: 0.22),
                ),
              ),
              child: Text(
                _error!,
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 13,
                ),
              ),
            ),
          ],
          const SizedBox(height: 16),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: _canSave ? _save : null,
              child: _saving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Сохранить изменения'),
            ),
          ),
        ],
      ),
    );
  }
}