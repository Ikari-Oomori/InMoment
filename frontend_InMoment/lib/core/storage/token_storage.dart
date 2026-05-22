import 'token_storage_backend.dart' as backend;

class StoredTokens {
  final String accessToken;
  final String refreshToken;

  const StoredTokens({
    required this.accessToken,
    required this.refreshToken,
  });

  bool get isValid => accessToken.isNotEmpty && refreshToken.isNotEmpty;
}

class TokenStorage {
  const TokenStorage();

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) {
    return backend.saveTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
    );
  }

  Future<String?> getAccessToken() {
    return backend.getAccessToken();
  }

  Future<String?> getRefreshToken() {
    return backend.getRefreshToken();
  }

  Future<StoredTokens?> getTokens() async {
    final accessToken = await getAccessToken();
    final refreshToken = await getRefreshToken();

    if (accessToken == null ||
        accessToken.isEmpty ||
        refreshToken == null ||
        refreshToken.isEmpty) {
      return null;
    }

    return StoredTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
    );
  }

  Future<bool> hasAccessToken() async {
    final token = await getAccessToken();
    return token != null && token.isNotEmpty;
  }

  Future<void> clear() {
    return backend.clearTokens();
  }
}