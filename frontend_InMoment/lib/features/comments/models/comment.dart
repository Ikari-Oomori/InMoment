/*class Comment {
  final String id;
  final String text;
  final DateTime createdAt;

  final String userName;
  final String? userAvatarUrl;

  Comment({
    required this.id,
    required this.text,
    required this.createdAt,
    required this.userName,
    this.userAvatarUrl,
  });

  factory Comment.fromJson(Map<String, dynamic> json) {
    return Comment(
      id: json['id'],
      text: json['text'] ?? '',
      createdAt: DateTime.parse(json['createdAt']),
      userName: json['userName'] ?? 'user',
      userAvatarUrl: json['userAvatarUrl'],
    );
  }
}*/