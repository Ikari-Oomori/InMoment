import 'package:flutter_secure_storage/flutter_secure_storage.dart';

const _accessTokenKey = 'accessToken';
const _refreshTokenKey = 'refreshToken';

const FlutterSecureStorage _storage = FlutterSecureStorage(
  aOptions: AndroidOptions(
    encryptedSharedPreferences: true,
  ),
  iOptions: IOSOptions(
    accessibility: KeychainAccessibility.first_unlock,
  ),
  mOptions: MacOsOptions(
    accessibility: KeychainAccessibility.first_unlock,
  ),
);

Future<void> saveTokens({
  required String accessToken,
  required String refreshToken,
}) async {
  await _storage.write(key: _accessTokenKey, value: accessToken);
  await _storage.write(key: _refreshTokenKey, value: refreshToken);
}

Future<String?> getAccessToken() {
  return _storage.read(key: _accessTokenKey);
}

Future<String?> getRefreshToken() {
  return _storage.read(key: _refreshTokenKey);
}

Future<void> clearTokens() async {
  await _storage.delete(key: _accessTokenKey);
  await _storage.delete(key: _refreshTokenKey);
}