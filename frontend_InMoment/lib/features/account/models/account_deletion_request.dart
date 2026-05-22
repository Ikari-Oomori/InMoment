class AccountDeletionRequest {
  final String id;
  final String userId;
  final int statusCode;
  final String status;
  final String requestedEmail;
  final String requestedUserName;
  final String? note;
  final String? processingNote;
  final String? processedByUserId;
  final DateTime requestedAtUtc;
  final DateTime updatedAtUtc;
  final DateTime? processedAtUtc;

  const AccountDeletionRequest({
    required this.id,
    required this.userId,
    required this.statusCode,
    required this.status,
    required this.requestedEmail,
    required this.requestedUserName,
    required this.note,
    required this.processingNote,
    required this.processedByUserId,
    required this.requestedAtUtc,
    required this.updatedAtUtc,
    required this.processedAtUtc,
  });

  bool get isActive => statusCode == 1 || statusCode == 2;

  bool get isTerminal => statusCode == 3 || statusCode == 4 || statusCode == 5;

  String get statusLabel {
    switch (statusCode) {
      case 1:
        return 'Ожидает обработки';
      case 2:
        return 'В обработке';
      case 3:
        return 'Удаление выполнено';
      case 4:
        return 'Отклонён';
      case 5:
        return 'Отменён';
      default:
        return status;
    }
  }

  factory AccountDeletionRequest.fromJson(Map<String, dynamic> json) {
    DateTime parseDate(String key) {
      final raw = json[key]?.toString();
      if (raw == null || raw.isEmpty) {
        return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
      }

      return DateTime.tryParse(raw)?.toUtc() ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
    }

    DateTime? parseNullableDate(String key) {
      final raw = json[key]?.toString();
      if (raw == null || raw.isEmpty) return null;
      return DateTime.tryParse(raw)?.toUtc();
    }

    return AccountDeletionRequest(
      id: (json['id'] ?? '').toString(),
      userId: (json['userId'] ?? '').toString(),
      statusCode: (json['statusCode'] as num?)?.toInt() ?? 0,
      status: (json['status'] ?? '').toString(),
      requestedEmail: (json['requestedEmail'] ?? '').toString(),
      requestedUserName: (json['requestedUserName'] ?? '').toString(),
      note: json['note']?.toString(),
      processingNote: json['processingNote']?.toString(),
      processedByUserId: json['processedByUserId']?.toString(),
      requestedAtUtc: parseDate('requestedAtUtc'),
      updatedAtUtc: parseDate('updatedAtUtc'),
      processedAtUtc: parseNullableDate('processedAtUtc'),
    );
  }
}