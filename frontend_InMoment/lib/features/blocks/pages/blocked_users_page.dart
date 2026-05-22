import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../../../core/widgets/inmoment_page_shell.dart';
import '../../contacts/api/search_api.dart';
import '../../contacts/models/user_search_item.dart';
import '../api/blocks_api.dart';
import '../models/blocked_user.dart';

class BlockedUsersPage extends StatefulWidget {
  const BlockedUsersPage({super.key});

  @override
  State<BlockedUsersPage> createState() => _BlockedUsersPageState();
}

class _BlockedUsersPageState extends State<BlockedUsersPage> {
  final BlocksApi _api = BlocksApi();
  final SearchApi _searchApi = SearchApi();
  final TextEditingController _controller = TextEditingController();

  bool _loading = true;
  bool _searching = false;
  String? _error;
  String? _busyUserId;
  Timer? _debounce;

  List<BlockedUser> _blocked = const [];
  List<UserSearchItem> _searchResults = const [];

  @override
  void initState() {
    super.initState();
    _controller.addListener(_onQueryChanged);
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.removeListener(_onQueryChanged);
    _controller.dispose();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }

    try {
      final items = await _api.getBlockedUsers().timeout(
        const Duration(seconds: 12),
        onTimeout: () => throw Exception('Не удалось загрузить блокировки'),
      );

      if (!mounted) return;

      setState(() {
        _blocked = items;
        _loading = false;
        _error = null;
      });
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
       _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить блокировки.',
        );
      });
    }
  }

  void _onQueryChanged() {
    final query = _controller.text.trim();
    _debounce?.cancel();

    if (query.isEmpty) {
      setState(() {
        _searchResults = const [];
        _searching = false;
      });
      return;
    }

    setState(() {
      _searching = true;
    });

    _debounce = Timer(const Duration(milliseconds: 350), () async {
      try {
        final result = await _searchApi.searchUsersFlexible(query).timeout(
          const Duration(seconds: 10),
          onTimeout: () => <UserSearchItem>[],
        );

        if (!mounted) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _searchResults = result
              .where((item) => !_blocked.any((b) => b.userId == item.id))
              .toList(growable: false);
          _searching = false;
        });
      } catch (_) {
        if (!mounted) return;
        if (_controller.text.trim() != query) return;

        setState(() {
          _searchResults = const [];
          _searching = false;
        });
      }
    });
  }

  Future<void> _block(UserSearchItem user) async {
    if (_busyUserId != null) return;

    setState(() {
      _busyUserId = user.id;
    });

    try {
      await _api.blockUser(user.id);
      await _load(silent: true);

      if (!mounted) return;

      _controller.clear();
      setState(() {
        _searchResults = const [];
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Пользователь @${user.userName} заблокирован')),
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось загрузить данные.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _busyUserId = null;
        });
      }
    }
  }

  Future<void> _unblock(BlockedUser user) async {
    if (_busyUserId != null) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Разблокировать пользователя',
      message: 'Разблокировать @${user.userName}?',
      confirmText: 'Разблокировать',
    );

    if (confirm != true) return;

    setState(() {
      _busyUserId = user.userId;
    });

    try {
      await _api.unblockUser(user.userId);
      await _load(silent: true);

      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('@${user.userName} разблокирован')),
      );
    } catch (e) {
      if (!mounted) return;

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось загрузить данные.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _busyUserId = null;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return InMomentPageShell(
      title: 'Блокировки',
      showSurface: false,
      scrollable: false,
      contentPadding: EdgeInsets.zero,
      actions: [
        if (_busyUserId != null || _loading)
          const Padding(
            padding: EdgeInsets.only(right: 14),
            child: Center(
              child: SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          )
        else
          IconButton(
            onPressed: _load,
            icon: const Icon(Icons.refresh_rounded),
            color: AppColors.textPrimary,
          ),
      ],
      child: RefreshIndicator(
        onRefresh: () => _load(silent: true),
        child: _loading
            ? const Center(child: CircularProgressIndicator())
            : _error != null
                ? ListView(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: const EdgeInsets.fromLTRB(10, 12, 10, 140),
                    children: [
                      _SimpleCard(
                        child: Column(
                          children: [
                            Text(
                              _error!,
                              textAlign: TextAlign.center,
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                height: 1.38,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                            const SizedBox(height: 14),
                            FilledButton(
                              onPressed: _load,
                              child: const Text('Повторить'),
                            ),
                          ],
                        ),
                      ),
                    ],
                  )
                : ListView(
                    physics: const AlwaysScrollableScrollPhysics(
                      parent: BouncingScrollPhysics(),
                    ),
                    padding: const EdgeInsets.fromLTRB(10, 8, 10, 140),
                    children: [
                      const _SectionTitle('Добавить блокировку'),
                      const SizedBox(height: 10),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          TextField(
                            controller: _controller,
                            style: const TextStyle(
                              color: AppColors.textPrimary,
                              fontWeight: FontWeight.w700,
                            ),
                            decoration: InputDecoration(
                              hintText: 'username, email или телефон',
                              prefixIcon: const Icon(Icons.search_rounded),
                              suffixIcon: _searching
                                  ? const Padding(
                                      padding: EdgeInsets.all(14),
                                      child: SizedBox(
                                        width: 16,
                                        height: 16,
                                        child: CircularProgressIndicator(
                                          strokeWidth: 2,
                                        ),
                                      ),
                                    )
                                  : null,
                            ),
                          ),
                          if (_searchResults.isNotEmpty) ...[
                            const SizedBox(height: 12),
                            ..._searchResults.map(
                              (item) => Padding(
                                padding: const EdgeInsets.only(bottom: 8),
                                child: _SearchUserTile(
                                  item: item,
                                  busy: _busyUserId == item.id,
                                  onBlock: () => _block(item),
                                ),
                              ),
                            ),
                          ],
                        ],
                      ),
                      const SizedBox(height: 18),
                      const _SectionTitle('Заблокированные пользователи'),
                      const SizedBox(height: 10),
                      if (_blocked.isEmpty)
                        const Padding(
                          padding: EdgeInsets.symmetric(horizontal: 4, vertical: 6),
                          child: Text(
                            'Список блокировок пуст.',
                            style: TextStyle(
                              color: AppColors.textSecondary,
                              height: 1.35,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                        )
                      else
                        ..._blocked.map(
                          (item) => Padding(
                            padding: const EdgeInsets.only(bottom: 10),
                            child: _BlockedUserTile(
                              item: item,
                              busy: _busyUserId == item.userId,
                              onUnblock: () => _unblock(item),
                            ),
                          ),
                        ),
                    ],
                  ),
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
        fontSize: 16,
        fontWeight: FontWeight.w800,
      ),
    );
  }
}

class _SimpleCard extends StatelessWidget {
  final Widget child;

  const _SimpleCard({
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.softStroke(0.10)),
      ),
      child: child,
    );
  }
}

class _SearchUserTile extends StatelessWidget {
  final UserSearchItem item;
  final bool busy;
  final VoidCallback onBlock;

  const _SearchUserTile({
    required this.item,
    required this.busy,
    required this.onBlock,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.softStroke(0.11)),
      ),
      child: Row(
        children: [
          CircleAvatar(
            radius: 20,
            backgroundColor: AppColors.accent.withValues(alpha: 0.24),
            backgroundImage: item.profilePhotoUrl != null &&
                    item.profilePhotoUrl!.trim().isNotEmpty
                ? NetworkImage(item.profilePhotoUrl!)
                : null,
            child: item.profilePhotoUrl == null ||
                    item.profilePhotoUrl!.trim().isEmpty
                ? Text(
                    item.userName.isNotEmpty
                        ? item.userName[0].toUpperCase()
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
            child: Text(
              item.title,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          const SizedBox(width: 10),
          OutlinedButton(
            onPressed: busy ? null : onBlock,
            child: Text(busy ? '...' : 'Блокировать'),
          ),
        ],
      ),
    );
  }
}

class _BlockedUserTile extends StatelessWidget {
  final BlockedUser item;
  final bool busy;
  final VoidCallback onUnblock;

  const _BlockedUserTile({
    required this.item,
    required this.busy,
    required this.onUnblock,
  });

  @override
  Widget build(BuildContext context) {
    final fullName = item.fullName.trim();

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceGlass(0.18),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.softStroke(0.11)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              CircleAvatar(
                radius: 20,
                backgroundColor: AppColors.accent.withValues(alpha: 0.24),
                backgroundImage: item.profilePhotoUrl != null &&
                        item.profilePhotoUrl!.trim().isNotEmpty
                    ? NetworkImage(item.profilePhotoUrl!)
                    : null,
                child: item.profilePhotoUrl == null ||
                        item.profilePhotoUrl!.trim().isEmpty
                    ? Text(
                        item.userName.isNotEmpty
                            ? item.userName[0].toUpperCase()
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
                      fullName.isEmpty ? item.userName : fullName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      '@${item.userName}',
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Align(
            alignment: Alignment.center,
            child: OutlinedButton(
              onPressed: busy ? null : onUnblock,
              style: OutlinedButton.styleFrom(
                minimumSize: const Size(0, 40),
                padding: const EdgeInsets.symmetric(horizontal: 18),
              ),
              child: Text(busy ? '...' : 'Разблокировать'),
            ),
          ),
        ],
      ),
    );
  }
}