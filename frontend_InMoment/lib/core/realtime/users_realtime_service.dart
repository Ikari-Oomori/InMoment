import 'package:signalr_netcore/hub_connection.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';
import 'package:signalr_netcore/http_connection_options.dart';

import '../config/env.dart';
import '../storage/token_storage.dart';
import '../../features/notifications/controllers/notifications_controller.dart';

class UsersRealtimeService {
  UsersRealtimeService._();

  static final UsersRealtimeService instance = UsersRealtimeService._();

  final TokenStorage _tokenStorage = const TokenStorage();

  HubConnection? _connection;
  bool _starting = false;
  String? _connectedAccessToken;

  Future<void> ensureConnected() async {
    final token = await _tokenStorage.getAccessToken() ?? '';

    if (token.isEmpty) {
      await _resetConnection();
      return;
    }

    if (_starting) return;

    final hasLiveConnection = _connection != null &&
        (_connection!.state.toString().contains('Connected') ||
            _connection!.state.toString().contains('Connecting'));

    final tokenChanged = _connectedAccessToken != token;

    if (hasLiveConnection && !tokenChanged) {
      return;
    }

    if (_connection != null || tokenChanged) {
      await _resetConnection();
    }

    _starting = true;

    try {
      final connection = HubConnectionBuilder()
          .withUrl(
            '${Env.baseUrl}/hubs/users',
            options: HttpConnectionOptions(
              accessTokenFactory: () async {
                final freshToken = await _tokenStorage.getAccessToken();
                return freshToken ?? '';
              },
            ),
          )
          .withAutomaticReconnect()
          .build();

      connection.on('NotificationsChanged', (arguments) async {
        if (arguments == null || arguments.isEmpty) return;

        final raw = arguments.first;
        final unreadCount = raw is int
            ? raw
            : int.tryParse(raw.toString()) ?? 0;

        await NotificationsController.instance.applyRealtimeUnreadCount(
          unreadCount,
        );
      });

      await connection.start();
      await connection.invoke('JoinSelf');

      _connection = connection;
      _connectedAccessToken = token;
    } finally {
      _starting = false;
    }
  }

  Future<void> disposeConnection() async {
    await _resetConnection();
  }

  Future<void> _resetConnection() async {
    try {
      await _connection?.invoke('LeaveSelf');
    } catch (_) {}

    try {
      await _connection?.stop();
    } catch (_) {}

    _connection = null;
    _connectedAccessToken = null;
    _starting = false;
  }
}