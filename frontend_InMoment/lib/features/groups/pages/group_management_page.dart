import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../../profile/api/profile_api.dart';
import '../../profile/models/user_profile.dart';
import '../api/group_management_api.dart';
import '../controllers/active_group_controller.dart';
import '../models/group.dart';
import '../models/group_member.dart';
import '../models/group_settings.dart';
import '../../invitations/pages/invite_to_group_page.dart';
import '../../profile/pages/public_user_profile_page.dart';

class GroupManagementPage extends StatefulWidget {
  final Group group;

  const GroupManagementPage({
    super.key,
    required this.group,
  });

  @override
  State<GroupManagementPage> createState() => _GroupManagementPageState();
}

class _GroupManagementPageState extends State<GroupManagementPage> {
  final GroupManagementApi _api = GroupManagementApi();
  final ProfileApi _profileApi = ProfileApi();
  final ActiveGroupController _activeGroupController =
      ActiveGroupController.instance;

  bool _loading = true;
  bool _busy = false;
  bool _membersExpanded = false;
  String? _error;

  GroupSettings? _settings;
  UserProfile? _me;
  List<GroupMember> _members = const [];

  bool get _isOwner {
    final ownerId = _settings?.ownerId;
    final me = _me;
    if (ownerId != null && me != null && ownerId == me.id) {
      return true;
    }
    return widget.group.isOwner;
  }

  bool get _isAdmin {
    final meId = _me?.id;
    if (meId == null) return false;

    for (final member in _members) {
      if (member.userId == meId) {
        return member.isOwner || member.isAdmin;
      }
    }
    return widget.group.isAdmin;
  }

  bool get _canManageSettings => _isOwner || _isAdmin;

  bool get _isActiveGroup {
    return _activeGroupController.activeGroup?.id == widget.group.id;
  }

  List<GroupMember> get _visibleMembers {
    if (_membersExpanded || _members.length <= 5) return _members;
    return _members.take(5).toList(growable: false);
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load({bool silent = false}) async {
    if (_loading && silent) return;
    if (_busy) return;

    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final results = await Future.wait<Object>([
        _profileApi.getMe(),
        _api.getSettings(widget.group.id),
        _api.getMembers(widget.group.id),
      ]);

      if (!mounted) return;

      setState(() {
        _me = results[0] as UserProfile;
        _settings = results[1] as GroupSettings;
        _members = results[2] as List<GroupMember>;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _error = _normalizeError(e);
      });
    }
  }

  Future<void> _openMemberProfile(GroupMember member) async {
    if (member.userId.trim().isEmpty) return;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => PublicUserProfilePage(
          userId: member.userId,
        ),
      ),
    );
  }

  Future<void> _setActiveGroup() async {
    if (_busy || _isActiveGroup) return;

    final settings = _settings;
    if (settings == null) return;

    setState(() {
      _busy = true;
    });

    try {
      await _activeGroupController.setActiveGroup(
        widget.group.copyWith(
          name: settings.name,
          avatarUrl: settings.avatarUrl,
          isOwner: _isOwner,
          isAdmin: _isAdmin,
          membersCount: _members.length,
        ),
      );

      if (!mounted) return;
      _showMessage('Активная группа: ${settings.name}');
      setState(() {});
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _editGroupInfo() async {
    final settings = _settings;
    if (settings == null || !_canManageSettings || _busy) return;

    final nameController = TextEditingController(text: settings.name);
    final descriptionController =
        TextEditingController(text: settings.description ?? '');
    var dialogClosing = false;

    final payload = await showDialog<_EditGroupPayload>(
      context: context,
      barrierColor: Colors.black.withValues(alpha: 0.54),
      builder: (dialogContext) {
        return PopScope(
          canPop: true,
          child: InMomentGlassDialog(
            title: 'Редактирование группы',
            confirmText: 'Сохранить',
            onCancel: () {
              if (dialogClosing) return;
              dialogClosing = true;
              FocusManager.instance.primaryFocus?.unfocus();
              Future<void>.delayed(const Duration(milliseconds: 90), () {
                if (dialogContext.mounted) {
                  Navigator.of(dialogContext).maybePop();
                }
              });
            },
            onConfirm: () {
              if (dialogClosing) return;
              dialogClosing = true;
              FocusManager.instance.primaryFocus?.unfocus();
              Future<void>.delayed(const Duration(milliseconds: 90), () {
                if (dialogContext.mounted) {
                  Navigator.of(dialogContext).pop(
                    _EditGroupPayload(
                      name: nameController.text,
                      description: descriptionController.text,
                    ),
                  );
                }
              });
            },
            content: SizedBox(
              width: double.maxFinite,
              child: SingleChildScrollView(
                keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextField(
                      controller: nameController,
                      maxLength: 60,
                      scrollPadding: const EdgeInsets.only(bottom: 160),
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                      decoration: const InputDecoration(
                        labelText: 'Название группы',
                        counterText: '',
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      controller: descriptionController,
                      maxLength: 180,
                      minLines: 3,
                      maxLines: 3,
                      scrollPadding: const EdgeInsets.only(bottom: 220),
                      keyboardType: TextInputType.multiline,
                      textInputAction: TextInputAction.newline,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                      decoration: const InputDecoration(
                        labelText: 'Описание',
                        hintText: 'Необязательно',
                        counterText: '',
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );

    Future<void>.delayed(const Duration(milliseconds: 300), () {
      nameController.dispose();
      descriptionController.dispose();
    });

    if (payload == null) return;

    final nextName = payload.name.trim();
    if (nextName.isEmpty) {
      _showMessage('Название группы не может быть пустым.');
      return;
    }

    setState(() {
      _busy = true;
    });

    try {
      final updated = await _api.updateSettings(
        groupId: widget.group.id,
        name: nextName,
        description: payload.description,
      );

      await _activeGroupController.load(force: true);

      if (!mounted) return;

      setState(() {
        _settings = updated;
      });

      _showMessage('Настройки группы сохранены.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _changeAvatar() async {
    if (!_canManageSettings || _busy) return;

    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: const ['jpg', 'jpeg', 'png', 'webp', 'heic', 'heif'],
      allowMultiple: false,
      withData: true,
    );

    if (result == null || result.files.isEmpty) return;

    setState(() {
      _busy = true;
    });

    try {
      final file = result.files.single;
      final avatarUrl = await _api.uploadAvatar(
        groupId: widget.group.id,
        file: file,
      );

      await _activeGroupController.load(force: true);

      if (!mounted) return;

      setState(() {
        _settings = (_settings ??
                GroupSettings(
                  id: widget.group.id,
                  name: widget.group.name,
                  ownerId: widget.group.ownerId ?? '',
                ))
            .copyWith(avatarUrl: avatarUrl);
      });

      _showMessage('Аватар группы обновлён.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

    Future<void> _openMemberActionsSheet({
    required GroupMember member,
    required bool canToggleAdmin,
    required bool canTransferOwnership,
    required bool canRemove,
  }) async {
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: false,
      builder: (context) {
        return SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
            child: Container(
              decoration: BoxDecoration(
                color: AppColors.surfaceGlassStrong(0.58),
                borderRadius: BorderRadius.circular(28),
                border: Border.all(
                  color: AppColors.border.withValues(alpha: 0.9),
                ),
              ),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 42,
                      height: 4,
                      decoration: BoxDecoration(
                        color: AppColors.textSecondary.withValues(alpha: 0.35),
                        borderRadius: BorderRadius.circular(999),
                      ),
                    ),
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        _UserAvatar(
                          imageUrl: member.profilePhotoUrl,
                          fallbackText: member.fullName,
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                member.fullName,
                                style: const TextStyle(
                                  color: AppColors.textPrimary,
                                  fontSize: 15,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                '@${member.userName}',
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 12.5,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    if (canToggleAdmin)
                      _SheetActionTile(
                        title: member.isAdmin
                            ? 'Снять админа'
                            : 'Сделать админом',
                        onTap: () async {
                          Navigator.of(context).pop();
                          await _toggleAdmin(member);
                        },
                      ),
                    if (canToggleAdmin) const SizedBox(height: 8),
                    if (canTransferOwnership)
                      _SheetActionTile(
                        title: 'Передать владение',
                        onTap: () async {
                          Navigator.of(context).pop();
                          await _transferOwnership(member);
                        },
                      ),
                    if (canTransferOwnership) const SizedBox(height: 8),
                    if (canRemove)
                      _SheetActionTile(
                        title: 'Удалить',
                        danger: true,
                        onTap: () async {
                          Navigator.of(context).pop();
                          await _removeMember(member);
                        },
                      ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  Future<void> _toggleAdmin(GroupMember member) async {
    if (!_isOwner || _busy) return;
    if (member.isOwner) return;

    setState(() {
      _busy = true;
    });

    try {
      if (member.isAdmin) {
        await _api.removeAdmin(
          groupId: widget.group.id,
          userId: member.userId,
        );
        _showMessage('Права администратора сняты.');
      } else {
        await _api.makeAdmin(
          groupId: widget.group.id,
          userId: member.userId,
        );
        _showMessage('Участник назначен админом.');
      }

      await _load(silent: true);
      await _activeGroupController.load(force: true);
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _transferOwnership(GroupMember member) async {
    if (!_isOwner || _busy) return;
    if (member.isOwner) return;

    final confirmed = await showInMomentConfirmDialog(
      context: context,
      title: 'Передать владение',
      message: 'Передать владение группой пользователю ${member.fullName}? После передачи вы станете админом.',
      confirmText: 'Передать',
    );

    if (confirmed != true) return;

    setState(() {
      _busy = true;
    });

    try {
      await _api.transferOwnership(
        groupId: widget.group.id,
        newOwnerUserId: member.userId,
      );

      await _load(silent: true);
      await _activeGroupController.load(force: true);

      if (!mounted) return;
      _showMessage('Владение группой передано.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _removeMember(GroupMember member) async {
    if ((!_isOwner && !_isAdmin) || _busy) return;
    if (member.isOwner) return;

    final confirmed = await showInMomentConfirmDialog(
      context: context,
      title: 'Удалить участника',
      message: 'Удалить ${member.fullName} из группы?',
      confirmText: 'Удалить',
      danger: true,
    );

    if (confirmed != true) return;

    setState(() {
      _busy = true;
    });

    try {
      await _api.removeMember(
        groupId: widget.group.id,
        userId: member.userId,
      );

      await _activeGroupController.load(force: true);
      await _load(silent: true);

      if (!mounted) return;
      _showMessage('Участник удалён из группы.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  Future<void> _leaveGroup() async {
    if (_busy || _isOwner) return;

    final confirmed = await showInMomentConfirmDialog(
      context: context,
      title: 'Выйти из группы',
      message: 'Вы уверены, что хотите выйти из этой группы? После выхода доступ к её контенту будет потерян.',
      confirmText: 'Выйти',
      danger: true,
    );

    if (confirmed != true) return;

    setState(() {
      _busy = true;
    });

    try {
      await _api.leaveGroup(widget.group.id);
      await _activeGroupController.load(force: true);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Вы покинули группу')),
      );

      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
      setState(() {
        _busy = false;
      });
    }
  }

  Future<void> _openInvitePage() async {
    if (_busy || !_canManageSettings) return;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const InviteToGroupPage(),
      ),
    );

    if (!mounted) return;

    await _activeGroupController.load(force: true);
    await _load(silent: true);
  }

  Future<void> _deleteGroup() async {
    if (!_isOwner || _busy) return;

    final confirmed = await showInMomentConfirmDialog(
      context: context,
      title: 'Удалить группу',
      message: 'Группа будет деактивирована, участники потеряют к ней доступ, а активные приглашения будут отменены. Это действие необратимо.',
      confirmText: 'Удалить',
      danger: true,
    );

    if (confirmed != true) return;

    setState(() {
      _busy = true;
    });

    try {
      await _api.deleteGroup(widget.group.id);
      await _activeGroupController.load(force: true);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Группа удалена')),
      );

      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;
      _showMessage(_normalizeError(e));
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  void _showMessage(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  String _normalizeError(Object error) {
    return ApiError.normalize(
      error,
      fallback: 'Не удалось выполнить действие с группой. Попробуйте ещё раз.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final settings = _settings;

    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (_error != null && settings == null) {
      return InMomentPageShell(
        title: 'Группа',
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              _error!,
              textAlign: TextAlign.center,
              style: const TextStyle(
                color: AppColors.textPrimary,
                height: 1.42,
              ),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _busy || _loading ? null : () => _load(),
              child: _busy || _loading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Повторить'),
            ),
          ],
        ),
      );
    }

    final groupName = settings?.name ?? widget.group.name;
    final groupDescription = settings?.description?.trim();
    final avatarUrl = settings?.avatarUrl ?? widget.group.avatarUrl;
    final meId = _me?.id;

    return InMomentPageShell(
      title: groupName,
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      actions: [
        IconButton(
          tooltip: 'Обновить',
          onPressed: _busy ? null : () => _load(),
          icon: const Icon(Icons.refresh_rounded),
          color: AppColors.textPrimary,
        ),
      ],
      child: ListView(
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
        children: [
          _GroupHeader(
            groupName: groupName,
            groupDescription: groupDescription,
            avatarUrl: avatarUrl,
            isOwner: _isOwner,
            isAdmin: _isAdmin,
            isActiveGroup: _isActiveGroup,
            membersCount: _members.length,
          ),
          const SizedBox(height: 18),

          if (_canManageSettings) ...[
            const _SectionTitle('Управление'),
            const SizedBox(height: 10),
            InMomentActionTile(
              icon: Icons.edit_outlined,
              title: 'Название и описание',
              onTap: _busy ? null : _editGroupInfo,
              compact: true,
            ),
            const SizedBox(height: 10),
            InMomentActionTile(
              icon: Icons.image_outlined,
              title: 'Аватар',
              onTap: _busy ? null : _changeAvatar,
              compact: true,
            ),
            const SizedBox(height: 10),
            InMomentActionTile(
              icon: Icons.person_add_alt_1_rounded,
              title: 'Пригласить',
              onTap: _busy ? null : _openInvitePage,
              compact: true,
            ),
            const SizedBox(height: 10),
            InMomentActionTile(
              icon: Icons.check_circle_outline_rounded,
              title: _isActiveGroup ? 'Уже активна' : 'Сделать активной',
              onTap: _busy || _isActiveGroup ? null : _setActiveGroup,
              compact: true,
            ),
            const SizedBox(height: 18),
          ],

          const _SectionTitle('Участники'),
          const SizedBox(height: 10),
          if (_members.isEmpty)
            const _PlainTextCard(
              text: 'В этой группе пока нет отображаемых участников.',
            )
          else ...[
            ..._visibleMembers.map((member) {
              final isMe = meId != null && member.userId == meId;
              final canRemove = (_isOwner || _isAdmin) &&
                  !member.isOwner &&
                  !isMe &&
                  !(_isAdmin && member.isAdmin);
              final canToggleAdmin = _isOwner && !member.isOwner && !isMe;
              final canTransferOwnership = _isOwner && !member.isOwner && !isMe;

              return Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _MemberCard(
                  member: member,
                  isMe: isMe,
                  onOpenProfile: () => _openMemberProfile(member),
                  onOpenActions:
                      canToggleAdmin || canTransferOwnership || canRemove
                          ? _busy
                              ? null
                              : () => _openMemberActionsSheet(
                                    member: member,
                                    canToggleAdmin: canToggleAdmin,
                                    canTransferOwnership: canTransferOwnership,
                                    canRemove: canRemove,
                                  )
                          : null,
                ),
              );
            }),
            if (_members.length > 5)
              Center(
                child: TextButton(
                  onPressed: () {
                    setState(() {
                      _membersExpanded = !_membersExpanded;
                    });
                  },
                  child: Text(_membersExpanded ? 'Скрыть' : 'Показать всех'),
                ),
              ),
          ],

          const SizedBox(height: 8),
          const _SectionTitle('Действия'),
          const SizedBox(height: 10),
          if (!_isOwner)
            InMomentActionTile(
              icon: Icons.logout_rounded,
              title: 'Выйти из группы',
              onTap: _busy ? null : _leaveGroup,
              danger: true,
              compact: true,
            )
          else
            InMomentActionTile(
              icon: Icons.delete_outline_rounded,
              title: 'Удалить группу',
              onTap: _busy ? null : _deleteGroup,
              danger: true,
              compact: true,
            ),
        ],
      ),
    );
  }
}

class _GroupHeader extends StatelessWidget {
  final String groupName;
  final String? groupDescription;
  final String? avatarUrl;
  final bool isOwner;
  final bool isAdmin;
  final bool isActiveGroup;
  final int membersCount;

  const _GroupHeader({
    required this.groupName,
    required this.groupDescription,
    required this.avatarUrl,
    required this.isOwner,
    required this.isAdmin,
    required this.isActiveGroup,
    required this.membersCount,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(24),
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      showGlow: false,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _GroupAvatar(
            avatarUrl: avatarUrl,
            name: groupName,
            radius: 28,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  groupName,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 16,
                    fontWeight: FontWeight.w700,
                    height: 1.15,
                  ),
                ),
                if (groupDescription != null &&
                    groupDescription!.isNotEmpty) ...[
                  const SizedBox(height: 6),
                  Text(
                    groupDescription!,
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                      height: 1.3,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ],
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _Badge(
                      text: isOwner
                          ? 'Владелец'
                          : isAdmin
                              ? 'Админ'
                              : 'Участник',
                      highlighted: isOwner || isAdmin,
                    ),
                    _Badge(
                      text: isActiveGroup ? 'Активная' : 'Не активная',
                      highlighted: isActiveGroup,
                    ),
                    _Badge(text: '$membersCount участников'),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String text;

  const _SectionTitle(this.text);

  @override
  Widget build(BuildContext context) {
    return Text(
      text,
      style: const TextStyle(
        color: AppColors.textPrimary,
        fontSize: 15,
        fontWeight: FontWeight.w700,
        height: 1.15,
      ),
    );
  }
}

class _PlainTextCard extends StatelessWidget {
  final String text;

  const _PlainTextCard({
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(20),
      padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
      showGlow: false,
      child: Text(
        text,
        style: const TextStyle(
          color: AppColors.textSecondary,
          fontSize: 12.5,
          height: 1.35,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _MemberCard extends StatelessWidget {
  final GroupMember member;
  final bool isMe;
  final VoidCallback onOpenProfile;
  final VoidCallback? onOpenActions;

  const _MemberCard({
    required this.member,
    required this.isMe,
    required this.onOpenProfile,
    required this.onOpenActions,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(20),
      onTap: onOpenProfile,
      padding: const EdgeInsets.fromLTRB(12, 12, 10, 12),
      showGlow: false,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _UserAvatar(
            imageUrl: member.profilePhotoUrl,
            fallbackText: member.fullName,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  member.fullName,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 13.5,
                    fontWeight: FontWeight.w700,
                    height: 1.15,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '@${member.userName}',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 11.5,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _Badge(text: member.roleLabel),
                    if (isMe)
                      const _Badge(
                        text: 'Вы',
                        highlighted: true,
                      ),
                  ],
                ),
              ],
            ),
          ),
          if (onOpenActions != null) ...[
            const SizedBox(width: 8),
            _MemberActionsButton(onTap: onOpenActions),
          ],
        ],
      ),
    );
  }
}

class _EditGroupPayload {
  final String name;
  final String description;

  const _EditGroupPayload({
    required this.name,
    required this.description,
  });
}

class _GroupAvatar extends StatelessWidget {
  final String? avatarUrl;
  final String name;
  final double radius;

  const _GroupAvatar({
    required this.avatarUrl,
    required this.name,
    this.radius = 26,
  });

  @override
  Widget build(BuildContext context) {
    return CircleAvatar(
      radius: radius,
      backgroundColor: AppColors.accent.withValues(alpha: 0.24),
      backgroundImage: (avatarUrl != null && avatarUrl!.trim().isNotEmpty)
          ? NetworkImage(avatarUrl!)
          : null,
      child: (avatarUrl == null || avatarUrl!.trim().isEmpty)
          ? Text(
              name.isNotEmpty ? name[0].toUpperCase() : 'G',
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            )
          : null,
    );
  }
}

class _UserAvatar extends StatelessWidget {
  final String? imageUrl;
  final String fallbackText;

  const _UserAvatar({
    required this.imageUrl,
    required this.fallbackText,
  });

  @override
  Widget build(BuildContext context) {
    return CircleAvatar(
      radius: 24,
      backgroundColor: AppColors.accent.withValues(alpha: 0.24),
      backgroundImage: imageUrl != null && imageUrl!.trim().isNotEmpty
          ? NetworkImage(imageUrl!)
          : null,
      child: imageUrl == null || imageUrl!.trim().isEmpty
          ? Text(
              fallbackText.isNotEmpty ? fallbackText[0].toUpperCase() : 'U',
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            )
          : null,
    );
  }
}

class _Badge extends StatelessWidget {
  final String text;
  final bool highlighted;

  const _Badge({
    required this.text,
    this.highlighted = false,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: highlighted
          ? InMomentSurfaceTone.elevated
          : InMomentSurfaceTone.overlay,
      borderRadius: BorderRadius.circular(999),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      child: Text(
        text,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontSize: 11.5,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _MemberActionsButton extends StatelessWidget {
  final VoidCallback? onTap;

  const _MemberActionsButton({
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: Ink(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: AppColors.surfaceGlass(0.62),
            shape: BoxShape.circle,
            border: Border.all(
              color: AppColors.softStroke(0.10),
            ),
          ),
          child: const Icon(
            Icons.more_horiz_rounded,
            color: AppColors.textPrimary,
            size: 20,
          ),
        ),
      ),
    );
  }
}

class _SheetActionTile extends StatelessWidget {
  final String title;
  final VoidCallback onTap;
  final bool danger;

  const _SheetActionTile({
    required this.title,
    required this.onTap,
    this.danger = false,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: Ink(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
          decoration: BoxDecoration(
            color: danger
                ? Colors.redAccent.withValues(alpha: 0.12)
                : AppColors.card,
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: danger
                  ? Colors.redAccent.withValues(alpha: 0.20)
                  : AppColors.border.withValues(alpha: 0.86),
            ),
          ),
          child: Text(
            title,
            style: TextStyle(
              color: danger ? Colors.redAccent : AppColors.textPrimary,
              fontSize: 14,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ),
    );
  }
}