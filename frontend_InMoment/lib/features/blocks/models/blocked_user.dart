class BlockedUser {
  final String userId;
  final String userName;
  final String firstName;
  final String lastName;
  final String? profilePhotoUrl;
  final DateTime? blockedAtUtc;

  const BlockedUser({
    required this.userId,
    required this.userName,
    required this.firstName,
    required this.lastName,
    this.profilePhotoUrl,
    this.blockedAtUtc,
  });

  factory BlockedUser.fromJson(Map<String, dynamic> json) {
    return BlockedUser(
      userId: (json['userId'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      firstName: (json['firstName'] ?? '').toString(),
      lastName: (json['lastName'] ?? '').toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      blockedAtUtc: json['blockedAtUtc'] == null
          ? null
          : DateTime.tryParse(json['blockedAtUtc'].toString()),
    );
  }

  String get fullName {
    final value = '$firstName $lastName'.trim();
    return value.isEmpty ? userName : value;
  }
}