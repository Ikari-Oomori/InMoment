class AsyncActionGuard {
  bool _running = false;

  bool get isRunning => _running;

  Future<void> run(Future<void> Function() action) async {
    if (_running) return;

    _running = true;
    try {
      await action();
    } finally {
      _running = false;
    }
  }
}