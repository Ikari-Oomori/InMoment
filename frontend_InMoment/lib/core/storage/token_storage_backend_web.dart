import 'package:web/web.dart' as web;

const _accessTokenKey = 'accessToken';
const _refreshTokenKey = 'refreshToken';

Future<void> saveTokens({
  required String accessToken,
  required String refreshToken,
}) async {
  web.window.localStorage.setItem(_accessTokenKey, accessToken);
  web.window.localStorage.setItem(_refreshTokenKey, refreshToken);
}

Future<String?> getAccessToken() async {
  return web.window.localStorage.getItem(_accessTokenKey);
}

Future<String?> getRefreshToken() async {
  return web.window.localStorage.getItem(_refreshTokenKey);
}

Future<void> clearTokens() async {
  web.window.localStorage.removeItem(_accessTokenKey);
  web.window.localStorage.removeItem(_refreshTokenKey);
}