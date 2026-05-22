import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import '../../../app/app.dart';
import '../../../core/api/api_client.dart';
import '../../../core/api/api_error.dart';
import '../../../core/controllers/app_session_controller.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_action_tile.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../../../core/widgets/inmoment_surface.dart';
import '../../../core/widgets/group_dropdown_selector.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/layout/inmoment_media_frame.dart';
import '../../feed/api/feed_api.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../groups/models/group_summary.dart';
import '../../invitations/api/invitations_api.dart';
import '../api/profile_api.dart';
import '../models/user_profile.dart';
import '../../groups/pages/create_group_page.dart';
import '../../account/pages/delete_account_page.dart';
import '../../blocks/pages/blocked_users_page.dart';
import '../../privacy/pages/privacy_page.dart';
import '../../support/pages/policy_page.dart';
import '../../support/pages/support_page.dart';
import 'change_password_page.dart';
import '../../notifications/pages/notification_settings_page.dart';
import '../../notifications/pages/system_announcements_page.dart';
import '../../photo/pages/photo_details_page.dart';
import '../../reports/pages/my_reports_page.dart';
import '../../reports/pages/moderation_reports_page.dart';
import '../../reports/pages/moderation_deletion_requests_page.dart';
import '../../support/pages/about_app_page.dart';
import '../../sessions/pages/sessions_page.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

enum _ProfileTab {
  groups,
  ownedGroups,
  invitations,
}

class _ProfilePageState extends State<ProfilePage> {
  final ProfileApi _profileApi = ProfileApi();
  final InvitationsApi _invitationsApi = InvitationsApi();
  final ActiveGroupController _groupController = ActiveGroupController.instance;
  final FeedApi _feedApi = FeedApi();

  bool _loading = true;
  bool _refreshingProfile = false;
  bool _savingActiveGroup = false;
  bool _loggingOut = false;
  String? _acceptingInvitationId;
  String? _error;

  UserProfile? _profile;
  _ProfileTab _selectedTab = _ProfileTab.groups;

  bool _widgetLoading = false;
  String? _widgetPreviewUrl;
  String? _widgetPreviewContentType;
  String? _widgetPreviewCaption;
  String? _widgetPreviewPhotoId;

  @override
  void initState() {
    super.initState();
    _groupController.addListener(_onGroupControllerChanged);
    _startInitialLoad();
  }

  void _startInitialLoad() {
    Future.microtask(() async {
      if (!mounted) return;

      setState(() {
        _loading = false;
      });

      await _load();
    });
  }

  @override
  void dispose() {
    _groupController.removeListener(_onGroupControllerChanged);
    super.dispose();
  }

  void _onGroupControllerChanged() {
    if (!mounted) return;
    final profile = _profile;
    if (profile == null) return;

    final groups = _mergeWithControllerGroups(profile.groups);
    final activeGroup = _groupController.activeGroup;

    setState(() {
      _profile = profile.copyWith(
        groups: groups,
        groupsCount: groups.length,
        activeGroupId: activeGroup?.id,
      );
    });

    _loadWidgetPreview();
  }

  Future<void> _load({bool silent = false}) async {
    if (_loading || _refreshingProfile) return;

    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    } else {
      setState(() {
        _refreshingProfile = true;
      });
    }

    try {
      final profile = await _profileApi.getMe();
      _groupController.syncFromProfile(profile);

      if (!mounted) return;

      final groups = _mergeWithControllerGroups(profile.groups);
      final activeGroup = _groupController.activeGroup;

      setState(() {
        _profile = profile.copyWith(
          groups: groups,
          groupsCount: groups.length,
          activeGroupId: activeGroup?.id ?? profile.activeGroupId,
        );
        _loading = false;
        _refreshingProfile = false;
        _error = null;
      });

      await _loadWidgetPreview();
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loading = false;
        _refreshingProfile = false;

        final message = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль. Попробуйте ещё раз.',
        );

        if (_profile == null || !silent) {
          _error = message;
        }
      });

      if (silent && mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              ApiError.normalize(
                e,
                fallback: 'Не удалось обновить профиль. Попробуйте ещё раз.',
              ),
            ),
          ),
        );
      }
    }
  }

  List<GroupSummary> _mergeWithControllerGroups(List<GroupSummary> fallback) {
    final groups = _groupController.groups;
    if (groups.isEmpty) return fallback;
    return groups;
  }

 Future<void> _loadWidgetPreview() async {
    final profile = _profile;
    if (profile == null) return;

    GroupSummary? activeGroup;
    for (final group in profile.groups) {
      if (group.id == profile.activeGroupId) {
        activeGroup = group;
        break;
      }
    }

    if (activeGroup == null) {
      if (!mounted) return;
      setState(() {
        _widgetPreviewUrl = null;
        _widgetPreviewContentType = null;
        _widgetPreviewCaption = null;
        _widgetPreviewPhotoId = null;
        _widgetLoading = false;
      });
      return;
    }

    setState(() {
      _widgetLoading = true;
    });

    try {
      final feed = await _feedApi.getGroupFeed(activeGroup.id);
      if (!mounted) return;

      if (feed.isEmpty) {
        setState(() {
          _widgetPreviewUrl = null;
          _widgetPreviewContentType = null;
          _widgetPreviewCaption = null;
          _widgetPreviewPhotoId = null;
          _widgetLoading = false;
        });
        return;
      }

      final latest = feed.first;
      setState(() {
        _widgetPreviewUrl = latest.url;
        _widgetPreviewContentType = latest.contentType;
        _widgetPreviewCaption = (latest.caption ?? '').trim();
        _widgetPreviewPhotoId = latest.photoId;
        _widgetLoading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _widgetPreviewUrl = null;
        _widgetPreviewContentType = null;
        _widgetPreviewCaption = null;
        _widgetPreviewPhotoId = null;
        _widgetLoading = false;
      });
    }
  }

  Future<void> _openModerationPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const ModerationReportsPage(),
      ),
    );
  }

  Future<void> _openModerationDeletionRequestsPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const ModerationDeletionRequestsPage(),
      ),
    );
  }

  Future<void> _openSystemAnnouncementsPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SystemAnnouncementsPage(),
      ),
    );
  }

  Future<void> _changeActiveGroup(GroupSummary? group) async {
    final profile = _profile;
    if (profile == null || group == null) return;
    if (profile.activeGroupId == group.id) return;

    setState(() {
      _savingActiveGroup = true;
    });

    try {
      await _groupController.setActiveGroup(group);

      if (!mounted) return;

      final groups = _mergeWithControllerGroups(profile.groups);

      setState(() {
        _profile = profile.copyWith(
          activeGroupId: group.id,
          groups: groups,
          groupsCount: groups.length,
        );
        _savingActiveGroup = false;
      });

      await _loadWidgetPreview();

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Активная группа: ${group.name}')),
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _savingActiveGroup = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось выполнить действие. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    }
  }

  Future<void> _acceptInvitation(PendingInvitationPreview invitation) async {
    if (_acceptingInvitationId != null) return;

    final profileBefore = _profile;

    setState(() {
      _acceptingInvitationId = invitation.invitationId;

      if (profileBefore != null) {
        final filteredInvitations = profileBefore.pendingInvitations
            .where((item) => item.invitationId != invitation.invitationId)
            .toList(growable: false);

        _profile = profileBefore.copyWith(
          pendingInvitations: filteredInvitations,
          pendingInvitationsCount: filteredInvitations.length,
        );
      }
    });

    try {
      await _invitationsApi.acceptInvitation(invitation.invitationId);
      await _groupController.load(force: true);
      await _load(silent: true);

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Вы вступили в группу «${invitation.groupName}»'),
        ),
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _profile = profileBefore;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
           content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось выполнить действие. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    } finally {
      if (mounted) {
        setState(() {
          _acceptingInvitationId = null;
        });
      }
    }
  }

 Future<void> _openNotificationsSheet() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const NotificationSettingsPage(),
      ),
    );

    if (!mounted) return;
    await _load(silent: true);
  }

  Future<void> _openNameSheet() async {
    final profile = _profile;
    if (profile == null) return;

    final updated = await showModalBottomSheet<UserProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _LargeBottomSheet(
        child: _EditNameSheet(
          profile: profile,
          api: _profileApi,
        ),
      ),
    );

    if (!mounted || updated == null) return;

    _groupController.syncFromProfile(updated);

    setState(() {
      _profile = updated.copyWith(
        groups: _mergeWithControllerGroups(updated.groups),
        groupsCount: _mergeWithControllerGroups(updated.groups).length,
        activeGroupId: _groupController.activeGroup?.id ?? updated.activeGroupId,
      );
    });
  }

  Future<void> _openUserNameSheet() async {
    final profile = _profile;
    if (profile == null) return;

    final updated = await showModalBottomSheet<UserProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _LargeBottomSheet(
        child: _EditUserNameSheet(
          profile: profile,
          api: _profileApi,
        ),
      ),
    );

    if (!mounted || updated == null) return;

    _groupController.syncFromProfile(updated);

    setState(() {
      _profile = updated.copyWith(
        groups: _mergeWithControllerGroups(updated.groups),
        groupsCount: _mergeWithControllerGroups(updated.groups).length,
        activeGroupId: _groupController.activeGroup?.id ?? updated.activeGroupId,
      );
    });
  }

  Future<void> _openPhoneSheet() async {
    final profile = _profile;
    if (profile == null) return;

    final updated = await showModalBottomSheet<UserProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _LargeBottomSheet(
        child: _EditPhoneSheet(
          profile: profile,
          api: _profileApi,
        ),
      ),
    );

    if (!mounted || updated == null) return;

    _groupController.syncFromProfile(updated);

    setState(() {
      _profile = updated.copyWith(
        groups: _mergeWithControllerGroups(updated.groups),
        groupsCount: _mergeWithControllerGroups(updated.groups).length,
        activeGroupId: _groupController.activeGroup?.id ?? updated.activeGroupId,
      );
    });
  }

  Future<void> _openEmailSheet() async {
    final profile = _profile;
    if (profile == null) return;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _LargeBottomSheet(
        child: _EditEmailSheet(profile: profile),
      ),
    );
  }

  Future<void> _openAvatarSheet() async {
    final profile = _profile;
    if (profile == null) return;

    final updated = await showModalBottomSheet<UserProfile>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _LargeBottomSheet(
        child: _EditAvatarSheet(
          profile: profile,
          onUploaded: (updatedProfile) {
            Navigator.of(context).pop(updatedProfile);
          },
        ),
      ),
    );

    if (!mounted || updated == null) return;

    _groupController.syncFromProfile(updated);
    setState(() {
      _profile = updated.copyWith(
        groups: _mergeWithControllerGroups(updated.groups),
        groupsCount: _mergeWithControllerGroups(updated.groups).length,
        activeGroupId: _groupController.activeGroup?.id ?? updated.activeGroupId,
      );
    });
  }

  Future<void> _openCreateGroupPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const CreateGroupPage(),
      ),
    );

    if (!mounted) return;
    await _groupController.load(force: true);
    await _load(silent: true);
  }

 Future<void> _openPrivacySheet() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const PrivacyPage(),
      ),
    );

    if (!mounted) return;
    await _load(silent: true);
  }

  Future<void> _openSessionsSheet() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SessionsPage(),
      ),
    );
  }

  Future<void> _openGroupsSheet({
    required String title,
    required List<GroupSummary> groups,
  }) async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      enableDrag: true,
      useSafeArea: true,
      backgroundColor: Colors.transparent,
      builder: (_) => _ProfileTabBottomSheet(
        child: _ProfileTabSheetContent(
          title: title,
          emptyText: 'Список пока пуст.',
          children: groups
              .map(
                (group) => _GroupRow(group: group),
              )
              .toList(),
        ),
      ),
    );
  }

  Future<void> _openInvitationsSheet() async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      isDismissible: true,
      enableDrag: true,
      useSafeArea: true,
      backgroundColor: Colors.transparent,
      builder: (_) => StatefulBuilder(
        builder: (context, modalSetState) {
          final invitations =
              _profile?.pendingInvitations ?? const <PendingInvitationPreview>[];

          return _ProfileTabBottomSheet(
            child: _ProfileTabSheetContent(
              title: 'Приглашения',
              emptyText: 'Нет ожидающих приглашений.',
              children: invitations
                  .map(
                    (invitation) => _InvitationRow(
                      invitation: invitation,
                      accepting:
                          _acceptingInvitationId == invitation.invitationId,
                      onAccept: () async {
                        await _acceptInvitation(invitation);
                        if (mounted) {
                          modalSetState(() {});
                        }
                      },
                    ),
                  )
                  .toList(),
            ),
          );
        },
      ),
    );
  }

  Future<void> _openWidgetPreviewPublication() async {
    final photoId = _widgetPreviewPhotoId?.trim();
    if (photoId == null || photoId.isEmpty) return;

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => PhotoDetailsPage(
          photoId: photoId,
          groupId: _profile?.activeGroupId,
        ),
      ),
    );
  }

  Future<void> _showWidgetHelp() async {
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (_) => const _LargeBottomSheet(
        child: _InfoSheet(
          title: 'Виджет',
          text:
              'Активная группа используется как основной контекст главного экрана и будущего виджета. В карточке слева показываем последнюю фотографию этой группы.',
        ),
      ),
    );
  }
  
  Future<void> _openSupportHubPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const SupportPage(),
      ),
    );
  }

  Future<void> _openAboutAppPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const AboutAppPage(),
      ),
    );
  }

  Future<void> _openPolicyPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const PolicyPage(),
      ),
    );
  }

  Future<void> _openBlockedUsersPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const BlockedUsersPage(),
      ),
    );
  }

  Future<void> _openMyReportsPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const MyReportsPage(),
      ),
    );
  }

  Future<void> _openDeleteAccountPage() async {
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => const DeleteAccountPage(),
      ),
    );
  }

 Future<void> _openChangePasswordPage() async {
    await ChangePasswordPage.show(context);
  }

  Future<void> _logout() async {
    if (_loggingOut) return;

    final confirm = await showInMomentConfirmDialog(
      context: context,
      title: 'Выйти из аккаунта',
      message: 'Вы уверены, что хотите выйти из текущего аккаунта?',
      confirmText: 'Выйти',
      danger: true,
    );

    if (confirm != true) return;

    setState(() {
      _loggingOut = true;
    });

    try {
      await AppSessionController.instance.logout();
      _groupController.reset();

      if (!mounted) return;

      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => const AppBootstrap(),
        ),
        (route) => false,
      );
    } catch (e) {
      if (!mounted) return;

      setState(() {
        _loggingOut = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            ApiError.normalize(
              e,
              fallback: 'Не удалось выполнить выход. Попробуйте ещё раз.',
            ),
          ),
        ),
      );
    }
  }

  String _formatRegistrationDate(DateTime? value) {
    if (value == null) return '—';

    final local = value.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year}';
  }

  String _buildInitials(UserProfile profile) {
    final first = profile.firstName.trim().isNotEmpty
        ? profile.firstName.trim()[0]
        : '';
    final last = profile.lastName.trim().isNotEmpty
        ? profile.lastName.trim()[0]
        : '';

    final joined = '$first$last'.trim();
    if (joined.isNotEmpty) return joined.toUpperCase();

    if (profile.userName.trim().isNotEmpty) {
      return profile.userName.trim()[0].toUpperCase();
    }

    return 'IM';
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_error != null) {
      return Scaffold(
        backgroundColor: AppColors.background,
        appBar: AppBar(title: const Text('Профиль')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: InMomentSurface(
              tone: InMomentSurfaceTone.elevated,
              borderRadius: BorderRadius.circular(28),
              padding: const EdgeInsets.all(18),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    _error!,
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      height: 1.45,
                    ),
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _loading ? null : _load,
                    child: _loading
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
        ),
      );
    }

    final profile = _profile!;
    final groups = profile.groups;
    final ownedGroups =
        groups.where((group) => group.isOwner).toList(growable: false);
    final invitations = profile.pendingInvitations;
    final contentWidth = InMomentMediaFrame.resolveTabletContentWidth(
      MediaQuery.sizeOf(context).width,
    );

    GroupSummary? activeGroup;
    for (final group in groups) {
      if (group.id == profile.activeGroupId) {
        activeGroup = group;
        break;
      }
    }

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Профиль'),
        actions: [
          IconButton(
            tooltip: 'Обновить',
            onPressed: _loading || _refreshingProfile
                ? null
                : () => _load(silent: false),
            icon: _refreshingProfile
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Icon(Icons.refresh_rounded),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          await _load(silent: true);
        },
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(14, 12, 14, 128),
          children: [
            Center(
              child: SizedBox(
                width: contentWidth,
                child: _CompactProfileHeader(
                  profile: profile,
                  initials: _buildInitials(profile),
                  registrationText: _formatRegistrationDate(profile.createdAt),
                ),
              ),
            ),
            const SizedBox(height: 16),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: const _BlockTitle(title: 'Виджет'),
              ),
            ),
            const SizedBox(height: 8),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: _WidgetProfileDashboard(
                  groupName: activeGroup?.name,
                  loading: _widgetLoading,
                  previewUrl: _widgetPreviewUrl,
                  contentType: _widgetPreviewContentType,
                  caption: _widgetPreviewCaption,
                  previewPhotoId: _widgetPreviewPhotoId,
                  savingActiveGroup: _savingActiveGroup,
                  groups: groups,
                  activeGroupId: profile.activeGroupId,
                  selectedTab: _selectedTab,
                  groupsCount: groups.length,
                  ownedGroupsCount: ownedGroups.length,
                  invitationsCount: invitations.length,
                  onHelpTap: _showWidgetHelp,
                  onPreviewTap: _openWidgetPreviewPublication,
                  onCreateGroupTap: _openCreateGroupPage,
                  onGroupChanged: (groupId) {
                    if (groupId == null) return;

                    GroupSummary? selected;

                    for (final group in groups) {
                      if (group.id == groupId) {
                        selected = group;
                        break;
                      }
                    }

                    if (selected != null) {
                      _changeActiveGroup(selected);
                    }
                  },
                  onStatsChanged: (tab) async {
                    setState(() {
                      _selectedTab = tab;
                    });

                    if (tab == _ProfileTab.groups) {
                      await _openGroupsSheet(
                        title: 'Группы',
                        groups: groups,
                      );
                    } else if (tab == _ProfileTab.ownedGroups) {
                      await _openGroupsSheet(
                        title: 'Личные группы',
                        groups: ownedGroups,
                      );
                    } else {
                      await _openInvitationsSheet();
                    }
                  },
                ),
              ),
            ),  
            const SizedBox(height: 16),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: const _BlockTitle(title: 'Основные'),
              ),
            ),
            const SizedBox(height: 8),
            Center(
              child: SizedBox(
                width: contentWidth,
                child:_SimpleSection(
                  children: [
                    _SimpleMenuTile(
                      title: 'Уведомления',
                      onTap: _openNotificationsSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Имя',
                      onTap: _openNameSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Ник',
                      onTap: _openUserNameSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Аватар',
                      onTap: _openAvatarSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Телефон',
                      onTap: _openPhoneSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Почта',
                      onTap: _openEmailSheet,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 14),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: const _BlockTitle(title: 'Помощь и документы'),
              ),
            ),
            const SizedBox(height: 8),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: _SimpleSection(
                  children: [
                    _SimpleMenuTile(
                      title: 'Поддержка',
                      onTap: _openSupportHubPage,
                    ),
                    _SimpleMenuTile(
                      title: 'Политика и данные',
                      onTap: _openPolicyPage,
                    ),
                    _SimpleMenuTile(
                      title: 'О приложении и документы',
                      onTap: _openAboutAppPage,
                    ),
                  ],
                ), 
              ),
            ),  
            if ((_profile?.isSystemModerator ?? false)) ...[
              const SizedBox(height: 16),
              Center(
                child: SizedBox(
                  width: contentWidth,
                  child: const _BlockTitle(title: 'Модерация'),
                ),
              ),
              const SizedBox(height: 8),
                Center(
                  child: SizedBox(
                    width: contentWidth,
                    child: _SimpleSection(
                    children: [
                      _SimpleMenuTile(
                        title: 'Жалобы пользователей',
                        onTap: _openModerationPage,
                      ),
                      _SimpleMenuTile(
                        title: 'Запросы на удаление аккаунтов',
                        onTap: _openModerationDeletionRequestsPage,
                      ),
                      _SimpleMenuTile(
                        title: 'Системные уведомления',
                        onTap: _openSystemAnnouncementsPage,
                      ),
                    ],
                  ),
                ),
              ),
            ],
            const SizedBox(height: 14),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: const _BlockTitle(title: 'Конфиденциальность и безопасность'),
              ),
            ),
            const SizedBox(height: 8),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: _SimpleSection(
                  children: [
                    _SimpleMenuTile(
                      title: 'Блокировки',
                      onTap: _openBlockedUsersPage,
                    ),
                    _SimpleMenuTile(
                      title: 'Мои жалобы',
                      onTap: _openMyReportsPage,
                    ),
                    _SimpleMenuTile(
                      title: 'Конфиденциальность и данные',
                      onTap: _openPrivacySheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Устройства и сессии',
                      onTap: _openSessionsSheet,
                    ),
                    _SimpleMenuTile(
                      title: 'Сменить пароль',
                      onTap: _openChangePasswordPage,
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 14),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: const _BlockTitle(title: 'Опасная зона'),
              ),
             ),
            const SizedBox(height: 8),
            Center(
              child: SizedBox(
                width: contentWidth,
                child: _SimpleSection(
                  danger: true,
                  children: [
                    _SimpleMenuTile(
                      title: 'Удалить аккаунт',
                      danger: true,
                      onTap: _openDeleteAccountPage,
                    ),
                    _SimpleMenuTile(
                      title: _loggingOut ? 'Выходим...' : 'Выйти',
                      danger: true,
                      onTap: _loggingOut ? null : _logout,
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CompactProfileHeader extends StatelessWidget {
  final UserProfile profile;
  final String initials;
  final String registrationText;

  const _CompactProfileHeader({
    required this.profile,
    required this.initials,
    required this.registrationText,
  });

  @override
  Widget build(BuildContext context) {
    final firstName =
        profile.firstName.trim().isEmpty ? '—' : profile.firstName.trim();
    final lastName =
        profile.lastName.trim().isEmpty ? '—' : profile.lastName.trim();

    return InMomentSurface(
      tone: InMomentSurfaceTone.elevated,
      borderRadius: BorderRadius.circular(28),
      padding: const EdgeInsets.all(16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            radius: 34,
            backgroundColor: AppColors.accent.withValues(alpha: 0.28),
            backgroundImage: profile.profilePhotoUrl != null &&
                    profile.profilePhotoUrl!.trim().isNotEmpty
                ? NetworkImage(profile.profilePhotoUrl!)
                : null,
            child: profile.profilePhotoUrl == null ||
                    profile.profilePhotoUrl!.trim().isEmpty
                ? Text(
                    initials,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontWeight: FontWeight.w800,
                    ),
                  )
                : null,
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Wrap(
                    spacing: 6,
                    runSpacing: 2,
                    children: [
                      Text(
                        firstName,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      Text(
                        lastName,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 3),
                  Text(
                    '@${profile.userName}',
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'С нами с $registrationText',
                    style: const TextStyle(
                      color: AppColors.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _BlockTitle extends StatelessWidget {
  final String title;

  const _BlockTitle({
    required this.title,
  });

  @override
  Widget build(BuildContext context) {
    return Text(
      title,
      style: const TextStyle(
        color: AppColors.textPrimary,
        fontSize: 15,
        fontWeight: FontWeight.w800,
      ),
    );
  }
}


class _WidgetProfileDashboard extends StatelessWidget {
  final String? groupName;
  final bool loading;
  final String? previewUrl;
  final String? contentType;
  final String? caption;
  final String? previewPhotoId;
  final bool savingActiveGroup;
  final List<GroupSummary> groups;
  final String? activeGroupId;
  final _ProfileTab selectedTab;
  final int groupsCount;
  final int ownedGroupsCount;
  final int invitationsCount;
  final ValueChanged<String?> onGroupChanged;
  final ValueChanged<_ProfileTab> onStatsChanged;
  final Future<void> Function() onHelpTap;
  final Future<void> Function() onPreviewTap;
  final VoidCallback onCreateGroupTap;

  const _WidgetProfileDashboard({
    required this.groupName,
    required this.loading,
    required this.previewUrl,
    required this.contentType,
    required this.caption,
    required this.previewPhotoId,
    required this.savingActiveGroup,
    required this.groups,
    required this.activeGroupId,
    required this.selectedTab,
    required this.groupsCount,
    required this.ownedGroupsCount,
    required this.invitationsCount,
    required this.onGroupChanged,
    required this.onStatsChanged,
    required this.onHelpTap,
    required this.onPreviewTap,
    required this.onCreateGroupTap,
  });

  @override
  Widget build(BuildContext context) {
    final frame = InMomentMediaFrame.resolveHomeSquare(
      viewportWidth: MediaQuery.sizeOf(context).width,
      viewportHeight: MediaQuery.sizeOf(context).height,
    );

    return Center(
      child: SizedBox(
        width: frame.width,
        child: Column(
          children: [
            _WidgetPreviewCard(
              frameSize: frame.width,
              groupName: groupName,
              loading: loading,
              previewUrl: previewUrl,
              contentType: contentType,
              caption: caption,
              previewPhotoId: previewPhotoId,
              savingActiveGroup: savingActiveGroup,
              groups: groups,
              activeGroupId: activeGroupId,
              onChanged: onGroupChanged,
              onHelpTap: onHelpTap,
              onPreviewTap: onPreviewTap,
            ),
            const SizedBox(height: 14),
            _StatsTabsBlock(
              selectedTab: selectedTab,
              groupsCount: groupsCount,
              ownedGroupsCount: ownedGroupsCount,
              invitationsCount: invitationsCount,
              vertical: false,
              onChanged: onStatsChanged,
            ),
            const SizedBox(height: 12),
            _CreateGroupButton(
              compact: false,
              onPressed: onCreateGroupTap,
            ),
          ],
        ),
      ),
    );
  }
}

class _WidgetPreviewCard extends StatelessWidget {
  final double frameSize;
  final String? groupName;
  final bool loading;
  final String? previewUrl;
  final String? contentType;
  final String? caption;
  final String? previewPhotoId;
  final bool savingActiveGroup;
  final List<GroupSummary> groups;
  final String? activeGroupId;
  final ValueChanged<String?> onChanged;
  final Future<void> Function() onHelpTap;
  final Future<void> Function() onPreviewTap;

  const _WidgetPreviewCard({
    required this.frameSize,
    required this.groupName,
    required this.loading,
    required this.previewUrl,
    required this.contentType,
    required this.caption,
    required this.previewPhotoId,
    required this.savingActiveGroup,
    required this.groups,
    required this.activeGroupId,
    required this.onChanged,
    required this.onHelpTap,
    required this.onPreviewTap,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: frameSize,
      child: InMomentSurface(
        tone: InMomentSurfaceTone.elevated,
        borderRadius: BorderRadius.circular(24),
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            GroupDropdownSelector(
              groups: groups,
              selectedGroupId: activeGroupId,
              hintText: 'Выберите группу',
              enabled: groups.isNotEmpty && !savingActiveGroup,
              isLoading: savingActiveGroup,
              height: 40,
              borderRadius: 16,
              avatarRadius: 12,
              fontSize: 13,
              padding: const EdgeInsets.symmetric(horizontal: 12),
              onChanged: groups.isEmpty || savingActiveGroup ? null : onChanged,
            ),
            const SizedBox(height: 10),
            SizedBox(
              height: frameSize,
              width: frameSize,
              child: Material(
                color: Colors.transparent,
                borderRadius: BorderRadius.circular(24),
                child: InkWell(
                  borderRadius: BorderRadius.circular(24),
                  onTap: previewPhotoId == null ||
                          previewPhotoId!.trim().isEmpty
                      ? null
                      : () => onPreviewTap(),
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      ClipRRect(
                        borderRadius: BorderRadius.circular(24),
                        child: previewUrl != null &&
                                previewUrl!.trim().isNotEmpty
                            ? NetworkVisualMedia(
                                url: previewUrl!,
                                contentType: contentType ?? 'image/jpeg',
                                allowInlineVideo: true,
                                autoplay: false,
                                looping: true,
                                startMuted: true,
                                showControls: false,
                                allowPlaybackSpeedChanging: false,
                                showVideoBadge: true,
                                fit: BoxFit.cover,
                                placeholderLabel:
                                    'Не удалось загрузить медиа',
                              )
                            : const _WidgetPreviewPlaceholder(),
                      ),
                      Container(
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(24),
                          gradient: LinearGradient(
                            begin: Alignment.topCenter,
                            end: Alignment.bottomCenter,
                            colors: [
                              Colors.black.withValues(alpha: 0.04),
                              Colors.transparent,
                              Colors.black.withValues(alpha: 0.28),
                            ],
                          ),
                        ),
                      ),
                      if (loading || savingActiveGroup)
                        Container(
                          decoration: BoxDecoration(
                            color: Colors.black.withValues(alpha: 0.18),
                            borderRadius: BorderRadius.circular(24),
                          ),
                          alignment: Alignment.center,
                          child: const SizedBox(
                            width: 24,
                            height: 24,
                            child: CircularProgressIndicator(strokeWidth: 2.4),
                          ),
                        ),
                      Positioned(
                        left: 12,
                        right: 12,
                        bottom: 12,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (groupName != null &&
                                groupName!.trim().isNotEmpty)
                              Text(
                                groupName!,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 14,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            if (caption != null &&
                                caption!.trim().isNotEmpty) ...[
                              const SizedBox(height: 4),
                              Text(
                                caption!,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 12.5,
                                  height: 1.3,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 10),
            SizedBox(
              width: double.infinity,
              height: 40,
              child: _WidgetSettingsButton(onTap: onHelpTap),
            ),
          ],
        ),
      ),
    );
  }
}

class _WidgetSettingsButton extends StatelessWidget {
  final Future<void> Function() onTap;

  const _WidgetSettingsButton({
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return InkWell(
      borderRadius: BorderRadius.circular(16),
      onTap: () => onTap(),
      child: Container(
        height: 40,
        padding: const EdgeInsets.symmetric(horizontal: 12),
        decoration: BoxDecoration(
          color: AppColors.surface.withValues(alpha: 0.16),
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: AppColors.softStroke(0.08),
          ),
        ),
        child: const Row(
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.settings_outlined,
              size: 18,
              color: AppColors.textPrimary,
            ),
            SizedBox(width: 8),
            Text(
              'Настроить',
              style: TextStyle(
                color: AppColors.textPrimary,
                fontSize: 12.5,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _WidgetPreviewPlaceholder extends StatelessWidget {
  const _WidgetPreviewPlaceholder();

  @override
  Widget build(BuildContext context) {
    return Container(
      color: AppColors.card,
      alignment: Alignment.center,
      child: const Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.widgets_outlined,
            color: AppColors.textSecondary,
            size: 28,
          ),
          SizedBox(height: 8),
          Text(
            'Последняя фотография группы',
            style: TextStyle(
              color: AppColors.textSecondary,
              fontSize: 12,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatsTabsBlock extends StatelessWidget {
  final _ProfileTab selectedTab;
  final int groupsCount;
  final int ownedGroupsCount;
  final int invitationsCount;
  final ValueChanged<_ProfileTab> onChanged;
  final bool vertical;

  const _StatsTabsBlock({
    required this.selectedTab,
    required this.groupsCount,
    required this.ownedGroupsCount,
    required this.invitationsCount,
    required this.onChanged,
    this.vertical = false,
  });

  @override
  Widget build(BuildContext context) {
    final cards = [
      _StatsTabCard(
        title: 'Группы',
        count: groupsCount,
        selected: selectedTab == _ProfileTab.groups,
        onTap: () => onChanged(_ProfileTab.groups),
      ),
      _StatsTabCard(
        title: 'Личные',
        count: ownedGroupsCount,
        selected: selectedTab == _ProfileTab.ownedGroups,
        onTap: () => onChanged(_ProfileTab.ownedGroups),
      ),
      _StatsTabCard(
        title: 'Инвайты',
        count: invitationsCount,
        selected: selectedTab == _ProfileTab.invitations,
        onTap: () => onChanged(_ProfileTab.invitations),
      ),
    ];

    if (vertical) {
      return Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (var index = 0; index < cards.length; index++) ...[
            cards[index],
            if (index != cards.length - 1) const SizedBox(height: 10),
          ],
        ],
      );
    }

    return Row(
      children: [
        for (var index = 0; index < cards.length; index++) ...[
          Expanded(child: cards[index]),
          if (index != cards.length - 1) const SizedBox(width: 10),
        ],
      ],
    );
  }
}

class _CreateGroupButton extends StatelessWidget {
  final bool compact;
  final VoidCallback onPressed;

  const _CreateGroupButton({
    required this.compact,
    required this.onPressed,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      height: compact ? 96 : 48,
      child: FilledButton.icon(
        onPressed: onPressed,
        icon: const Icon(Icons.group_add_rounded, size: 18),
        label: Text(
          compact ? 'Создать\nгруппу' : 'Создать группу',
          textAlign: TextAlign.center,
        ),
        style: FilledButton.styleFrom(
          padding: EdgeInsets.symmetric(
            horizontal: compact ? 10 : 18,
            vertical: compact ? 12 : 0,
          ),
          minimumSize: Size.fromHeight(compact ? 96 : 48),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(compact ? 18 : 18),
          ),
        ),
      ),
    );
  }
}

class _StatsTabCard extends StatelessWidget {
  final String title;
  final int count;
  final bool selected;
  final VoidCallback onTap;

  const _StatsTabCard({
    required this.title,
    required this.count,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(18),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(18),
        child: Ink(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 12),
          decoration: BoxDecoration(
            color: selected
                ? AppColors.accentSecondary.withValues(alpha: 0.42)
                : AppColors.accentSoft.withValues(alpha: 0.16),
            borderRadius: BorderRadius.circular(18),
            border: Border.all(
              color: selected
                  ? AppColors.accentSoft.withValues(alpha: 0.48)
                  : AppColors.accentSoft.withValues(alpha: 0.20),
            ),
          ),
          child: Column(
            children: [
              Text(
                '$count',
                style: const TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 18,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                title,
                textAlign: TextAlign.center,
                style: TextStyle(
                  color: selected
                      ? AppColors.textPrimary
                      : AppColors.textSecondary.withValues(alpha: 0.92),
                  fontSize: 12,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _GroupRow extends StatelessWidget {
  final GroupSummary group;

  const _GroupRow({
    required this.group,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: group.isActiveGroup
          ? InMomentSurfaceTone.elevated
          : InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(18),
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          CircleAvatar(
            radius: 18,
            backgroundColor: AppColors.accent.withValues(alpha: 0.25),
            backgroundImage:
                group.avatarUrl != null && group.avatarUrl!.trim().isNotEmpty
                    ? NetworkImage(group.avatarUrl!)
                    : null,
            child: group.avatarUrl == null || group.avatarUrl!.trim().isEmpty
                ? Text(
                    group.name.isNotEmpty ? group.name[0].toUpperCase() : 'G',
                    style: const TextStyle(color: AppColors.textPrimary),
                  )
                : null,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              group.name,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 14,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          if (group.isOwner) const _MiniTag(text: 'моя'),
          if (group.isOwner && group.isActiveGroup) const SizedBox(width: 6),
          if (group.isActiveGroup)
            const _MiniTag(
              text: 'активная',
              highlighted: true,
            ),
        ],
      ),
    );
  }
}

class _InvitationRow extends StatelessWidget {
  final PendingInvitationPreview invitation;
  final bool accepting;
  final VoidCallback onAccept;

  const _InvitationRow({
    required this.invitation,
    required this.accepting,
    required this.onAccept,
  });

  @override
  Widget build(BuildContext context) {
    final invitedBy = (invitation.invitedByUserName ?? '').trim();

    return InMomentSurface(
      tone: InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(18),
      padding: const EdgeInsets.all(12),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  invitation.groupName,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 14,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  invitedBy.isEmpty ? 'Приглашение в группу' : 'от @$invitedBy',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 10),
          FilledButton(
            onPressed: accepting ? null : onAccept,
            child: accepting
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Принять'),
          ),
        ],
      ),
    );
  }
}

class _MiniTag extends StatelessWidget {
  final String text;
  final bool highlighted;

  const _MiniTag({
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
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      child: Text(
        text,
        style: const TextStyle(
          color: AppColors.textPrimary,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}

class _EmptyStateText extends StatelessWidget {
  final String text;

  const _EmptyStateText({
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 18),
      child: Center(
        child: Text(
          text,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 13,
          ),
        ),
      ),
    );
  }
}

class _SimpleSection extends StatelessWidget {
  final List<Widget> children;
  final bool danger;

  const _SimpleSection({
    required this.children,
    this.danger = false,
  });

  @override
  Widget build(BuildContext context) {
    return InMomentSurface(
      tone: danger
          ? InMomentSurfaceTone.danger
          : InMomentSurfaceTone.base,
      borderRadius: BorderRadius.circular(22),
      padding: const EdgeInsets.all(8),
      child: Column(
        children: children,
      ),
    );
  }
}

class _SimpleMenuTile extends StatelessWidget {
  final String title;
  final bool danger;
  final VoidCallback? onTap;

  const _SimpleMenuTile({
    required this.title,
    this.danger = false,
    this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: InMomentActionTile(
        title: title,
        onTap: onTap,
        danger: danger,
        compact: true,
      ),
    );
  }
}

class _ProfileTabBottomSheet extends StatelessWidget {
  final Widget child;

  const _ProfileTabBottomSheet({
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final bottomInset = media.viewInsets.bottom;
    final height = media.size.height;

    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.fromLTRB(12, 0, 12, 12 + bottomInset),
        child: DraggableScrollableSheet(
          expand: false,
          initialChildSize: height < 720 ? 0.72 : 0.62,
          minChildSize: 0.32,
          maxChildSize: 0.9,
          builder: (context, scrollController) {
            return InMomentSurface(
              tone: InMomentSurfaceTone.elevated,
              borderRadius: BorderRadius.circular(28),
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  const SizedBox(height: 10),
                  Container(
                    width: 42,
                    height: 4,
                    decoration: BoxDecoration(
                      color: AppColors.textSecondary.withValues(alpha: 0.35),
                      borderRadius: BorderRadius.circular(999),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Expanded(
                    child: PrimaryScrollController(
                      controller: scrollController,
                      child: child,
                    ),
                  ),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class _ProfileTabSheetContent extends StatelessWidget {
  final String title;
  final String emptyText;
  final List<Widget> children;

  const _ProfileTabSheetContent({
    required this.title,
    required this.emptyText,
    required this.children,
  });

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      padding: const EdgeInsets.fromLTRB(18, 6, 18, 18),
      itemCount: children.isEmpty ? 2 : children.length + 1,
      separatorBuilder: (_, index) {
        if (index == 0 || children.isEmpty) {
          return const SizedBox(height: 14);
        }

        return const SizedBox(height: 10);
      },
      itemBuilder: (context, index) {
        if (index == 0) {
          return Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 18,
              fontWeight: FontWeight.w800,
            ),
          );
        }

        if (children.isEmpty) {
          return _EmptyStateText(text: emptyText);
        }

        return children[index - 1];
      },
    );
  }
}

class _LargeBottomSheet extends StatelessWidget {
  final Widget child;

  const _LargeBottomSheet({
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.of(context).viewInsets.bottom;

    return SafeArea(
      top: false,
      child: Padding(
        padding: EdgeInsets.fromLTRB(12, 0, 12, 10 + bottomInset),
        child: InMomentSurface(
          tone: InMomentSurfaceTone.overlay,
          borderRadius: BorderRadius.circular(28),
          padding: const EdgeInsets.fromLTRB(0, 8, 0, 0),
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
              const SizedBox(height: 8),
              Flexible(
                child: child,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _InfoSheet extends StatelessWidget {
  final String title;
  final String text;

  const _InfoSheet({
    required this.title,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    final steps = <({IconData icon, String title, String text})>[
      (
        icon: Icons.groups_2_rounded,
        title: '1. Выберите активную группу',
        text: 'Именно из неё будет браться последняя публикация для главного экрана и виджета.',
      ),
      (
        icon: Icons.widgets_rounded,
        title: '2. Добавьте виджет InMoment',
        text: 'На рабочем столе Android удерживайте пустое место, откройте «Виджеты» и выберите InMoment.',
      ),
      (
        icon: Icons.sync_rounded,
        title: '3. Обновление',
        text: 'Если фото не появилось сразу, откройте приложение и обновите профиль — снимок синхронизируется повторно.',
      ),
    ];

    return Padding(
      padding: const EdgeInsets.fromLTRB(18, 8, 18, 18),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              title,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 20,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              text,
              style: const TextStyle(
                color: AppColors.textSecondary,
                height: 1.42,
                fontSize: 13.5,
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 16),
            ...steps.map(
              (step) => Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: InMomentSurface(
                  tone: InMomentSurfaceTone.overlay,
                  borderRadius: BorderRadius.circular(18),
                  padding: const EdgeInsets.all(14),
                  showGlow: false,
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                        step.icon,
                        color: AppColors.accentSoft,
                        size: 22,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              step.title,
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                fontSize: 13.5,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              step.text,
                              style: const TextStyle(
                                color: AppColors.textSecondary,
                                height: 1.35,
                                fontSize: 12.5,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 8),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text('Понятно'),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileEditSheetScaffold extends StatelessWidget {
  final String title;
  final String? subtitle;
  final Widget child;

  const _ProfileEditSheetScaffold({
    required this.title,
    this.subtitle,
    required this.child,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 18,
              fontWeight: FontWeight.w800,
            ),
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 6),
            Text(
              subtitle!,
              style: const TextStyle(
                color: AppColors.textSecondary,
                fontSize: 13,
                height: 1.4,
              ),
            ),
          ],
          const SizedBox(height: 14),
          child,
        ],
      ),
    );
  }
}

class _EditNameSheet extends StatefulWidget {
  final UserProfile profile;
  final ProfileApi api;

  const _EditNameSheet({
    required this.profile,
    required this.api,
  });

  @override
  State<_EditNameSheet> createState() => _EditNameSheetState();
}

class _EditNameSheetState extends State<_EditNameSheet> {
  late final TextEditingController _firstNameController;
  late final TextEditingController _lastNameController;

  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _firstNameController = TextEditingController(text: widget.profile.firstName);
    _lastNameController = TextEditingController(text: widget.profile.lastName);
  }

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_saving) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final updated = await widget.api.updateProfile(
        firstName: _firstNameController.text.trim(),
        lastName: _lastNameController.text.trim(),
        userName: widget.profile.userName,
        phoneNumber: widget.profile.phoneNumber,
      );

      if (!mounted) return;
      Navigator.of(context).pop(updated);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль. Попробуйте ещё раз.',
        );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      shrinkWrap: true,
      padding: EdgeInsets.zero,
      children: [
        _ProfileEditSheetScaffold(
          title: 'Имя',
          subtitle: 'Обновите имя и фамилию профиля.',
          child: Column(
            children: [
              TextField(
                controller: _firstNameController,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(labelText: 'Имя'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _lastNameController,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(labelText: 'Фамилия'),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _error!,
                    style: const TextStyle(
                      color: Colors.redAccent,
                      fontSize: 13,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _saving ? null : _save,
                  child: _saving
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Сохранить'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _EditUserNameSheet extends StatefulWidget {
  final UserProfile profile;
  final ProfileApi api;

  const _EditUserNameSheet({
    required this.profile,
    required this.api,
  });

  @override
  State<_EditUserNameSheet> createState() => _EditUserNameSheetState();
}

class _EditUserNameSheetState extends State<_EditUserNameSheet> {
  late final TextEditingController _userNameController;

  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _userNameController = TextEditingController(text: widget.profile.userName);
  }

  @override
  void dispose() {
    _userNameController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_saving) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final updated = await widget.api.updateProfile(
        firstName: widget.profile.firstName,
        lastName: widget.profile.lastName,
        userName: _userNameController.text.trim(),
        phoneNumber: widget.profile.phoneNumber,
      );

      if (!mounted) return;
      Navigator.of(context).pop(updated);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль. Попробуйте ещё раз.',
        );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      shrinkWrap: true,
      padding: EdgeInsets.zero,
      children: [
        _ProfileEditSheetScaffold(
          title: 'Ник',
          subtitle: 'Измените отображаемый ник.',
          child: Column(
            children: [
              TextField(
                controller: _userNameController,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(labelText: 'Никнейм'),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _error!,
                    style: const TextStyle(
                      color: Colors.redAccent,
                      fontSize: 13,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _saving ? null : _save,
                  child: _saving
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Сохранить'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _EditPhoneSheet extends StatefulWidget {
  final UserProfile profile;
  final ProfileApi api;

  const _EditPhoneSheet({
    required this.profile,
    required this.api,
  });

  @override
  State<_EditPhoneSheet> createState() => _EditPhoneSheetState();
}

class _EditPhoneSheetState extends State<_EditPhoneSheet> {
  late final TextEditingController _phoneController;

  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _phoneController =
        TextEditingController(text: widget.profile.phoneNumber ?? '');
  }

  @override
  void dispose() {
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_saving) return;

    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final updated = await widget.api.updateProfile(
        firstName: widget.profile.firstName,
        lastName: widget.profile.lastName,
        userName: widget.profile.userName,
        phoneNumber: _phoneController.text.trim(),
      );

      if (!mounted) return;
      Navigator.of(context).pop(updated);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль. Попробуйте ещё раз.',
        );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      shrinkWrap: true,
      padding: EdgeInsets.zero,
      children: [
        _ProfileEditSheetScaffold(
          title: 'Телефон',
          subtitle: 'Обновите номер телефона профиля.',
          child: Column(
            children: [
              TextField(
                controller: _phoneController,
                keyboardType: TextInputType.phone,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(labelText: 'Номер телефона'),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _error!,
                    style: const TextStyle(
                      color: Colors.redAccent,
                      fontSize: 13,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: _saving ? null : _save,
                  child: _saving
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Сохранить'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _EditEmailSheet extends StatelessWidget {
  final UserProfile profile;

  const _EditEmailSheet({
    required this.profile,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      shrinkWrap: true,
      padding: EdgeInsets.zero,
      children: [
        _ProfileEditSheetScaffold(
          title: 'Почта',
          subtitle: 'Сейчас здесь только просмотр текущей почты.',
          child: Column(
            children: [
              TextField(
                controller: TextEditingController(text: profile.email),
                enabled: false,
                style: const TextStyle(color: AppColors.textPrimary),
                decoration: const InputDecoration(labelText: 'Текущая почта'),
              ),
              const SizedBox(height: 12),
              const Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Смена почты пока не подключена в текущем API профиля.',
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.45,
                    fontSize: 13,
                  ),
                ),
              ),
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () => Navigator.of(context).pop(),
                  child: const Text('Закрыть'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _EditAvatarSheet extends StatefulWidget {
  final UserProfile profile;
  final ValueChanged<UserProfile> onUploaded;

  const _EditAvatarSheet({
    required this.profile,
    required this.onUploaded,
  });

  @override
  State<_EditAvatarSheet> createState() => _EditAvatarSheetState();
}

class _EditAvatarSheetState extends State<_EditAvatarSheet> {
  final Dio _publicDio = Dio();
  final ProfileApi _profileApi = ProfileApi();

  bool _uploading = false;
  String? _error;

  Future<void> _pickAndUpload() async {
    if (_uploading) return;

    setState(() {
      _uploading = true;
      _error = null;
    });

    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.image,
        allowMultiple: false,
        withData: true,
      );

      if (result == null || result.files.isEmpty) {
        if (!mounted) return;
        setState(() {
          _uploading = false;
        });
        return;
      }

      final file = result.files.single;
      final bytes = file.bytes;

      if (bytes == null || bytes.isEmpty) {
        throw Exception('Не удалось прочитать файл.');
      }

      final contentType = _resolveContentType(file.name);
      final api = ApiClient.create().dio;

      final presignResponse = await api.post(
        '/api/uploads/profile-photo/presign',
        data: {
          'contentType': contentType,
        },
      );

      final data = presignResponse.data;
      if (data is! Map<String, dynamic>) {
        throw Exception('Некорректный ответ сервера.');
      }

      final uploadUrl = (data['uploadUrl'] ?? '').toString();
      final fileUrl = (data['fileUrl'] ?? '').toString();

      if (uploadUrl.trim().isEmpty || fileUrl.trim().isEmpty) {
        throw Exception('Не удалось подготовить загрузку аватара.');
      }

      await _publicDio.put(
        uploadUrl,
        data: Stream.fromIterable([bytes]),
        options: Options(
          headers: {
            'Content-Type': contentType,
            'Content-Length': bytes.length,
          },
        ),
      );

      await api.post(
        '/api/users/me/profile-photo',
        data: {
          'url': fileUrl,
        },
      );

      final updated = await _profileApi.getMe();

      if (!mounted) return;
      widget.onUploaded(updated);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _uploading = false;
        _error = ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить профиль. Попробуйте ещё раз.',
        );
      });
    }
  }

  String _resolveContentType(String fileName) {
    final lower = fileName.toLowerCase();

    if (lower.endsWith('.png')) return 'image/png';
    if (lower.endsWith('.webp')) return 'image/webp';
    if (lower.endsWith('.heic')) return 'image/heic';
    if (lower.endsWith('.heif')) return 'image/heif';

    return 'image/jpeg';
  }

  @override
  Widget build(BuildContext context) {
    return ListView(
      shrinkWrap: true,
      padding: EdgeInsets.zero,
      children: [
        _ProfileEditSheetScaffold(
          title: 'Аватар',
          subtitle: 'Обновите фотографию профиля.',
          child: Column(
            children: [
              Center(
                child: CircleAvatar(
                  radius: 46,
                  backgroundColor: AppColors.accent.withValues(alpha: 0.28),
                  backgroundImage: widget.profile.profilePhotoUrl != null &&
                          widget.profile.profilePhotoUrl!.trim().isNotEmpty
                      ? NetworkImage(widget.profile.profilePhotoUrl!)
                      : null,
                  child: widget.profile.profilePhotoUrl == null ||
                          widget.profile.profilePhotoUrl!.trim().isEmpty
                      ? Text(
                          widget.profile.userName.isNotEmpty
                              ? widget.profile.userName[0].toUpperCase()
                              : 'U',
                          style: const TextStyle(
                            color: AppColors.textPrimary,
                            fontWeight: FontWeight.w800,
                          ),
                        )
                      : null,
                ),
              ),
              if (_error != null) ...[
                const SizedBox(height: 12),
                Text(
                  _error!,
                  style: const TextStyle(
                    color: Colors.redAccent,
                    fontSize: 13,
                  ),
                  textAlign: TextAlign.center,
                ),
              ],
              const SizedBox(height: 14),
              SizedBox(
                width: double.infinity,
                child: FilledButton.icon(
                  onPressed: _uploading ? null : _pickAndUpload,
                  icon: _uploading
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.photo_camera_back_outlined),
                  label: Text(_uploading ? 'Загружаем...' : 'Выбрать фото'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}