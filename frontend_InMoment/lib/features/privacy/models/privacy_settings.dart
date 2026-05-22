enum PrivacyAudienceValue {
  everyone(1, 'Все'),
  friendsOnly(2, 'Только знакомые'),
  nobody(3, 'Никто');

  final int code;
  final String label;

  const PrivacyAudienceValue(this.code, this.label);

  static PrivacyAudienceValue fromCode(dynamic value) {
    final intCode = value is num ? value.toInt() : int.tryParse('$value') ?? 1;

    for (final item in PrivacyAudienceValue.values) {
      if (item.code == intCode) return item;
    }

    return PrivacyAudienceValue.everyone;
  }
}

class PrivacySettingsModel {
  final PrivacyAudienceValue allowFriendRequestsFrom;
  final PrivacyAudienceValue allowGroupInvitesFrom;
  final bool discoverableByContacts;
  final bool discoverableBySearch;

  const PrivacySettingsModel({
    required this.allowFriendRequestsFrom,
    required this.allowGroupInvitesFrom,
    required this.discoverableByContacts,
    required this.discoverableBySearch,
  });

  factory PrivacySettingsModel.fromJson(Map<String, dynamic> json) {
    return PrivacySettingsModel(
      allowFriendRequestsFrom: PrivacyAudienceValue.fromCode(
        json['allowFriendRequestsFrom'],
      ),
      allowGroupInvitesFrom: PrivacyAudienceValue.fromCode(
        json['allowGroupInvitesFrom'],
      ),
      discoverableByContacts: json['discoverableByContacts'] as bool? ?? true,
      discoverableBySearch: json['discoverableBySearch'] as bool? ?? true,
    );
  }

  PrivacySettingsModel copyWith({
    PrivacyAudienceValue? allowFriendRequestsFrom,
    PrivacyAudienceValue? allowGroupInvitesFrom,
    bool? discoverableByContacts,
    bool? discoverableBySearch,
  }) {
    return PrivacySettingsModel(
      allowFriendRequestsFrom:
          allowFriendRequestsFrom ?? this.allowFriendRequestsFrom,
      allowGroupInvitesFrom:
          allowGroupInvitesFrom ?? this.allowGroupInvitesFrom,
      discoverableByContacts:
          discoverableByContacts ?? this.discoverableByContacts,
      discoverableBySearch:
          discoverableBySearch ?? this.discoverableBySearch,
    );
  }
}