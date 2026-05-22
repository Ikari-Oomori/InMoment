import 'package:signalr_netcore/hub_connection.dart';
import 'package:signalr_netcore/hub_connection_builder.dart';
import 'package:signalr_netcore/http_connection_options.dart';

import '../config/env.dart';
import '../storage/token_storage.dart';

class GroupRealtimeService {
  GroupRealtimeService._();

  static final GroupRealtimeService instance = GroupRealtimeService._();

  final _tokenStorage = TokenStorage();

  HubConnection? _connection;
  bool _starting = false;

  final List<void Function()> _feedChangedListeners = [];

  String? _joinedGroupId;
  String? _connectedAccessToken;

  Future<void> ensureConnected() async {
    final token = await _tokenStorage.getAccessToken() ?? '';

    if (token.isEmpty) {
      await _resetConnection(
        clearJoinedGroup: false,
        clearListeners: false,
      );
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
      await _resetConnection(
        clearJoinedGroup: false,
        clearListeners: false,
      );
    }

    _starting = true;

    try {
      final connection = HubConnectionBuilder()
          .withUrl(
            '${Env.baseUrl}/hubs/groups',
            options: HttpConnectionOptions(
              accessTokenFactory: () async {
                final freshToken = await _tokenStorage.getAccessToken();
                return freshToken ?? '';
              },
            ),
          )
          .withAutomaticReconnect()
          .build();

      connection.on('FeedChanged', (arguments) {
        for (final listener in List<void Function()>.from(_feedChangedListeners)) {
          listener();
        }
      });

      await connection.start();

      _connection = connection;
      _connectedAccessToken = token;

      final groupIdToRestore = _joinedGroupId;
      if (groupIdToRestore != null) {
        try {
          await _connection!.invoke('JoinGroup', args: <Object>[groupIdToRestore]);
        } catch (_) {}
      }
    } finally {
      _starting = false;
    }
  }

  Future<void> joinGroup(String groupId) async {
    await ensureConnected();

    if (_connection == null) return;

    if (_joinedGroupId == groupId) return;

    if (_joinedGroupId != null && _joinedGroupId != groupId) {
      try {
        await _connection!.invoke('LeaveGroup', args: <Object>[_joinedGroupId!]);
      } catch (_) {}
    }

    await _connection!.invoke('JoinGroup', args: <Object>[groupId]);
    _joinedGroupId = groupId;
  }

  Future<void> leaveCurrentGroup() async {
    if (_connection == null || _joinedGroupId == null) return;

    try {
      await _connection!.invoke('LeaveGroup', args: <Object>[_joinedGroupId!]);
    } catch (_) {}

    _joinedGroupId = null;
  }

  void addFeedChangedListener(void Function() listener) {
    _feedChangedListeners.add(listener);
  }

  void removeFeedChangedListener(void Function() listener) {
    _feedChangedListeners.remove(listener);
  }

  Future<void> disposeConnection() async {
    await _resetConnection(
      clearJoinedGroup: true,
      clearListeners: true,
    );
  }

  Future<void> _resetConnection({
    required bool clearJoinedGroup,
    required bool clearListeners,
  }) async {
    try {
      await _connection?.stop();
    } catch (_) {}

    _connection = null;
    _connectedAccessToken = null;
    _starting = false;

    if (clearJoinedGroup) {
      _joinedGroupId = null;
    }

    if (clearListeners) {
      _feedChangedListeners.clear();
    }
  }
}