import 'package:flutter/material.dart';

class ReactionCatalogItem {
  final int type;
  final String emoji;
  final String label;
  final IconData fallbackIcon;

  const ReactionCatalogItem({
    required this.type,
    required this.emoji,
    this.label = '',
    required this.fallbackIcon,
  });
}

class ReactionCatalog {
  static const int none = 0;

  static const ReactionCatalogItem heart = ReactionCatalogItem(
    type: 1,
    emoji: '❤️',
    fallbackIcon: Icons.favorite_rounded,
  );

  static const ReactionCatalogItem like = ReactionCatalogItem(
    type: 2,
    emoji: '💜',
    fallbackIcon: Icons.thumb_up_rounded,
  );

  static const ReactionCatalogItem fire = ReactionCatalogItem(
    type: 3,
    emoji: '🔥',
    fallbackIcon: Icons.local_fire_department_rounded,
  );

  static const ReactionCatalogItem laugh = ReactionCatalogItem(
    type: 4,
    emoji: '😂',
    fallbackIcon: Icons.emoji_emotions_rounded,
  );

  static const ReactionCatalogItem sad = ReactionCatalogItem(
    type: 5,
    emoji: '😭',
    fallbackIcon: Icons.sentiment_dissatisfied_rounded,
  );

  static const ReactionCatalogItem wow = ReactionCatalogItem(
    type: 6,
    emoji: '😮',
    fallbackIcon: Icons.emoji_emotions_outlined,
  );

  static const ReactionCatalogItem cool = ReactionCatalogItem(
    type: 7,
    emoji: '😎',
    fallbackIcon: Icons.sentiment_satisfied_alt_rounded,
  );

  static const ReactionCatalogItem angry = ReactionCatalogItem(
    type: 8,
    emoji: '😡',
    fallbackIcon: Icons.sentiment_very_dissatisfied_rounded,
  );

  static const ReactionCatalogItem clap = ReactionCatalogItem(
    type: 9,
    emoji: '👏',
    fallbackIcon: Icons.pan_tool_rounded,
  );

  static const ReactionCatalogItem support = ReactionCatalogItem(
    type: 10,
    emoji: '🫶',
    fallbackIcon: Icons.favorite_border_rounded,
  );

  static const ReactionCatalogItem thumbsUp = ReactionCatalogItem(
    type: 11,
    emoji: '👍',
    fallbackIcon: Icons.thumb_up_rounded,
  );

  static const ReactionCatalogItem thumbsDown = ReactionCatalogItem(
    type: 12,
    emoji: '👎',
    fallbackIcon: Icons.thumb_down_rounded,
  );

  static const ReactionCatalogItem devil = ReactionCatalogItem(
    type: 13,
    emoji: '😈',
    fallbackIcon: Icons.whatshot_rounded,
  );

  static const ReactionCatalogItem skull = ReactionCatalogItem(
    type: 14,
    emoji: '💀',
    fallbackIcon: Icons.warning_rounded,
  );

  static const ReactionCatalogItem banana = ReactionCatalogItem(
    type: 15,
    emoji: '🍌',
    fallbackIcon: Icons.emoji_food_beverage_rounded,
  );

  static const ReactionCatalogItem peach = ReactionCatalogItem(
    type: 16,
    emoji: '🍑',
    fallbackIcon: Icons.emoji_food_beverage_rounded,
  );

  static const List<ReactionCatalogItem> all = [
    heart,
    like,
    fire,
    laugh,
    sad,
    wow,
    cool,
    angry,
    clap,
    support,
    thumbsUp,
    thumbsDown,
    devil,
    skull,
    banana,
    peach,
  ];

  static ReactionCatalogItem? byType(int type) {
    for (final item in all) {
      if (item.type == type) return item;
    }
    return null;
  }

  static bool isSupported(int type) {
    return type == none || byType(type) != null;
  }

  static String emojiOf(int type) {
    return byType(type)?.emoji ?? '✨';
  }

  static String labelOf(int type) {
    return byType(type)?.label ?? '';
  }

  static IconData iconOf(int type) {
    return byType(type)?.fallbackIcon ?? Icons.emoji_emotions_outlined;
  }

  static ReactionCatalogItem get primary => heart;
}