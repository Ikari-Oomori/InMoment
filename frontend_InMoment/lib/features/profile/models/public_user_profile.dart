class PublicUserProfile {
  final String id;
  final String userName;
  final String firstName;
  final String lastName;
  final String? profilePhotoUrl;
  final DateTime createdAt;
  final bool isBlockedByMe;
  final bool hasBlockedMe;
  final bool isActive;
  final bool canBlock;
  final bool canReport;

  const PublicUserProfile({
    required this.id,
    required this.userName,
    required this.firstName,
    required this.lastName,
    required this.profilePhotoUrl,
    required this.createdAt,
    required this.isBlockedByMe,
    required this.hasBlockedMe,
    required this.isActive,
    required this.canBlock,
    required this.canReport,
  });

  factory PublicUserProfile.fromJson(Map<String, dynamic> json) {
    return PublicUserProfile(
      id: (json['id'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      firstName: (json['firstName'] ?? '').toString(),
      lastName: (json['lastName'] ?? '').toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      createdAt: DateTime.parse(
        (json['createdAt'] ?? DateTime.now().toIso8601String()).toString(),
      ),
      isBlockedByMe: json['isBlockedByMe'] as bool? ?? false,
      hasBlockedMe: json['hasBlockedMe'] as bool? ?? false,
      isActive: json['isActive'] as bool? ?? true,
      canBlock: json['canBlock'] as bool? ?? true,
      canReport: json['canReport'] as bool? ?? true,
    );
  }

  String get displayName {
    final joined = '${firstName.trim()} ${lastName.trim()}'.trim();
    if (joined.isNotEmpty) return joined;
    if (userName.trim().isNotEmpty) return '@${userName.trim()}';
    return 'Пользователь';
  }

  PublicUserProfile copyWith({
    bool? isBlockedByMe,
  }) {
    return PublicUserProfile(
      id: id,
      userName: userName,
      firstName: firstName,
      lastName: lastName,
      profilePhotoUrl: profilePhotoUrl,
      createdAt: createdAt,
      isBlockedByMe: isBlockedByMe ?? this.isBlockedByMe,
      hasBlockedMe: hasBlockedMe,
      isActive: isActive,
      canBlock: canBlock,
      canReport: canReport,
    );
  }
}