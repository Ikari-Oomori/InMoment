import 'package:flutter/foundation.dart';

import '../../../core/api/api_error.dart';
import '../../profile/api/profile_api.dart';
import '../../profile/models/user_profile.dart';
import '../../widget/services/widget_sync_service.dart';
import '../api/groups_api.dart';
import '../models/group.dart';

class ActiveGroupController extends ChangeNotifier {
  ActiveGroupController._();

  static final ActiveGroupController instance = ActiveGroupController._();

  final GroupsApi _groupsApi = GroupsApi();
  final ProfileApi _profileApi = ProfileApi();

  bool _loading = false;
  bool _saving = false;
  bool _loaded = false;
  String? _error;

  List<Group> _groups = const [];
  Group? _activeGroup;
  String? _persistedActiveGroupId;

  bool get loading => _loading;
  bool get saving => _saving;
  bool get loaded => _loaded;
  String? get error => _error;

  List<Group> get groups => List.unmodifiable(_groups);
  Group? get activeGroup => _activeGroup;

  bool get hasGroups => _groups.isNotEmpty;

  List<Group> get ownedGroups =>
      _groups.where((group) => group.isOwner).toList(growable: false);

  List<Group> get manageableGroups =>
      _groups.where((group) => group.isManager).toList(growable: false);

  bool get hasOwnedGroups => ownedGroups.isNotEmpty;
  bool get hasManageableGroups => manageableGroups.isNotEmpty;

  Group? get invitationGroup {
    final active = _activeGroup;
    if (active != null && active.isManager) {
      return active;
    }

    final manageable = manageableGroups;
    if (manageable.isNotEmpty) {
      return manageable.first;
    }

    return null;
  }

  Future<void> load({bool force = false}) async {
    if (_loading) return;
    if (_loaded && !force) return;

    _loading = true;
    _error = null;
    notifyListeners();

    try {
      final profile = await _profileApi.getMe();
      final groups = await _groupsApi.getMyGroups();

      _applyProfileAndGroups(
        profile: profile,
        groups: groups,
      );

      await _persistImplicitActiveGroupIfNeeded();

      _loaded = true;
      _error = null;
      await WidgetSyncService.instance.syncFromBackend();
    } catch (e) {
      _error = _normalizeError(e);
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  Future<void> setActiveGroup(Group group) async {
    if (_saving) return;

    final alreadySelected = _activeGroup?.id == group.id;
    final alreadyPersisted = _persistedActiveGroupId == group.id;
    if (alreadySelected && alreadyPersisted) return;

    _saving = true;
    _error = null;
    notifyListeners();

    try {
      await _profileApi.updateActiveGroup(group.id);

      _groups = _groups
          .map(
            (item) => item.copyWith(
              isActiveGroup: item.id == group.id,
            ),
          )
          .toList();

      _activeGroup = _groups.firstWhere(
        (item) => item.id == group.id,
        orElse: () => group,
      );
      _persistedActiveGroupId = group.id;

      _error = null;
      await WidgetSyncService.instance.syncFromBackend();
    } catch (e) {
      _error = _normalizeError(e);
      rethrow;
    } finally {
      _saving = false;
      notifyListeners();
    }
  }

  void syncFromProfile(UserProfile profile) {
    _persistedActiveGroupId = profile.activeGroupId;

    if (_groups.isEmpty) {
      _activeGroup = null;
      notifyListeners();
      return;
    }

    final normalizedGroups = _normalizeOwnership(
      profile: profile,
      groups: _groups,
    );

    Group? resolvedActive;

    if (profile.activeGroupId != null) {
      for (final group in normalizedGroups) {
        if (group.id == profile.activeGroupId) {
          resolvedActive = group;
          break;
        }
      }
    }

    resolvedActive ??= normalizedGroups.cast<Group?>().firstWhere(
          (group) => group?.isActiveGroup == true,
          orElse: () => null,
        );

    if (resolvedActive == null && normalizedGroups.isNotEmpty) {
      resolvedActive = normalizedGroups.first;
    }

    _groups = normalizedGroups
        .map(
          (item) => item.copyWith(
            isActiveGroup:
                resolvedActive != null && item.id == resolvedActive.id,
          ),
        )
        .toList();

    _activeGroup = resolvedActive == null
        ? null
        : _groups.firstWhere(
            (item) => item.id == resolvedActive!.id,
            orElse: () => resolvedActive!,
          );

    notifyListeners();
  }

  void reset() {
    _loading = false;
    _saving = false;
    _loaded = false;
    _error = null;
    _groups = const [];
    _activeGroup = null;
    _persistedActiveGroupId = null;
    notifyListeners();
  }

  void _applyProfileAndGroups({
    required UserProfile profile,
    required List<Group> groups,
  }) {
    _persistedActiveGroupId = profile.activeGroupId;

    final normalizedGroups = _normalizeOwnership(
      profile: profile,
      groups: groups,
    );

    Group? resolvedActive;

    if (profile.activeGroupId != null) {
      for (final group in normalizedGroups) {
        if (group.id == profile.activeGroupId) {
          resolvedActive = group;
          break;
        }
      }
    }

    resolvedActive ??= normalizedGroups.cast<Group?>().firstWhere(
          (group) => group?.isActiveGroup == true,
          orElse: () => null,
        );

    if (resolvedActive == null && normalizedGroups.isNotEmpty) {
      resolvedActive = normalizedGroups.first;
    }

    _groups = normalizedGroups
        .map(
          (group) => group.copyWith(
            isActiveGroup:
                resolvedActive != null && group.id == resolvedActive.id,
          ),
        )
        .toList();

    _activeGroup = resolvedActive == null
        ? null
        : _groups.firstWhere(
            (group) => group.id == resolvedActive!.id,
            orElse: () => resolvedActive!,
          );
  }

  Future<void> _persistImplicitActiveGroupIfNeeded() async {
    final active = _activeGroup;
    if (active == null) return;
    if (_persistedActiveGroupId == active.id) return;

    await _profileApi.updateActiveGroup(active.id);
    _persistedActiveGroupId = active.id;
  }

  List<Group> _normalizeOwnership({
    required UserProfile profile,
    required List<Group> groups,
  }) {
    return groups
        .map(
          (group) => group.copyWith(
            isOwner: group.isOwner || group.ownerId == profile.id,
          ),
        )
        .toList(growable: false);
  }

  String _normalizeError(Object error) {
    return ApiError.normalize(
      error,
      fallback: 'Не удалось загрузить группы. Попробуйте ещё раз.',
    );
  }
}