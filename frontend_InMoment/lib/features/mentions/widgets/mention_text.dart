import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';

import '../utils/mention_parsing.dart';

class MentionText extends StatelessWidget {
  final String text;
  final TextStyle? style;
  final TextStyle? mentionStyle;
  final int? maxLines;
  final TextOverflow? overflow;
  final ValueChanged<String>? onMentionTap;

  const MentionText({
    super.key,
    required this.text,
    this.style,
    this.mentionStyle,
    this.maxLines,
    this.overflow,
    this.onMentionTap,
  });

  @override
  Widget build(BuildContext context) {
    final spans = <InlineSpan>[];
    var currentIndex = 0;

    for (final match in MentionParsing.findMentions(text)) {
      if (match.start > currentIndex) {
        spans.add(
          TextSpan(
            text: text.substring(currentIndex, match.start),
            style: style,
          ),
        );
      }

      final userName = match.group(1) ?? '';
      final visibleText = text.substring(match.start, match.end);

      spans.add(
        TextSpan(
          text: visibleText,
          style: mentionStyle ??
              style?.copyWith(
                fontWeight: FontWeight.w700,
                decoration: TextDecoration.underline,
              ),
          recognizer: onMentionTap == null
              ? null
              : (TapGestureRecognizer()
                ..onTap = () => onMentionTap!(userName)),
        ),
      );

      currentIndex = match.end;
    }

    if (currentIndex < text.length) {
      spans.add(
        TextSpan(
          text: text.substring(currentIndex),
          style: style,
        ),
      );
    }

    return RichText(
      maxLines: maxLines,
      overflow: overflow ?? TextOverflow.clip,
      text: TextSpan(
        children: spans.isEmpty
            ? [
                TextSpan(
                  text: text,
                  style: style,
                ),
              ]
            : spans,
      ),
    );
  }
}