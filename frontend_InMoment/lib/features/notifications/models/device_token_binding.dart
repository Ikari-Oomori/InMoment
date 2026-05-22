class DeviceTokenBinding {
  final String id;
  final String token;
  final int platform;
  final int provider;
  final String? deviceName;
  final bool isActive;

  const DeviceTokenBinding({
    required this.id,
    required this.token,
    required this.platform,
    required this.provider,
    required this.deviceName,
    required this.isActive,
  });

  factory DeviceTokenBinding.fromJson(Map<String, dynamic> json) {
    return DeviceTokenBinding(
      id: (json['id'] ?? '').toString(),
      token: (json['token'] ?? '').toString(),
      platform: (json['platform'] as num?)?.toInt() ?? 0,
      provider: (json['provider'] as num?)?.toInt() ?? 0,
      deviceName: json['deviceName']?.toString(),
      isActive: json['isActive'] as bool? ?? false,
    );
  }
}