class NotificationSettingsModel {
  final bool pushEnabled;
  final bool pushGroupInvitations;
  final bool pushReactions;
  final bool pushComments;
  final bool pushReplies;
  final bool pushMentions;
  final bool pushPosts;
  final bool pushRetention;
  final bool pushProductUpdates;
  final DateTime? createdAtUtc;
  final DateTime? updatedAtUtc;

  const NotificationSettingsModel({
    required this.pushEnabled,
    required this.pushGroupInvitations,
    required this.pushReactions,
    required this.pushComments,
    required this.pushReplies,
    required this.pushMentions,
    required this.pushPosts,
    required this.pushRetention,
    required this.pushProductUpdates,
    this.createdAtUtc,
    this.updatedAtUtc,
  });

  factory NotificationSettingsModel.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(dynamic value) {
      if (value == null) return null;
      return DateTime.tryParse(value.toString());
    }

    return NotificationSettingsModel(
      pushEnabled: json['pushEnabled'] as bool? ?? true,
      pushGroupInvitations: json['pushGroupInvitations'] as bool? ?? true,
      pushReactions: json['pushReactions'] as bool? ?? true,
      pushComments: json['pushComments'] as bool? ?? true,
      pushReplies: json['pushReplies'] as bool? ?? true,
      pushMentions: json['pushMentions'] as bool? ?? true,
      pushPosts: json['pushPosts'] as bool? ?? true,
      pushRetention: json['pushRetention'] as bool? ?? true,
      pushProductUpdates: json['pushProductUpdates'] as bool? ?? true,
      createdAtUtc: parseDate(json['createdAtUtc']),
      updatedAtUtc: parseDate(json['updatedAtUtc']),
    );
  }

  NotificationSettingsModel copyWith({
    bool? pushEnabled,
    bool? pushGroupInvitations,
    bool? pushReactions,
    bool? pushComments,
    bool? pushReplies,
    bool? pushMentions,
    bool? pushPosts,
    bool? pushRetention,
    bool? pushProductUpdates,
    DateTime? createdAtUtc,
    DateTime? updatedAtUtc,
  }) {
    return NotificationSettingsModel(
      pushEnabled: pushEnabled ?? this.pushEnabled,
      pushGroupInvitations:
          pushGroupInvitations ?? this.pushGroupInvitations,
      pushReactions: pushReactions ?? this.pushReactions,
      pushComments: pushComments ?? this.pushComments,
      pushReplies: pushReplies ?? this.pushReplies,
      pushMentions: pushMentions ?? this.pushMentions,
      pushPosts: pushPosts ?? this.pushPosts,
      pushRetention: pushRetention ?? this.pushRetention,
      pushProductUpdates: pushProductUpdates ?? this.pushProductUpdates,
      createdAtUtc: createdAtUtc ?? this.createdAtUtc,
      updatedAtUtc: updatedAtUtc ?? this.updatedAtUtc,
    );
  }
}