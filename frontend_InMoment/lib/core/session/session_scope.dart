import 'resettable.dart';

class SessionScope {
  static final List<Resettable> _items = [];

  static void register(Resettable item) {
    // защита от повторной регистрации одного и того же инстанса
    if (_items.contains(item)) return;
    _items.add(item);
  }

  static void resetAll() {
    for (final i in _items) {
      try {
        i.reset();
      } catch (_) {}
    }
  }
}