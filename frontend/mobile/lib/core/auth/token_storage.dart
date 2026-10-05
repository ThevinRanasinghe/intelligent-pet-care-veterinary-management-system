import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class TokenStorage {
  static const _keyToken = 'petcare.token';
  static const _keyExpiresAt = 'petcare.expiresAt';
  static const _keyUserId = 'petcare.userId';
  static const _keyEmail = 'petcare.email';
  static const _keyName = 'petcare.name';
  static const _keyRole = 'petcare.role';

  final FlutterSecureStorage _storage;

  TokenStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  Future<void> save({
    required String token,
    required String expiresAt,
    required String userId,
    required String email,
    required String name,
    required String role,
  }) async {
    await _storage.write(key: _keyToken, value: token);
    await _storage.write(key: _keyExpiresAt, value: expiresAt);
    await _storage.write(key: _keyUserId, value: userId);
    await _storage.write(key: _keyEmail, value: email);
    await _storage.write(key: _keyName, value: name);
    await _storage.write(key: _keyRole, value: role);
  }

  Future<String?> getToken() async => _storage.read(key: _keyToken);

  Future<bool> isValid() async {
    final token = await _storage.read(key: _keyToken);
    final expiresAt = await _storage.read(key: _keyExpiresAt);
    if (token == null || expiresAt == null) return false;
    final expiry = DateTime.tryParse(expiresAt);
    if (expiry == null) return false;
    return expiry.isAfter(DateTime.now());
  }

  Future<AuthSession?> getSession() async {
    final token = await _storage.read(key: _keyToken);
    if (token == null) return null;
    final expiresAt = await _storage.read(key: _keyExpiresAt);
    if (expiresAt == null) return null;
    final expiry = DateTime.tryParse(expiresAt);
    if (expiry == null || !expiry.isAfter(DateTime.now())) return null;
    return AuthSession(
      token: token,
      expiresAt: expiresAt,
      userId: await _storage.read(key: _keyUserId) ?? '',
      email: await _storage.read(key: _keyEmail) ?? '',
      name: await _storage.read(key: _keyName) ?? '',
      role: await _storage.read(key: _keyRole) ?? '',
    );
  }

  Future<void> clear() async {
    await _storage.deleteAll();
  }
}

class AuthSession {
  final String token;
  final String expiresAt;
  final String userId;
  final String email;
  final String name;
  final String role;

  AuthSession({
    required this.token,
    required this.expiresAt,
    required this.userId,
    required this.email,
    required this.name,
    required this.role,
  });
}
