enum ReportTargetType {
  photo(1, 'Публикация'),
  comment(2, 'Комментарий'),
  user(3, 'Пользователь');

  final int value;
  final String label;

  const ReportTargetType(this.value, this.label);
}

enum ReportReasonOption {
  spam(1, 'Спам'),
  harassment(2, 'Оскорбления / травля'),
  violence(3, 'Насилие'),
  nudity(4, 'Откровенный контент'),
  hateSpeech(5, 'Язык ненависти'),
  fakeContent(6, 'Фейк / вводящий в заблуждение контент'),
  other(7, 'Другое');

  final int value;
  final String label;

  const ReportReasonOption(this.value, this.label);
}

extension ReportReasonOptionX on ReportReasonOption {
  static ReportReasonOption fromValue(int value) {
    for (final item in ReportReasonOption.values) {
      if (item.value == value) return item;
    }
    return ReportReasonOption.other;
  }
}