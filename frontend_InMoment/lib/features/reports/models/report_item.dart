import 'report_reason_option.dart';

enum ReportStatusOption {
  pending(1, 'Новая'),
  reviewed(2, 'Просмотрена'),
  rejected(3, 'Отклонена'),
  resolved(4, 'Решена');

  final int value;
  final String label;

  const ReportStatusOption(this.value, this.label);

  static ReportStatusOption fromValue(int value) {
    for (final item in ReportStatusOption.values) {
      if (item.value == value) return item;
    }
    return ReportStatusOption.pending;
  }
}

enum ReviewReportActionOption {
  none(0, 'Без действия'),
  deletePhoto(1, 'Удалить фото'),
  deleteComment(2, 'Удалить комментарий'),
  deactivateUser(3, 'Деактивировать пользователя');

  final int value;
  final String label;

  const ReviewReportActionOption(this.value, this.label);
}

class ReporterPreview {
  final String userId;
  final String userName;
  final String displayName;
  final String? profilePhotoUrl;

  const ReporterPreview({
    required this.userId,
    required this.userName,
    required this.displayName,
    required this.profilePhotoUrl,
  });

  factory ReporterPreview.fromJson(Map<String, dynamic> json) {
    return ReporterPreview(
      userId: (json['userId'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
    );
  }
}

class ReportPhotoPreview {
  final String photoId;
  final String groupId;
  final String authorUserId;
  final String authorUserName;
  final String authorDisplayName;
  final String? authorProfilePhotoUrl;
  final String? groupName;
  final String? photoUrl;
  final String? caption;
  final DateTime createdAt;
  final bool isDeleted;

  const ReportPhotoPreview({
    required this.photoId,
    required this.groupId,
    required this.authorUserId,
    required this.authorUserName,
    required this.authorDisplayName,
    required this.authorProfilePhotoUrl,
    required this.groupName,
    required this.photoUrl,
    required this.caption,
    required this.createdAt,
    required this.isDeleted,
  });

  factory ReportPhotoPreview.fromJson(Map<String, dynamic> json) {
    return ReportPhotoPreview(
      photoId: (json['photoId'] ?? '').toString(),
      groupId: (json['groupId'] ?? '').toString(),
      authorUserId: (json['authorUserId'] ?? '').toString(),
      authorUserName: (json['authorUserName'] ?? '').toString(),
      authorDisplayName: (json['authorDisplayName'] ?? '').toString(),
      authorProfilePhotoUrl: json['authorProfilePhotoUrl']?.toString(),
      groupName: json['groupName']?.toString(),
      photoUrl: json['photoUrl']?.toString(),
      caption: json['caption']?.toString(),
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString()) ??
          DateTime.now(),
      isDeleted: json['isDeleted'] as bool? ?? false,
    );
  }
}

class ReportCommentPreview {
  final String commentId;
  final String photoId;
  final String authorUserId;
  final String authorUserName;
  final String authorDisplayName;
  final String? authorProfilePhotoUrl;
  final String text;
  final DateTime createdAt;
  final bool isDeleted;
  final String? parentCommentId;
  final String? parentCommentTextPreview;

  const ReportCommentPreview({
    required this.commentId,
    required this.photoId,
    required this.authorUserId,
    required this.authorUserName,
    required this.authorDisplayName,
    required this.authorProfilePhotoUrl,
    required this.text,
    required this.createdAt,
    required this.isDeleted,
    required this.parentCommentId,
    required this.parentCommentTextPreview,
  });

  factory ReportCommentPreview.fromJson(Map<String, dynamic> json) {
    return ReportCommentPreview(
      commentId: (json['commentId'] ?? '').toString(),
      photoId: (json['photoId'] ?? '').toString(),
      authorUserId: (json['authorUserId'] ?? '').toString(),
      authorUserName: (json['authorUserName'] ?? '').toString(),
      authorDisplayName: (json['authorDisplayName'] ?? '').toString(),
      authorProfilePhotoUrl: json['authorProfilePhotoUrl']?.toString(),
      text: (json['text'] ?? '').toString(),
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString()) ??
          DateTime.now(),
      isDeleted: json['isDeleted'] as bool? ?? false,
      parentCommentId: json['parentCommentId']?.toString(),
      parentCommentTextPreview: json['parentCommentTextPreview']?.toString(),
    );
  }
}

class ReportUserPreview {
  final String userId;
  final String userName;
  final String displayName;
  final String? profilePhotoUrl;
  final bool isActive;
  final DateTime createdAt;
  final int reportsAgainstCount;
  final int pendingReportsAgainstCount;
  final int resolvedReportsAgainstCount;

  const ReportUserPreview({
    required this.userId,
    required this.userName,
    required this.displayName,
    required this.profilePhotoUrl,
    required this.isActive,
    required this.createdAt,
    required this.reportsAgainstCount,
    required this.pendingReportsAgainstCount,
    required this.resolvedReportsAgainstCount,
  });

  factory ReportUserPreview.fromJson(Map<String, dynamic> json) {
    return ReportUserPreview(
      userId: (json['userId'] ?? '').toString(),
      userName: (json['userName'] ?? '').toString(),
      displayName: (json['displayName'] ?? '').toString(),
      profilePhotoUrl: json['profilePhotoUrl']?.toString(),
      isActive: json['isActive'] as bool? ?? false,
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString()) ??
          DateTime.now(),
      reportsAgainstCount: (json['reportsAgainstCount'] as num?)?.toInt() ?? 0,
      pendingReportsAgainstCount:
          (json['pendingReportsAgainstCount'] as num?)?.toInt() ?? 0,
      resolvedReportsAgainstCount:
          (json['resolvedReportsAgainstCount'] as num?)?.toInt() ?? 0,
    );
  }
}

class ReportTargetContext {
  final ReportPhotoPreview? photo;
  final ReportCommentPreview? comment;
  final ReportUserPreview? user;

  const ReportTargetContext({
    required this.photo,
    required this.comment,
    required this.user,
  });

  factory ReportTargetContext.fromJson(Map<String, dynamic> json) {
    return ReportTargetContext(
      photo: json['photo'] is Map<String, dynamic>
          ? ReportPhotoPreview.fromJson(json['photo'] as Map<String, dynamic>)
          : null,
      comment: json['comment'] is Map<String, dynamic>
          ? ReportCommentPreview.fromJson(
              json['comment'] as Map<String, dynamic>,
            )
          : null,
      user: json['user'] is Map<String, dynamic>
          ? ReportUserPreview.fromJson(json['user'] as Map<String, dynamic>)
          : null,
    );
  }
}

class ReportResolutionInfo {
  final bool isResolved;
  final String? resolutionCode;
  final String resolutionText;
  final String? appealText;
  final DateTime? appealedAt;

  const ReportResolutionInfo({
    required this.isResolved,
    required this.resolutionCode,
    required this.resolutionText,
    required this.appealText,
    required this.appealedAt,
  });

  factory ReportResolutionInfo.fromJson(Map<String, dynamic> json) {
    return ReportResolutionInfo(
      isResolved: json['isResolved'] as bool? ?? false,
      resolutionCode: json['resolutionCode']?.toString(),
      resolutionText:
          (json['resolutionText'] ?? 'Статус жалобы обновлён.').toString(),
      appealText: json['appealText']?.toString(),
      appealedAt: json['appealedAt'] == null
          ? null
          : DateTime.tryParse(json['appealedAt'].toString()),
    );
  }
}

class ReportItem {
  final String id;
  final String reporterUserId;
  final ReportTargetType targetType;
  final String targetId;
  final ReportReasonOption reason;
  final String? description;
  final ReportStatusOption status;
  final String? reviewedByUserId;
  final DateTime? reviewedAt;
  final DateTime createdAt;
  final ReporterPreview? reporter;
  final ReportTargetContext targetContext;
  final ReportResolutionInfo resolution;

  const ReportItem({
    required this.id,
    required this.reporterUserId,
    required this.targetType,
    required this.targetId,
    required this.reason,
    required this.description,
    required this.status,
    required this.reviewedByUserId,
    required this.reviewedAt,
    required this.createdAt,
    required this.reporter,
    required this.targetContext,
    required this.resolution,
  });

  factory ReportItem.fromJson(Map<String, dynamic> json) {
    final rawTargetType = (json['targetType'] as num?)?.toInt() ?? 1;
    final rawReason = (json['reason'] as num?)?.toInt() ?? 7;
    final rawStatus = (json['status'] as num?)?.toInt() ?? 1;

    return ReportItem(
      id: (json['id'] ?? '').toString(),
      reporterUserId: (json['reporterUserId'] ?? '').toString(),
      targetType: _targetTypeFromValue(rawTargetType),
      targetId: (json['targetId'] ?? '').toString(),
      reason: ReportReasonOptionX.fromValue(rawReason),
      description: json['description']?.toString(),
      status: ReportStatusOption.fromValue(rawStatus),
      reviewedByUserId: json['reviewedByUserId']?.toString(),
      reviewedAt: json['reviewedAt'] == null
          ? null
          : DateTime.tryParse(json['reviewedAt'].toString()),
      createdAt: DateTime.tryParse((json['createdAt'] ?? '').toString()) ??
          DateTime.now(),
      reporter: json['reporter'] is Map<String, dynamic>
          ? ReporterPreview.fromJson(json['reporter'] as Map<String, dynamic>)
          : null,
      targetContext: json['targetContext'] is Map<String, dynamic>
          ? ReportTargetContext.fromJson(
              json['targetContext'] as Map<String, dynamic>,
            )
          : const ReportTargetContext(
              photo: null,
              comment: null,
              user: null,
            ),
      resolution: json['resolution'] is Map<String, dynamic>
          ? ReportResolutionInfo.fromJson(
              json['resolution'] as Map<String, dynamic>,
            )
          : const ReportResolutionInfo(
              isResolved: false,
              resolutionCode: null,
              resolutionText: 'Статус жалобы обновлён.',
              appealText: null,
              appealedAt: null,
            ),
    );
  }

  static ReportTargetType _targetTypeFromValue(int value) {
    for (final item in ReportTargetType.values) {
      if (item.value == value) return item;
    }
    return ReportTargetType.photo;
  }

  String get statusLabel => status.label;
}

class ReportCommentTargetContext {
  final String id;
  final String photoId;
  final String? groupId;
  final String text;
  final String authorDisplayName;
  final String authorUserName;

  const ReportCommentTargetContext({
    required this.id,
    required this.photoId,
    required this.groupId,
    required this.text,
    required this.authorDisplayName,
    required this.authorUserName,
  });

  factory ReportCommentTargetContext.fromJson(Map<String, dynamic> json) {
    return ReportCommentTargetContext(
      id: (json['id'] ?? '').toString(),
      photoId: (json['photoId'] ?? '').toString(),
      groupId: json['groupId']?.toString(),
      text: (json['text'] ?? '').toString(),
      authorDisplayName: (json['authorDisplayName'] ?? '').toString(),
      authorUserName: (json['authorUserName'] ?? '').toString(),
    );
  }
}

class ReportPhotoTargetContext {
  final String id;
  final String? groupId;
  final String? caption;
  final String authorDisplayName;
  final String authorUserName;
  final String? groupName;

  const ReportPhotoTargetContext({
    required this.id,
    required this.groupId,
    required this.caption,
    required this.authorDisplayName,
    required this.authorUserName,
    required this.groupName,
  });

  factory ReportPhotoTargetContext.fromJson(Map<String, dynamic> json) {
    return ReportPhotoTargetContext(
      id: (json['id'] ?? '').toString(),
      groupId: json['groupId']?.toString(),
      caption: json['caption']?.toString(),
      authorDisplayName: (json['authorDisplayName'] ?? '').toString(),
      authorUserName: (json['authorUserName'] ?? '').toString(),
      groupName: json['groupName']?.toString(),
    );
  }
}