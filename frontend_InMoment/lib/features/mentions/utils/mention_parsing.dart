class ActiveMentionMatch {
  final String query;
  final int start;
  final int end;

  const ActiveMentionMatch({
    required this.query,
    required this.start,
    required this.end,
  });
}

class MentionReplaceResult {
  final String text;
  final int caretOffset;

  const MentionReplaceResult({
    required this.text,
    required this.caretOffset,
  });
}

class MentionParsing {
  static final RegExp mentionRegex = RegExp(
    r'(?<![\w@])@([A-Za-zА-Яа-яЁё0-9_\.]{2,50})',
    unicode: true,
  );

  static final RegExp activeMentionRegex = RegExp(
    r'(?<![\w@])@([A-Za-zА-Яа-яЁё0-9_\.]{0,50})$',
    unicode: true,
  );

  static ActiveMentionMatch? findActiveMention(
    String text,
    int caretOffset,
  ) {
    if (caretOffset < 0 || caretOffset > text.length) return null;

    final beforeCaret = text.substring(0, caretOffset);
    final match = activeMentionRegex.firstMatch(beforeCaret);

    if (match == null) return null;

    return ActiveMentionMatch(
      query: match.group(1) ?? '',
      start: match.start,
      end: caretOffset,
    );
  }

  static MentionReplaceResult replaceActiveMention({
    required String text,
    required ActiveMentionMatch match,
    required String userName,
  }) {
    final prefix = text.substring(0, match.start);
    final suffix = text.substring(match.end);

    final needsTrailingSpace = suffix.isEmpty ||
        !suffix.startsWith(RegExp(r'[\s\.\,\!\?\:\;\)\]\}]'));

    final replacement = '@$userName${needsTrailingSpace ? ' ' : ''}';
    final newText = '$prefix$replacement$suffix';
    final caretOffset = prefix.length + replacement.length;

    return MentionReplaceResult(
      text: newText,
      caretOffset: caretOffset,
    );
  }

  static Iterable<RegExpMatch> findMentions(String text) {
    return mentionRegex.allMatches(text);
  }
}