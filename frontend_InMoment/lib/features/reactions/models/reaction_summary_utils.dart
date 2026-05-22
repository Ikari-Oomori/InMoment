class ReactionSummaryUtils {
  static int totalCountFromPairs<T>(
    Iterable<T> items,
    int Function(T item) countSelector,
  ) {
    var total = 0;
    for (final item in items) {
      total += countSelector(item);
    }
    return total;
  }

  static int topReactionTypeFromPairs<T>(
    Iterable<T> items, {
    required int Function(T item) typeSelector,
    required int Function(T item) countSelector,
  }) {
    int topType = 0;
    int topCount = -1;

    for (final item in items) {
      final count = countSelector(item);
      if (count > topCount) {
        topCount = count;
        topType = typeSelector(item);
      }
    }

    return topType;
  }
}