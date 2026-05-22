class MentionUser {
  final String id;
  final String userName;
  final String displayName;
  final String? profilePhotoUrl;

  const MentionUser({
    required this.id,
    required this.userName,
    required this.displayName,
    required this.profilePhotoUrl,
  });

  factory MentionUser.fromJson(Map<String, dynamic> json) {
    return MentionUser(
      id: json['id'] as String,
      userName: json['userName'] as String? ?? '',
      displayName: json['displayName'] as String? ?? '',
      profilePhotoUrl: json['profilePhotoUrl'] as String?,
    );
  }
}