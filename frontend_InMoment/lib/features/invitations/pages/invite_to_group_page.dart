import 'dart:async';
import 'package:flutter/services.dart';
import 'package:share_plus/share_plus.dart';
import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/api/api_error.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../../core/widgets/group_dropdown_selector.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../../../core/config/app_links.dart';
import '../../contacts/api/search_api.dart';
import '../../contacts/models/user_search_item.dart';
import '../../groups/api/group_management_api.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../api/invitations_api.dart';

class InviteToGroupPage extends StatefulWidget {
  const InviteToGroupPage({super.key});

  @override
  State<InviteToGroupPage> createState() => _InviteToGroupPageState();
}

class _InviteToGroupPageState extends State<InviteToGroupPage> {
  final _api = InvitationsApi();
  final _groupManagementApi = GroupManagementApi();
  final _searchApi = SearchApi();
  final _controller = TextEditingController();
  final _groupController = ActiveGroupController.instance;

  bool _sending = false;
  bool _creatingInviteLink = false;
  bool _loadingGroups = true;
  bool _searchingUsers = false;

  String? _error;
  String? _groupsError;
  String? _selectedGroupId;

  List<UserSearchItem> _foundUsers = const [];
  Timer? _searchDebounce;
  int _searchRequestId = 0;
  UserSearchItem? _pendingInviteUser;

  @override
  void initState() {
    super.initState();
    _groupController.addListener(_onGroupsChanged);
    _controller.addListener(_onQueryChanged);
    _init();
  }

  @override
  void dispose() {
    _groupController.removeListener(_onGroupsChanged);
    _controller.removeListener(_onQueryChanged);
    _controller.dispose();
    _searchDebounce?.cancel();
    super.dispose();
  }

  Future<void> _init() async {
    if (_loadingGroups && _groupsError == null) {
      // Initial load is already in progress.
    }

    try {
      await _groupController.load(force: true);

      if (!mounted) return;

      _syncSelectedGroup();

      setState(() {
        _loadingGroups = false;
        _groupsError = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loadingGroups = false;
        _groupsError = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить группы для приглашения.',
        );
      });
    }
  }

  Future<void> _retryLoadGroups() async {
    if (_loadingGroups) return;

    setState(() {
      _loadingGroups = true;
      _groupsError = null;
      _error = null;
    });

    await _init();
  }

  void _onGroupsChanged() {
    if (!mounted) return;
    if (_groupsError != null) return;

    _syncSelectedGroup();
    setState(() {});
  }

  void _syncSelectedGroup() {
    final manageableGroups = _groupController.manageableGroups;
    final currentId = _selectedGroupId;

    if (manageableGroups.isEmpty) {
      _selectedGroupId = null;
      return;
    }

    final stillExists = manageableGroups.any((group) => group.id == currentId);
    if (stillExists) return;

    _selectedGroupId =
        _groupController.invitationGroup?.id ?? manageableGroups.first.id;
  }

  void _onQueryChanged() {
    final query = _controller.text.trim();
    final pendingUser = _pendingInviteUser;

    if (pendingUser != null && query == pendingUser.userName.trim()) {
      _searchDebounce?.cancel();
      if (!mounted) return;
      setState(() {
        _searchingUsers = false;
        _error = null;
      });
      return;
    }

    _searchDebounce?.cancel();
    final requestId = ++_searchRequestId;

    if (query.isEmpty) {
      if (!mounted) return;

      setState(() {
        _foundUsers = const [];
        _searchingUsers = false;
        _error = null;
        _pendingInviteUser = null;
      });
      return;
    }

    setState(() {
      _error = null;
      _searchingUsers = true;
      _pendingInviteUser = null;
    });

    _searchDebounce = Timer(const Duration(milliseconds: 350), () async {
      try {
        final result = await _searchApi.searchUsersFlexible(query);

        if (!mounted) return;
        if (requestId != _searchRequestId) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _foundUsers = result;
          _searchingUsers = false;
        });
      } catch (e) {
        if (!mounted) return;
        if (requestId != _searchRequestId) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _foundUsers = const [];
          _searchingUsers = false;
          _error = ApiError.normalize(
            e,
            fallback: 'Не удалось выполнить поиск пользователей.',
          );
        });
      }
    });
  }

  List<Group> get _manageableGroups => _groupController.manageableGroups;

  Group? get _selectedGroup {
    for (final group in _manageableGroups) {
      if (group.id == _selectedGroupId) {
        return group;
      }
    }
    return null;
  }

  bool get _canSend =>
      !_sending &&
      _controller.text.trim().isNotEmpty &&
      _selectedGroup != null;

  bool _looksLikeEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  Future<void> _sendInvite() async {
    final targetGroup = _selectedGroup;
    final value = _controller.text.trim();

    if (targetGroup == null) {
      setState(() {
        _error =
            'У вас нет группы, которой вы можете управлять, чтобы отправить приглашение.';
      });
      return;
    }

    if (value.isEmpty || _sending) return;

    _searchDebounce?.cancel();
    _searchRequestId++;

    setState(() {
      _sending = true;
      _error = null;
    });

    try {
      if (_looksLikeEmail(value)) {
        await _api.inviteByEmail(
          groupId: targetGroup.id,
          email: value,
        );
      } else {
        await _api.inviteByUserName(
          groupId: targetGroup.id,
          userName: value,
        );
      }

      if (!mounted) return;

      final selectedUser = _pendingInviteUser;
      final successText = selectedUser != null
          ? 'Приглашение отправлено в группу «${targetGroup.name}» пользователю @${selectedUser.userName}'
          : (_looksLikeEmail(value)
              ? 'Приглашение отправлено в группу «${targetGroup.name}» на $value'
              : 'Приглашение отправлено в группу «${targetGroup.name}» пользователю @$value');

      InMomentFeedback.showSuccess(context, successText);

      _controller.clear();

      setState(() {
        _foundUsers = const [];
        _pendingInviteUser = null;
        _searchingUsers = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _error = _normalizeInviteError(e);
      });

      InMomentFeedback.showError(context, _error!);
    } finally {
      if (mounted) {
        setState(() {
          _sending = false;
        });
      }
    }
  }

  Future<void> _createInviteLink() async {
    final targetGroup = _selectedGroup;
    if (targetGroup == null || _creatingInviteLink) return;

    setState(() {
      _creatingInviteLink = true;
      _error = null;
    });

    try {
      final code = await _groupManagementApi.createInviteCode(
        groupId: targetGroup.id,
        maxUses: 1,
        expireHours: 168,
      );

      final link = AppLinks.invitePublicLink(code);
      final deepLink = AppLinks.inviteDeepLink(code);

      if (!mounted) return;

      await _showInviteLinkSheet(
        groupName: targetGroup.name,
        link: link,
        code: code,
        deepLink: deepLink,
      );
    } catch (e) {
      if (!mounted) return;

      final message = ApiError.normalize(
        e,
        fallback: 'Не удалось создать ссылку-приглашение.',
      );

      setState(() {
        _error = message;
      });

      InMomentFeedback.showError(context, message);
    } finally {
      if (mounted) {
        setState(() {
          _creatingInviteLink = false;
        });
      }
    }
  }

  Future<void> _showInviteLinkSheet({
    required String groupName,
    required String link,
    required String code,
    required String deepLink,
  }) async {
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: Colors.transparent,
      isScrollControlled: false,
      builder: (sheetContext) {
        return SafeArea(
          top: false,
          child: Center(
            child: SizedBox(
              width: InMomentMediaFrame.resolveBottomSheetWidth(
                MediaQuery.sizeOf(sheetContext).width,
              ),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 0, 12, 12),
                child: Container(
                  padding: const EdgeInsets.fromLTRB(16, 12, 16, 16),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(28),
                    border: Border.all(
                      color: AppColors.border.withValues(alpha: 0.9),
                    ),
                  ),
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
                        'Ссылка-приглашение',
                        style: TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 18,
                          fontWeight: FontWeight.w900,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'Группа «$groupName». Ссылка одноразовая и действует 7 дней.',
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 13,
                          height: 1.4,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 14),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 13,
                          vertical: 12,
                        ),
                        decoration: BoxDecoration(
                          color: AppColors.background.withValues(alpha: 0.72),
                          borderRadius: BorderRadius.circular(18),
                          border: Border.all(
                            color: AppColors.border.withValues(alpha: 0.8),
                          ),
                        ),
                        child: SelectableText(
                          link,
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontSize: 13,
                            height: 1.35,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      const SizedBox(height: 14),
                      Row(
                        children: [
                          Expanded(
                            child: _InviteSheetButton(
                              icon: Icons.copy_rounded,
                              label: 'Скопировать',
                              onTap: () async {
                                await Clipboard.setData(
                                  ClipboardData(text: link),
                                );

                                if (sheetContext.mounted) {
                                  Navigator.of(sheetContext).pop();
                                }

                                if (mounted) {
                                  InMomentFeedback.showSuccess(
                                    context,
                                    'Ссылка скопирована.',
                                  );
                                }
                              },
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: _InviteSheetButton(
                              icon: Icons.ios_share_rounded,
                              label: 'Поделиться',
                              onTap: () async {
                                await Share.share(
                                  'Присоединяйся к моей группе в InMoment:\n$link\n\n'
                                  'Если ссылка не открылась автоматически, скопируй код приглашения: $code\n'
                                  'Резервная ссылка для приложения: $deepLink\n\n'
                                  'Ссылка одноразовая и действует 7 дней.',
                                  subject: 'Приглашение в InMoment',
                                );

                                if (sheetContext.mounted) {
                                  Navigator.of(sheetContext).pop();
                                }
                              },
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  String _normalizeInviteError(Object error) {
    final message = ApiError.normalize(
      error,
      fallback: 'Не удалось отправить приглашение.',
    );
    final lower = message.toLowerCase();

    if (lower.contains('validation')) {
      return 'Проверьте правильность username или email.';
    }

    if (lower.contains('user not found')) {
      return 'Пользователь с таким username или email не найден.';
    }

    if (lower.contains('pending invitation')) {
      return 'Приглашение уже отправлено этому пользователю.';
    }

    if (lower.contains('already a member')) {
      return 'Пользователь уже состоит в этой группе.';
    }

    if (lower.contains('only owner or admin') ||
        lower.contains('нет доступа')) {
      return 'Приглашать можно только в группе, которой вы управляете.';
    }

    if (lower.contains('email')) {
      return 'Некорректный email или пользователь не зарегистрирован.';
    }

    return message;
  }

  void _applySearchUser(UserSearchItem user) {
    final normalizedUserName = user.userName.trim();
    if (normalizedUserName.isEmpty) return;

    _controller.value = TextEditingValue(
      text: normalizedUserName,
      selection: TextSelection.collapsed(offset: normalizedUserName.length),
    );

    setState(() {
      _pendingInviteUser = null;
      _error = null;
    });
  }

  void _selectPendingInviteUser(UserSearchItem user) {
    final normalizedUserName = user.userName.trim();
    if (normalizedUserName.isEmpty) return;

    setState(() {
      _pendingInviteUser = user;
      _searchingUsers = false;
      _error = null;
    });

    _searchDebounce?.cancel();
    _controller.value = TextEditingValue(
      text: normalizedUserName,
      selection: TextSelection.collapsed(offset: normalizedUserName.length),
    );
  }

  @override
  Widget build(BuildContext context) {
    final manageableGroups = _manageableGroups;
    final selectedGroup = _selectedGroup;
    final query = _controller.text.trim();

    if (_loadingGroups) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    if (_groupsError != null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        body: Container(
          decoration: const BoxDecoration(
            gradient: AppColors.pageBackgroundGradient,
          ),
          child: SafeArea(
            child: InMomentResponsiveContent(
              child: ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(8, 10, 8, 24),
                children: [
                  _InvitePageHeader(
                    title: 'Пригласить в группу',
                    onBack: () => Navigator.of(context).maybePop(),
                  ),
                  const SizedBox(height: 18),
                  Center(
                    child: Container(
                      padding: const EdgeInsets.fromLTRB(18, 18, 18, 18),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius: BorderRadius.circular(24),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(
                            Icons.error_outline_rounded,
                            color: AppColors.textSecondary,
                            size: 38,
                          ),
                          const SizedBox(height: 12),
                          const Text(
                            'Не удалось загрузить группы',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 16,
                              fontWeight: FontWeight.w900,
                            ),
                          ),
                          const SizedBox(height: 8),
                          Text(
                            _groupsError!,
                            textAlign: TextAlign.center,
                            style: const TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 13,
                              height: 1.4,
                            ),
                          ),
                          const SizedBox(height: 14),
                          FilledButton(
                            onPressed: _loadingGroups ? null : _retryLoadGroups,
                            child: _loadingGroups
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  )
                                : const Text('Повторить'),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Container(
        decoration: const BoxDecoration(
          gradient: AppColors.pageBackgroundGradient,
        ),
        child: SafeArea(
          child: InMomentResponsiveContent(
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(8, 10, 8, 24),
              children: [
                _InvitePageHeader(
                  title: 'Пригласить в группу',
                  onBack: () => Navigator.of(context).maybePop(),
                ),
                const SizedBox(height: 16),
                if (manageableGroups.isEmpty)
                  Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(22),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: const Text(
                      'У вас нет группы, которой вы можете управлять, чтобы пригласить кого-то. Нужна роль владельца или админа.',
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 14,
                        height: 1.45,
                      ),
                    ),
                  )
                else ...[
                  _InviteGroupSelector(
                    groups: manageableGroups,
                    selectedGroupId: selectedGroup?.id,
                    disabled: _sending || _creatingInviteLink,
                    onChanged: (value) {
                      if (value == null) return;
                      setState(() {
                        _selectedGroupId = value;
                      });
                    },
                  ),
                  const SizedBox(height: 14),
                  _ExternalInviteLinkPanel(
                    busy: _creatingInviteLink,
                    onTap: _createInviteLink,
                  ),
                  const SizedBox(height: 18),
                  const _InviteSectionTitle(title: 'Поиск пользователя'),
                  const SizedBox(height: 10),
                  TextField(
                    controller: _controller,
                    enabled: !_sending && !_creatingInviteLink,
                    style: const TextStyle(color: AppColors.textPrimary),
                    decoration: InputDecoration(
                      hintText: 'username, email или телефон',
                      prefixIcon: const Icon(Icons.person_add_alt_1_rounded),
                      suffixIcon: _searchingUsers
                          ? const Padding(
                              padding: EdgeInsets.all(14),
                              child: SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              ),
                            )
                          : (query.isNotEmpty
                              ? IconButton(
                                  onPressed: _sending || _creatingInviteLink
                                      ? null
                                      : () {
                                          _controller.clear();
                                          setState(() {
                                            _foundUsers = const [];
                                            _error = null;
                                            _pendingInviteUser = null;
                                          });
                                        },
                                  icon: const Icon(Icons.close_rounded),
                                )
                              : null),
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
                  ),
                  if (_foundUsers.isNotEmpty) ...[
                    const SizedBox(height: 14),
                    ..._foundUsers.map(
                      (user) => Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: _UserSearchTile(
                          user: user,
                          selected: _pendingInviteUser?.id == user.id,
                          disabled: _sending || _creatingInviteLink,
                          onTap: () => _applySearchUser(user),
                          onInvite: () => _selectPendingInviteUser(user),
                        ),
                      ),
                    ),
                  ],
                  if (!_searchingUsers &&
                      query.isNotEmpty &&
                      _foundUsers.isEmpty &&
                      !_looksLikeEmail(query)) ...[
                    const SizedBox(height: 14),
                    const Text(
                      'Совпадений в поиске не найдено. Можно всё равно попробовать отправить приглашение вручную.',
                      style: TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                        height: 1.4,
                      ),
                    ),
                  ],
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      _error!,
                      style: const TextStyle(
                        color: Colors.redAccent,
                        fontSize: 13,
                      ),
                    ),
                  ],
                  if (_pendingInviteUser != null) ...[
                    const SizedBox(height: 14),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton.icon(
                        onPressed: _sending ? null : _sendInvite,
                        icon: _sending
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.send_rounded),
                        label: Text(
                          _sending ? 'Отправка...' : 'Отправить приглашение',
                        ),
                      ),
                    ),
                  ] else if (_looksLikeEmail(query)) ...[
                    const SizedBox(height: 14),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton.icon(
                        onPressed: _canSend ? _sendInvite : null,
                        icon: _sending
                            ? const SizedBox(
                                width: 16,
                                height: 16,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Icon(Icons.send_rounded),
                        label: Text(
                          _sending ? 'Отправка...' : 'Отправить приглашение',
                        ),
                      ),
                    ),
                  ],
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}


class _InvitePageHeader extends StatelessWidget {
  final String title;
  final VoidCallback onBack;

  const _InvitePageHeader({
    required this.title,
    required this.onBack,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _InviteHeaderIconButton(
          icon: Icons.arrow_back_ios_new_rounded,
          onTap: onBack,
        ),
        Expanded(
          child: Text(
            title,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 20,
              fontWeight: FontWeight.w900,
              height: 1.1,
            ),
          ),
        ),
        const SizedBox(width: 44),
      ],
    );
  }
}

class _InviteHeaderIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _InviteHeaderIconButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surfaceGlass(0.14),
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(
            icon,
            color: AppColors.textPrimary,
            size: 20,
          ),
        ),
      ),
    );
  }
}

class _ExternalInviteLinkPanel extends StatelessWidget {
  final bool busy;
  final VoidCallback onTap;

  const _ExternalInviteLinkPanel({
    required this.busy,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 13),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Material(
            color: Colors.transparent,
            child: InkWell(
              onTap: busy ? null : onTap,
              borderRadius: BorderRadius.circular(18),
              child: Ink(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color: AppColors.surfaceLight,
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(
                    color: AppColors.border.withValues(alpha: 0.86),
                  ),
                ),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 20,
                      backgroundColor:
                          AppColors.accent.withValues(alpha: 0.20),
                      child: busy
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(
                              Icons.link_rounded,
                              color: AppColors.textPrimary,
                              size: 19,
                            ),
                    ),
                    const SizedBox(width: 12),
                    const Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Ссылка-приглашение',
                            style: TextStyle(
                              color: AppColors.textPrimary,
                              fontSize: 14,
                              fontWeight: FontWeight.w900,
                            ),
                          ),
                          SizedBox(height: 2),
                          Text(
                            'Скопировать или отправить ссылку во внешний мессенджер',
                            style: TextStyle(
                              color: AppColors.textSecondary,
                              fontSize: 12,
                              height: 1.25,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    const Icon(
                      Icons.ios_share_rounded,
                      color: AppColors.textSecondary,
                      size: 20,
                    ),
                  ],
                ),
              ),
            ),
          ),
          const SizedBox(height: 10),
          const Text(
            'Ссылка действует 7 дней и срабатывает только один раз.',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
              height: 1.35,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _InviteSheetButton extends StatefulWidget {
  final IconData icon;
  final String label;
  final FutureOr<void> Function() onTap;

  const _InviteSheetButton({
    required this.icon,
    required this.label,
    required this.onTap,
  });

  @override
  State<_InviteSheetButton> createState() => _InviteSheetButtonState();
}

class _InviteSheetButtonState extends State<_InviteSheetButton> {
  bool _busy = false;

  Future<void> _handleTap() async {
    if (_busy) return;

    setState(() {
      _busy = true;
    });

    try {
      await widget.onTap();
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: _busy ? null : _handleTap,
        borderRadius: BorderRadius.circular(18),
        child: Ink(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 13),
          decoration: BoxDecoration(
            color: AppColors.accent.withValues(alpha: 0.18),
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: AppColors.accent.withValues(alpha: 0.28),
            ),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              if (_busy)
                const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              else
                Icon(
                  widget.icon,
                  color: AppColors.textPrimary,
                  size: 18,
                ),
              const SizedBox(width: 8),
              Flexible(
                child: Text(
                  widget.label,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 13,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _UserSearchTile extends StatelessWidget {
  final UserSearchItem user;
  final bool selected;
  final bool disabled;
  final VoidCallback onTap;
  final VoidCallback onInvite;

  const _UserSearchTile({
    required this.user,
    required this.selected,
    required this.disabled,
    required this.onTap,
    required this.onInvite,
  });

  @override
  Widget build(BuildContext context) {
    final title = user.title;

    return Material(
      color: AppColors.surfaceLight,
      borderRadius: BorderRadius.circular(18),
      child: InkWell(
        onTap: disabled ? null : onTap,
        borderRadius: BorderRadius.circular(18),
        child: Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: selected ? AppColors.accentSecondary : AppColors.border,
            ),
          ),
          child: Row(
            children: [
              CircleAvatar(
                radius: 20,
                backgroundColor: AppColors.accent.withValues(alpha: 0.22),
                backgroundImage: user.profilePhotoUrl != null &&
                        user.profilePhotoUrl!.trim().isNotEmpty
                    ? NetworkImage(user.profilePhotoUrl!)
                    : null,
                child: user.profilePhotoUrl == null ||
                        user.profilePhotoUrl!.trim().isEmpty
                    ? Text(
                        user.userName.isNotEmpty
                            ? user.userName[0].toUpperCase()
                            : 'U',
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontWeight: FontWeight.w700,
                        ),
                      )
                    : null,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 14,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '@${user.userName}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 10),
              IconButton(
                onPressed: disabled ? null : onInvite,
                icon: Icon(
                  selected
                      ? Icons.check_circle_rounded
                      : Icons.person_add_alt_1_rounded,
                  color: selected
                      ? AppColors.accentSecondary
                      : AppColors.textSecondary,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _InviteSectionTitle extends StatelessWidget {
  final String title;

  const _InviteSectionTitle({
    required this.title,
  });

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: const TextStyle(
        color: AppColors.textPrimary,
        fontSize: 16,
        fontWeight: FontWeight.w800,
      ),
    );
  }
}

class _InviteGroupSelector extends StatelessWidget {
  final List<Group> groups;
  final String? selectedGroupId;
  final bool disabled;
  final ValueChanged<String?> onChanged;

  const _InviteGroupSelector({
    required this.groups,
    required this.selectedGroupId,
    required this.disabled,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return GroupDropdownSelector(
      groups: groups,
      selectedGroupId: selectedGroupId,
      hintText: 'Выберите группу',
      enabled: !disabled && groups.isNotEmpty,
      isLoading: false,
      height: 42,
      borderRadius: 18,
      avatarRadius: 12,
      fontSize: 14,
      onChanged: disabled || groups.isEmpty ? null : onChanged,
    );
  }
}