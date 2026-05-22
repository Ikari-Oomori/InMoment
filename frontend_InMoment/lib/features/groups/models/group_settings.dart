class GroupSettings {
  final String id;
  final String name;
  final String? description;
  final String? avatarUrl;
  final String ownerId;
  final DateTime? createdAt;

  const GroupSettings({
    required this.id,
    required this.name,
    required this.ownerId,
    this.description,
    this.avatarUrl,
    this.createdAt,
  });

  factory GroupSettings.fromJson(Map<String, dynamic> json) {
    return GroupSettings(
      id: (json['id'] ?? '').toString(),
      name: (json['name'] ?? 'Без названия').toString(),
      description: json['description']?.toString(),
      avatarUrl: json['avatarUrl']?.toString(),
      ownerId: (json['ownerId'] ?? '').toString(),
      createdAt: json['createdAt'] == null
          ? null
          : DateTime.tryParse(json['createdAt'].toString()),
    );
  }

  GroupSettings copyWith({
    String? id,
    String? name,
    String? description,
    String? avatarUrl,
    String? ownerId,
    DateTime? createdAt,
  }) {
    return GroupSettings(
      id: id ?? this.id,
      name: name ?? this.name,
      description: description ?? this.description,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      ownerId: ownerId ?? this.ownerId,
      createdAt: createdAt ?? this.createdAt,
    );
  }
}