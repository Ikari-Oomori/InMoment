import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../api/groups_api.dart';
import '../controllers/active_group_controller.dart';
import '../models/group.dart';
import 'group_management_page.dart';

class CreateGroupPage extends StatefulWidget {
  const CreateGroupPage({super.key});

  @override
  State<CreateGroupPage> createState() => _CreateGroupPageState();
}

class _CreateGroupPageState extends State<CreateGroupPage> {
  final GroupsApi _groupsApi = GroupsApi();
  final ActiveGroupController _groupController = ActiveGroupController.instance;
  final TextEditingController _nameController = TextEditingController();

  bool _creating = false;
  String? _error;

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final name = _nameController.text.trim();

    if (_creating) return;

    if (name.isEmpty) {
      setState(() {
        _error = 'Введите название группы.';
      });
      return;
    }

    setState(() {
      _creating = true;
      _error = null;
    });

    try {
      final createdGroupId = await _groupsApi.createGroup(name);

      await _groupController.load(force: true);

      Group? createdGroup;
      for (final group in _groupController.groups) {
        if (group.id == createdGroupId) {
          createdGroup = group;
          break;
        }
      }

      if (createdGroup != null) {
        await _groupController.setActiveGroup(createdGroup);
      }

      if (!mounted) return;

      final targetGroup = createdGroup ??
          Group(
            id: createdGroupId,
            name: name,
            isOwner: true,
            isAdmin: true,
            isActiveGroup: true,
          );

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Группа «${targetGroup.name}» создана'),
        ),
      );

      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => GroupManagementPage(group: targetGroup),
        ),
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = ApiError.normalize(
        e,
        fallback: 'Не удалось создать группу. Попробуйте ещё раз.',
      );
        _creating = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final canSubmit = !_creating && _nameController.text.trim().isNotEmpty;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: InMomentResponsiveContent(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(0, 16, 0, 24),
            children: [
              _CreateGroupPageHeader(
                title: 'Создать группу',
                onBack: () => Navigator.of(context).maybePop(),
              ),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.all(18),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(26),
                  border: Border.all(color: AppColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'Новая группа',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 20,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'После создания вы станете владельцем группы. Название, аватар, участников и остальные настройки можно будет сразу изменить на следующем экране.',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 14,
                        height: 1.45,
                      ),
                    ),
                    const SizedBox(height: 18),
                    TextField(
                      controller: _nameController,
                      enabled: !_creating,
                      maxLength: 60,
                      style: const TextStyle(color: AppColors.textPrimary),
                      decoration: const InputDecoration(
                        labelText: 'Название группы',
                        hintText: 'Например: Семья, Близкие, Поездка',
                        counterText: '',
                      ),
                      onChanged: (_) {
                        if (_error != null) {
                          setState(() {
                            _error = null;
                          });
                        } else {
                          setState(() {});
                        }
                      },
                      textInputAction: TextInputAction.done,
                      onSubmitted: (_) {
                        if (canSubmit) {
                          _submit();
                        }
                      },
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 10),
                      Text(
                        _error!,
                        style: const TextStyle(
                          color: Colors.redAccent,
                          fontSize: 13,
                        ),
                      ),
                    ],
                    const SizedBox(height: 16),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton.icon(
                        onPressed: canSubmit ? _submit : null,
                        icon: _creating
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.group_add_rounded),
                        label: Text(
                          _creating ? 'Создаём...' : 'Создать группу',
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
class _CreateGroupPageHeader extends StatelessWidget {
  final String title;
  final VoidCallback onBack;

  const _CreateGroupPageHeader({
    required this.title,
    required this.onBack,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 44,
      child: Row(
        children: [
          Material(
            color: AppColors.surfaceGlass(0.28),
            shape: const CircleBorder(),
            child: InkWell(
              onTap: onBack,
              customBorder: const CircleBorder(),
              child: const SizedBox(
                width: 44,
                height: 44,
                child: Icon(
                  Icons.arrow_back_ios_new_rounded,
                  size: 19,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              title,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 20,
                fontWeight: FontWeight.w900,
              ),
            ),
          ),
          const SizedBox(width: 52),
        ],
      ),
    );
  }
}