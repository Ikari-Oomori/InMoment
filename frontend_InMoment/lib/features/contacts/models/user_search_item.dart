class UserSearchItem {
  final String id;
  final String userName;
  final String displayName;
  final String? profilePhotoUrl;
  final String? matchedBy;
  final String? matchedValue;

  const UserSearchItem({
    required this.id,
    required this.userName,
    required this.displayName,
    required this.profilePhotoUrl,
    this.matchedBy,
    this.matchedValue,
  });

  factory UserSearchItem.fromJson(Map<String, dynamic> json) {
    String readString(String key) {
      final value = json[key];
      return value is String ? value : '';
    }

    String? readNullableString(String key) {
      final value = json[key];
      if (value is String && value.trim().isNotEmpty) {
        return value.trim();
      }
      return null;
    }

    return UserSearchItem(
      id: readString('id'),
      userName: readString('userName'),
      displayName: readString('displayName'),
      profilePhotoUrl: readNullableString('profilePhotoUrl'),
      matchedBy: readNullableString('matchedBy'),
      matchedValue: readNullableString('matchedValue'),
    );
  }

  factory UserSearchItem.fromContactMatchJson(Map<String, dynamic> json) {
    final firstName = (json['firstName'] ?? '').toString().trim();
    final lastName = (json['lastName'] ?? '').toString().trim();
    final userName = (json['userName'] ?? '').toString().trim();

    final displayName = '$firstName $lastName'.trim();

    return UserSearchItem(
      id: (json['userId'] ?? '').toString(),
      userName: userName,
      displayName: displayName.isEmpty ? userName : displayName,
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      matchedBy: json['matchedBy']?.toString(),
      matchedValue: json['matchedValue']?.toString(),
    );
  }

  String get title {
    final value = displayName.trim();
    if (value.isNotEmpty) return value;
    return '@$userName';
  }
}