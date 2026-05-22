import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/api/api_error.dart';
import '../../../core/widgets/inmoment_feedback.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group.dart';
import '../../invitations/api/invitations_api.dart';
import '../api/search_api.dart';
import '../models/user_search_item.dart';

class FindFriendsPage extends StatefulWidget {
  const FindFriendsPage({super.key});

  @override
  State<FindFriendsPage> createState() => _FindFriendsPageState();
}

class _FindFriendsPageState extends State<FindFriendsPage> {
  final _api = SearchApi();
  final _invites = InvitationsApi();
  final _controller = TextEditingController();
  final _groupController = ActiveGroupController.instance;

  List<UserSearchItem> _results = [];
  bool _loading = false;
  bool _loadingGroups = true;
  String? _error;
  String? _invitingKey;
  String? _selectedGroupId;

  Timer? _debounce;
  int _searchRequestId = 0;

  String get _query => _controller.text.trim();

  @override
  void initState() {
    super.initState();
    _groupController.addListener(_onGroupsChanged);
    _init();
  }

  @override
  void dispose() {
    _groupController.removeListener(_onGroupsChanged);
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  Future<void> _init() async {
    try {
      await _groupController.load(force: true);
    } finally {
      if (mounted){
        _syncSelectedGroup();
        
        setState(() {
        _loadingGroups = false;
        });
      }
    }
  }

  void _onGroupsChanged() {
    if (!mounted) return;

    _syncSelectedGroup();
    setState(() {});
  }

  void _syncSelectedGroup() {
    final ownedGroups = _groupController.ownedGroups;

    if (ownedGroups.isEmpty) {
      _selectedGroupId = null;
      return;
    }

    final currentId = _selectedGroupId;
    final stillExists = ownedGroups.any((group) => group.id == currentId);
    if (stillExists) return;

    _selectedGroupId = _groupController.invitationGroup?.id ?? ownedGroups.first.id;
  }

  List<Group> get _ownedGroups => _groupController.ownedGroups;

  Group? get _selectedGroup {
    for (final group in _ownedGroups) {
      if (group.id == _selectedGroupId) {
        return group;
      }
    }
    return null;
  }

  bool get _hasOwnedGroups => _ownedGroups.isNotEmpty;

  bool _looksLikeEmail(String value) {
    final email = value.trim();
    if (email.isEmpty) return false;

    final regex = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    return regex.hasMatch(email);
  }

  bool _looksLikePhone(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) return false;

    final normalized = trimmed.replaceAll(RegExp(r'[^\d+]'), '');
    final regex = RegExp(r'^\+?\d{7,15}$');
    return regex.hasMatch(normalized);
  }

  void _onSearchChanged(String value) {
    _debounce?.cancel();
    final requestId = ++_searchRequestId;

    _debounce = Timer(const Duration(milliseconds: 350), () async {
      final query = value.trim();

      if (query.isEmpty) {
        if (!mounted) return;

        setState(() {
          _results = [];
          _error = null;
          _loading = false;
        });
        return;
      }

      if (!mounted) return;

      setState(() {
        _loading = true;
        _error = null;
      });

      try {
        final users = await _api.searchUsersFlexible(query);

        if (!mounted) return;
        if (requestId != _searchRequestId) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _results = users;
          _loading = false;
        });
      } catch (e) {
        if (!mounted) return;
        if (requestId != _searchRequestId) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _loading = false;
          _error = ApiError.normalize(
            e,
            fallback: 'Не удалось выполнить поиск пользователей.',
          );
          _results = [];
        });
      }
    });
  }

  Future<void> _inviteByUserName(UserSearchItem user) async {
    if (_invitingKey != null) return;

    final group = _selectedGroup;

    if (group == null) {
      _showMessage(
        'У вас нет собственной группы, которой вы управляете, чтобы пригласить пользователя.',
      );
      return;
    }

    setState(() {
      _invitingKey = user.userName;
    });

    try {
      await _invites.inviteByUserName(
        groupId: group.id,
        userName: user.userName,
      );

      if (!mounted) return;
      _showMessage('Приглашение отправлено в группу «${group.name}» пользователю @${user.userName}');
    } catch (e) {
      if (!mounted) return;
      _showInviteError(e);
    } finally {
      if (mounted) {
        setState(() {
          _invitingKey = null;
        });
      }
    }
  }

  Future<void> _inviteByEmailDirect() async {
    if (_invitingKey != null) return;

    final group = _selectedGroup;
    final query = _query;

    if (group == null) {
      _showMessage(
        'У вас нет собственной группы, которой вы управляете, чтобы пригласить пользователя.',
      );
      return;
    }

    if (!_looksLikeEmail(query)) return;

    setState(() {
      _invitingKey = query;
    });

    try {
      await _invites.inviteByEmail(
        groupId: group.id,
        email: query,
      );

      if (!mounted) return;

      _showMessage('Приглашение отправлено в группу «${group.name}» на $query');
    } catch (e) {
      if (!mounted) return;
      _showInviteError(e);
    } finally {
      if (mounted) {
        setState(() {
          _invitingKey = null;
        });
      }
    }
  }

  void _showInviteError(Object error) {
    final message = ApiError.normalize(
      error,
      fallback: 'Не удалось отправить приглашение.',
    );

    if (message.contains('Only owner or admin can perform this action')) {
      _showMessage(
        'У вас нет собственной управляемой группы для приглашений. Приглашать пользователей можно только в свою группу.',
      );
      return;
    }

    _showMessage(message);
  }

  void _showMessage(String text) {
    InMomentFeedback.showInfo(context, text);
  }

  @override
  Widget build(BuildContext context) {
    final activeGroup = _groupController.activeGroup;
    final selectedGroup = _selectedGroup;
    final query = _query;
    final isEmail = _looksLikeEmail(query);
    final isPhone = _looksLikePhone(query);

    if (_loadingGroups) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Пригласить в группу'),
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 10),
            child: Column(
              children: [
                TextField(
                  controller: _controller,
                  onChanged: _onSearchChanged,
                  style: const TextStyle(color: AppColors.textPrimary),
                  decoration: const InputDecoration(
                    hintText: 'username, email или телефон',
                    prefixIcon: Icon(Icons.search),
                  ),
                ),
                const SizedBox(height: 10),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(14),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        activeGroup == null
                            ? 'Активная группа сейчас не выбрана.'
                            : 'Активная группа: ${activeGroup.name}',
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 13,
                        ),
                      ),
                      const SizedBox(height: 10),
                      if (_hasOwnedGroups)
                        DropdownButtonFormField<String>(
                          initialValue: selectedGroup?.id,
                          dropdownColor: AppColors.surfaceLight,
                          decoration: const InputDecoration(
                            labelText: 'Группа для приглашения',
                          ),
                          items: _ownedGroups
                              .map(
                                (group) => DropdownMenuItem<String>(
                                  value: group.id,
                                  child: Text(group.name),
                                ),
                              )
                              .toList(),
                          onChanged: (value) {
                            if (value == null) return;
                            setState(() {
                              _selectedGroupId = value;
                            });
                          },
                        )
                      else
                        const Text(
                          'У вас нет собственных личных групп, которыми вы управляете, чтобы пригласить кого-то. Поиск будет работать, но кнопки приглашения будут недоступны.',
                          style: TextStyle(
                            color: AppColors.textPrimary,
                            fontSize: 14,
                            height: 1.45,
                          ),
                        ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          if (_loading)
            const Padding(
              padding: EdgeInsets.all(16),
              child: CircularProgressIndicator(),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Text(
                _error!,
                style: const TextStyle(color: Colors.redAccent),
              ),
            ),
          Expanded(
            child: _buildBody(
              query: query,
              isEmail: isEmail,
              isPhone: isPhone,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody({
    required String query,
    required bool isEmail,
    required bool isPhone,
  }) {
    if (query.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'Введите username, email или телефон, чтобы найти пользователя и пригласить его в группу.',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 14,
            ),
          ),
        ),
      );
    }

    if (_loading) {
      return const SizedBox.shrink();
    }

    if (_results.isEmpty && _error == null) {
      if (isEmail) {
        return Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(20),
                border: Border.all(color: AppColors.border),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'Пользователь с таким email среди зарегистрированных аккаунтов не найден.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 14,
                      height: 1.45,
                    ),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Можно сразу отправить приглашение на email.',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 14),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: _hasOwnedGroups && _invitingKey == null
                          ? _inviteByEmailDirect
                          : null,
                      icon: _invitingKey == query
                          ? const SizedBox(
                              width: 16,
                              height: 16,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.send_rounded),
                      label: const Text('Отправить приглашение на email'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        );
      }

      if (isPhone) {
        return const Center(
          child: Padding(
            padding: EdgeInsets.all(24),
            child: Text(
              'По этому номеру телефона зарегистрированный пользователь не найден.',
              textAlign: TextAlign.center,
              style: TextStyle(
                color: AppColors.textSecondary,
                fontSize: 14,
              ),
            ),
          ),
        );
      }

      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'Ничего не найдено.',
            textAlign: TextAlign.center,
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 14,
            ),
          ),
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
      itemCount: _results.length,
      itemBuilder: (context, index) {
        final user = _results[index];
        final isInviting = _invitingKey == user.userName;

        return Container(
          margin: const EdgeInsets.only(bottom: 12),
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: AppColors.border),
          ),
          child: ListTile(
            contentPadding: const EdgeInsets.symmetric(
              horizontal: 14,
              vertical: 8,
            ),
            leading: CircleAvatar(
              backgroundColor: AppColors.accent.withValues(alpha: 0.2),
              backgroundImage: user.profilePhotoUrl != null
                  ? NetworkImage(user.profilePhotoUrl!)
                  : null,
              child: user.profilePhotoUrl == null
                  ? Text(
                      user.userName.isNotEmpty
                          ? user.userName[0].toUpperCase()
                          : 'U',
                      style: const TextStyle(color: AppColors.textPrimary),
                    )
                  : null,
            ),
            title: Text(
              user.title,
              style: const TextStyle(color: AppColors.textPrimary),
            ),
            subtitle: Text(
              _buildSubtitle(user),
              style: const TextStyle(color: AppColors.textSecondary),
            ),
            trailing: FilledButton(
              onPressed: !_hasOwnedGroups || isInviting
                  ? null
                  : () => _inviteByUserName(user),
              child: isInviting
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Пригласить'),
            ),
          ),
        );
      },
    );
  }

  String _buildSubtitle(UserSearchItem user) {
    final parts = <String>['@${user.userName}'];

    final matchedBy = user.matchedBy?.trim();
    final matchedValue = user.matchedValue?.trim();

    if (matchedBy != null && matchedBy.isNotEmpty) {
      if (matchedBy == 'email' && matchedValue != null && matchedValue.isNotEmpty) {
        parts.add('совпадение по email: $matchedValue');
      } else if (matchedBy == 'phone' &&
          matchedValue != null &&
          matchedValue.isNotEmpty) {
        parts.add('совпадение по телефону: $matchedValue');
      }
    }

    return parts.join('\n');
  }
}