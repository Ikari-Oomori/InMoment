class GroupMember {
  final String userId;
  final String userName;
  final String firstName;
  final String lastName;
  final String? profilePhotoUrl;
  final int role;
  final bool isOwner;
  final bool isAdmin;

  const GroupMember({
    required this.userId,
    required this.userName,
    required this.firstName,
    required this.lastName,
    required this.profilePhotoUrl,
    required this.role,
    required this.isOwner,
    required this.isAdmin,
  });

  factory GroupMember.fromJson(Map<String, dynamic> json) {
    final rawRole = json['role'];
    final parsedRole = rawRole is num
        ? rawRole.toInt()
        : int.tryParse(rawRole?.toString() ?? '') ?? 3;

    return GroupMember(
      userId: (json['userId'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      firstName: (json['firstName'] ?? '').toString(),
      lastName: (json['lastName'] ?? '').toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      role: parsedRole,
      isOwner: json['isOwner'] as bool? ?? false,
      isAdmin: json['isAdmin'] as bool? ?? false,
    );
  }

  String get fullName {
    final value = '$firstName $lastName'.trim();
    if (value.isNotEmpty) return value;
    if (userName.trim().isNotEmpty) return userName.trim();
    return 'Без имени';
  }

  String get roleLabel {
    if (isOwner) return 'Владелец';
    if (isAdmin) return 'Админ';
    return 'Участник';
  }
}