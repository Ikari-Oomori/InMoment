/*import 'package:shared_preferences/shared_preferences.dart';

class SettingsStorage {
  static const _pushNotificationsKey = 'settings.push_notifications';
  static const _commentNotificationsKey = 'settings.comment_notifications';
  static const _inviteNotificationsKey = 'settings.invite_notifications';

  static const _privateAccountKey = 'settings.private_account';
  static const _hideActivityStatusKey = 'settings.hide_activity_status';
  static const _allowMentionsKey = 'settings.allow_mentions';

  Future<StoredSettings> load() async {
    final prefs = await SharedPreferences.getInstance();

    return StoredSettings(
      pushNotifications: prefs.getBool(_pushNotificationsKey) ?? true,
      commentNotifications: prefs.getBool(_commentNotificationsKey) ?? true,
      inviteNotifications: prefs.getBool(_inviteNotificationsKey) ?? true,
      privateAccount: prefs.getBool(_privateAccountKey) ?? true,
      hideActivityStatus: prefs.getBool(_hideActivityStatusKey) ?? false,
      allowMentions: prefs.getBool(_allowMentionsKey) ?? true,
    );
  }

  Future<void> savePushNotifications(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_pushNotificationsKey, value);
  }

  Future<void> saveCommentNotifications(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_commentNotificationsKey, value);
  }

  Future<void> saveInviteNotifications(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_inviteNotificationsKey, value);
  }

  Future<void> savePrivateAccount(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_privateAccountKey, value);
  }

  Future<void> saveHideActivityStatus(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_hideActivityStatusKey, value);
  }

  Future<void> saveAllowMentions(bool value) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_allowMentionsKey, value);
  }

  Future<void> saveAll(StoredSettings settings) async {
    final prefs = await SharedPreferences.getInstance();

    await prefs.setBool(_pushNotificationsKey, settings.pushNotifications);
    await prefs.setBool(_commentNotificationsKey, settings.commentNotifications);
    await prefs.setBool(_inviteNotificationsKey, settings.inviteNotifications);
    await prefs.setBool(_privateAccountKey, settings.privateAccount);
    await prefs.setBool(_hideActivityStatusKey, settings.hideActivityStatus);
    await prefs.setBool(_allowMentionsKey, settings.allowMentions);
  }
}

class StoredSettings {
  final bool pushNotifications;
  final bool commentNotifications;
  final bool inviteNotifications;
  final bool privateAccount;
  final bool hideActivityStatus;
  final bool allowMentions;

  const StoredSettings({
    required this.pushNotifications,
    required this.commentNotifications,
    required this.inviteNotifications,
    required this.privateAccount,
    required this.hideActivityStatus,
    required this.allowMentions,
  });

  StoredSettings copyWith({
    bool? pushNotifications,
    bool? commentNotifications,
    bool? inviteNotifications,
    bool? privateAccount,
    bool? hideActivityStatus,
    bool? allowMentions,
  }) {
    return StoredSettings(
      pushNotifications: pushNotifications ?? this.pushNotifications,
      commentNotifications: commentNotifications ?? this.commentNotifications,
      inviteNotifications: inviteNotifications ?? this.inviteNotifications,
      privateAccount: privateAccount ?? this.privateAccount,
      hideActivityStatus: hideActivityStatus ?? this.hideActivityStatus,
      allowMentions: allowMentions ?? this.allowMentions,
    );
  }
}*/