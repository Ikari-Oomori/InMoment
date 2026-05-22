import '../../groups/models/group_summary.dart';

class PendingInvitationPreview {
  final String invitationId;
  final String groupId;
  final String groupName;
  final String? groupAvatarUrl;
  final String? invitedByUserId;
  final String? invitedByUserName;
  final String? invitedByUserProfilePhotoUrl;
  final DateTime? createdAt;

  const PendingInvitationPreview({
    required this.invitationId,
    required this.groupId,
    required this.groupName,
    this.groupAvatarUrl,
    this.invitedByUserId,
    this.invitedByUserName,
    this.invitedByUserProfilePhotoUrl,
    this.createdAt,
  });

  factory PendingInvitationPreview.fromJson(Map<String, dynamic> json) {
    return PendingInvitationPreview(
      invitationId: (json['invitationId'] ?? '').toString(),
      groupId: (json['groupId'] ?? '').toString(),
      groupName: (json['groupName'] ?? 'Без названия').toString(),
      groupAvatarUrl: json['groupAvatarUrl']?.toString(),
      invitedByUserId: json['invitedByUserId']?.toString(),
      invitedByUserName: json['invitedByUserName']?.toString(),
      invitedByUserProfilePhotoUrl:
          json['invitedByUserProfilePhotoUrl']?.toString(),
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.tryParse(json['createdAt'].toString()),
    );
  }
}

class UserProfile {
  final String id;
  final String email;
  final String userName;
  final String firstName;
  final String lastName;
  final String? phoneNumber;
  final String? profilePhotoUrl;
  final String? activeGroupId;
  final DateTime? createdAt;
  final bool isSystemModerator;
  final int groupsCount;
  final int pendingInvitationsCount;
  final List<GroupSummary> groups;
  final List<PendingInvitationPreview> pendingInvitations;

  const UserProfile({
    required this.id,
    required this.email,
    required this.userName,
    required this.firstName,
    required this.lastName,
    this.phoneNumber,
    this.profilePhotoUrl,
    this.activeGroupId,
    this.createdAt,
    this.isSystemModerator = false,
    this.groupsCount = 0,
    this.pendingInvitationsCount = 0,
    this.groups = const [],
    this.pendingInvitations = const [],
  });

  String get fullName {
    final value = '$firstName $lastName'.trim();
    return value.isEmpty ? userName : value;
  }

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    final groupsRaw = json['groups'];
    final invitationsRaw = json['pendingInvitations'];

    return UserProfile(
      id: (json['id'] ?? '').toString(),
      email: (json['email'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      firstName: (json['firstName'] ?? '').toString(),
      lastName: (json['lastName'] ?? '').toString(),
      phoneNumber: json['phoneNumber']?.toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      activeGroupId: json['activeGroupId']?.toString(),
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.tryParse(json['createdAt'].toString()),
      isSystemModerator: json['isSystemModerator'] as bool? ?? false,
      groupsCount: (json['groupsCount'] as num?)?.toInt() ?? 0,
      pendingInvitationsCount:
          (json['pendingInvitationsCount'] as num?)?.toInt() ?? 0,
      groups: groupsRaw is List
          ? groupsRaw
              .whereType<Map<String, dynamic>>()
              .map(GroupSummary.fromJson)
              .toList()
          : const [],
      pendingInvitations: invitationsRaw is List
          ? invitationsRaw
              .whereType<Map<String, dynamic>>()
              .map(PendingInvitationPreview.fromJson)
              .toList()
          : const [],
    );
  }

  UserProfile copyWith({
    String? id,
    String? email,
    String? userName,
    String? firstName,
    String? lastName,
    String? phoneNumber,
    String? profilePhotoUrl,
    String? activeGroupId,
    DateTime? createdAt,
    bool? isSystemModerator,
    int? groupsCount,
    int? pendingInvitationsCount,
    List<GroupSummary>? groups,
    List<PendingInvitationPreview>? pendingInvitations,
  }) {
    return UserProfile(
      id: id ?? this.id,
      email: email ?? this.email,
      userName: userName ?? this.userName,
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      phoneNumber: phoneNumber ?? this.phoneNumber,
      profilePhotoUrl: profilePhotoUrl ?? this.profilePhotoUrl,
      activeGroupId: activeGroupId ?? this.activeGroupId,
      createdAt: createdAt ?? this.createdAt,
      isSystemModerator: isSystemModerator ?? this.isSystemModerator,
      groupsCount: groupsCount ?? this.groupsCount,
      pendingInvitationsCount:
          pendingInvitationsCount ?? this.pendingInvitationsCount,
      groups: groups ?? this.groups,
      pendingInvitations: pendingInvitations ?? this.pendingInvitations,
    );
  }
}