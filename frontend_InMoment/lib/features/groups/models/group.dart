class Group {
  final String id;
  final String name;
  final bool isOwner;
  final bool isAdmin;
  final String? ownerId;
  final String? avatarUrl;
  final bool isActiveGroup;
  final int? membersCount;

  const Group({
    required this.id,
    required this.name,
    required this.isOwner,
    this.isAdmin = false,
    this.ownerId,
    this.avatarUrl,
    this.isActiveGroup = false,
    this.membersCount,
  });

  bool get isManager => isOwner || isAdmin;

  factory Group.fromJson(Map<String, dynamic> json) {
    final ownerIdRaw = json['ownerId']?.toString();
    final ownerId = ownerIdRaw != null && ownerIdRaw.trim().isNotEmpty
        ? ownerIdRaw.trim()
        : null;

    return Group(
      id: (json['id'] ?? '').toString(),
      name: (json['name'] ?? 'Без названия').toString(),
      isOwner: json['isOwner'] as bool? ?? false,
      isAdmin: json['isAdmin'] as bool? ?? false,
      ownerId: ownerId,
      avatarUrl: json['avatarUrl']?.toString(),
      isActiveGroup: json['isActiveGroup'] as bool? ?? false,
      membersCount: (json['membersCount'] as num?)?.toInt(),
    );
  }

  Group copyWith({
    String? id,
    String? name,
    bool? isOwner,
    bool? isAdmin,
    String? ownerId,
    String? avatarUrl,
    bool? isActiveGroup,
    int? membersCount,
  }) {
    return Group(
      id: id ?? this.id,
      name: name ?? this.name,
      isOwner: isOwner ?? this.isOwner,
      isAdmin: isAdmin ?? this.isAdmin,
      ownerId: ownerId ?? this.ownerId,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      isActiveGroup: isActiveGroup ?? this.isActiveGroup,
      membersCount: membersCount ?? this.membersCount,
    );
  }
}