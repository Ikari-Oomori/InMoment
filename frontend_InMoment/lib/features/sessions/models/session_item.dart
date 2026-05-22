class SessionItem {
  final String id;
  final String? deviceName;
  final String? platform;
  final String? ipAddress;
  final String? userAgent;
  final String? geoCountry;
  final String? geoRegion;
  final String? geoCity;
  final DateTime? createdAt;
  final DateTime? lastUsedAt;
  final DateTime? expiresAt;
  final bool isCurrent;
  final bool isRevoked;

  const SessionItem({
    required this.id,
    required this.deviceName,
    required this.platform,
    required this.ipAddress,
    required this.userAgent,
    required this.geoCountry,
    required this.geoRegion,
    required this.geoCity,
    required this.createdAt,
    required this.lastUsedAt,
    required this.expiresAt,
    required this.isCurrent,
    required this.isRevoked,
  });

  DateTime? get lastActivityAt => lastUsedAt ?? createdAt;

  String get title {
    final normalizedDevice = _normalizeDeviceName(deviceName);
    final normalizedPlatform = _normalizePlatform(platform);
    final browser = browserLabel;
    final os = osLabel;

    if (normalizedDevice != null &&
        normalizedPlatform != null &&
        normalizedDevice.toLowerCase() == normalizedPlatform.toLowerCase()) {
      return normalizedDevice;
    }

    if (normalizedDevice != null &&
        normalizedPlatform != null &&
        normalizedDevice.toLowerCase() != 'web') {
      return '$normalizedDevice • $normalizedPlatform';
    }

    if (browser != null && os != null) {
      return '$browser • $os';
    }

    if (normalizedDevice != null) return normalizedDevice;
    if (normalizedPlatform != null) return normalizedPlatform;
    if (browser != null) return browser;
    if (os != null) return os;

    return 'Устройство';
  }

  String? get browserLabel => _normalizeBrowser(userAgent);

  String? get osLabel {
    final normalizedPlatform = _normalizePlatform(platform);
    if (normalizedPlatform != null && normalizedPlatform.toLowerCase() != 'web') {
      return normalizedPlatform;
    }

    return _normalizeOsFromUserAgent(userAgent);
  }

  String? get secondaryLabel {
    if (isLikelyMobile) {
      return 'Мобильное устройство';
    }

    if (isLikelyDesktop) {
      return 'Компьютер';
    }

    if (isLikelyWeb) {
      return 'Браузер';
    }

    return 'Устройство';
  }

  String? get environmentBadge {
    final browser = browserLabel;
    final os = osLabel;

    if (browser != null && os != null) {
      return '$browser • $os';
    }

    return browser ?? os;
  }

  String? get locationLabel {
    final parts = <String>[
      if (_clean(geoCity) != null) _clean(geoCity)!,
      if (_clean(geoRegion) != null) _clean(geoRegion)!,
      if (_clean(geoCountry) != null) _clean(geoCountry)!,
    ];

    if (parts.isNotEmpty) {
      return parts.join(', ');
    }

    return null;
  }

  String? get cleanIpAddress {
    final ip = _clean(ipAddress);
    if (ip == null) return null;
    return ip;
  }

  String? get publicIpLabel {
    final ip = cleanIpAddress;
    if (ip == null) return null;
    if (_isPrivateIp(ip)) return null;
    return 'IP: $ip';
  }

  bool get isLikelyMobile {
    final lowerTitle = title.toLowerCase();
    final lowerPlatform = (platform ?? '').toLowerCase();

    return lowerTitle.contains('android') ||
        lowerTitle.contains('iphone') ||
        lowerTitle.contains('ios') ||
        lowerPlatform.contains('android') ||
        lowerPlatform.contains('ios');
  }

  bool get isLikelyDesktop {
    final lowerTitle = title.toLowerCase();
    final lowerPlatform = (platform ?? '').toLowerCase();

    return lowerTitle.contains('windows') ||
        lowerTitle.contains('mac') ||
        lowerTitle.contains('linux') ||
        lowerPlatform.contains('windows') ||
        lowerPlatform.contains('mac') ||
        lowerPlatform.contains('linux');
  }

  bool get isLikelyWeb {
    final lowerTitle = title.toLowerCase();
    final lowerPlatform = (platform ?? '').toLowerCase();

    return lowerTitle.contains('chrome') ||
        lowerTitle.contains('safari') ||
        lowerTitle.contains('firefox') ||
        lowerTitle.contains('edge') ||
        lowerTitle.contains('браузер') ||
        lowerPlatform == 'web';
  }

  bool get hasGeo =>
      _clean(geoCity) != null ||
      _clean(geoRegion) != null ||
      _clean(geoCountry) != null;

  bool get isExpired {
    final value = expiresAt;
    if (value == null) return false;
    return value.isBefore(DateTime.now().toUtc());
  }

  List<String> buildMetaLines(String Function(DateTime?) formatDate) {
    final lines = <String>[];

    if (locationLabel != null) {
      lines.add(locationLabel!);
    } else if (publicIpLabel != null) {
      lines.add(publicIpLabel!);
    }

    if (lastUsedAt != null) {
      lines.add('Последняя активность: ${formatDate(lastUsedAt)}');
    } else if (createdAt != null) {
      lines.add('Сессия создана: ${formatDate(createdAt)}');
    }

    if (expiresAt != null) {
      lines.add(
        isExpired
            ? 'Истекла: ${formatDate(expiresAt)}'
            : 'Истекает: ${formatDate(expiresAt)}',
      );
    }

    return lines;
  }

  bool _isPrivateIp(String ip) {
    return ip.startsWith('10.') ||
        ip.startsWith('192.168.') ||
        ip.startsWith('127.') ||
        ip.startsWith('172.16.') ||
        ip.startsWith('172.17.') ||
        ip.startsWith('172.18.') ||
        ip.startsWith('172.19.') ||
        ip.startsWith('172.20.') ||
        ip.startsWith('172.21.') ||
        ip.startsWith('172.22.') ||
        ip.startsWith('172.23.') ||
        ip.startsWith('172.24.') ||
        ip.startsWith('172.25.') ||
        ip.startsWith('172.26.') ||
        ip.startsWith('172.27.') ||
        ip.startsWith('172.28.') ||
        ip.startsWith('172.29.') ||
        ip.startsWith('172.30.') ||
        ip.startsWith('172.31.');
  }

  static String? _clean(String? value) {
    if (value == null) return null;
    final trimmed = value.trim();
    if (trimmed.isEmpty) return null;
    return trimmed;
  }

  static String? _normalizeDeviceName(String? value) {
    final raw = _clean(value);
    if (raw == null) return null;

    final lower = raw.toLowerCase();

    if (lower == 'android') return 'Android';
    if (lower == 'ios') return 'iOS';
    if (lower == 'iphone') return 'iPhone';
    if (lower == 'web') return 'Веб';
    if (lower == 'windows') return 'Windows';
    if (lower == 'macos' || lower == 'mac os') return 'macOS';

    return raw;
  }

  static String? _normalizePlatform(String? value) {
    final raw = _clean(value);
    if (raw == null) return null;

    final lower = raw.toLowerCase();

    if (lower == 'android') return 'Android';
    if (lower == 'ios') return 'iOS';
    if (lower == 'web') return 'Web';
    if (lower == 'windows') return 'Windows';
    if (lower == 'macos' || lower == 'mac os') return 'macOS';
    if (lower == 'linux') return 'Linux';

    return raw;
  }

  static String? _normalizeBrowser(String? value) {
    final agent = _clean(value);
    if (agent == null) return null;

    final lower = agent.toLowerCase();

    if (lower.contains('edg/')) return 'Edge';
    if (lower.contains('edge')) return 'Edge';
    if (lower.contains('firefox')) return 'Firefox';
    if (lower.contains('chrome')) return 'Chrome';
    if (lower.contains('safari') && !lower.contains('chrome')) return 'Safari';
    if (lower.contains('opera') || lower.contains('opr/')) return 'Opera';

    return null;
  }

  static String? _normalizeOsFromUserAgent(String? value) {
    final agent = _clean(value);
    if (agent == null) return null;

    final lower = agent.toLowerCase();

    if (lower.contains('windows')) return 'Windows';
    if (lower.contains('mac os') || lower.contains('macintosh')) return 'macOS';
    if (lower.contains('android')) return 'Android';
    if (lower.contains('iphone') || lower.contains('ios')) return 'iOS';
    if (lower.contains('linux')) return 'Linux';

    return null;
  }

  factory SessionItem.fromJson(Map<String, dynamic> json) {
    DateTime? parseDate(dynamic value) {
      if (value is! String || value.trim().isEmpty) {
        return null;
      }

      return DateTime.tryParse(value)?.toUtc();
    }

    String? readString(List<String> keys) {
      for (final key in keys) {
        final value = json[key];
        if (value is String && value.trim().isNotEmpty) {
          return value.trim();
        }
      }

      return null;
    }

    bool readBool(List<String> keys, {bool fallback = false}) {
      for (final key in keys) {
        final value = json[key];
        if (value is bool) {
          return value;
        }
      }

      return fallback;
    }

    return SessionItem(
      id: (json['id'] ?? '').toString(),
      deviceName: readString(['deviceName', 'device', 'clientName']),
      platform: readString(['platform']),
      ipAddress: readString(['ipAddress', 'ip']),
      userAgent: readString(['userAgent', 'browser']),
      geoCountry: readString(['geoCountry']),
      geoRegion: readString(['geoRegion']),
      geoCity: readString(['geoCity']),
      createdAt: parseDate(json['createdAt'] ?? json['issuedAt'] ?? json['createdAtUtc']),
      lastUsedAt: parseDate(json['lastUsedAt'] ?? json['lastUsedAtUtc']),
      expiresAt: parseDate(json['expiresAt'] ?? json['expiresAtUtc']),
      isCurrent: readBool(['isCurrent', 'current']),
      isRevoked: readBool(['isRevoked', 'revoked']),
    );
  }
}