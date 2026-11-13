import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Access + refresh tokens in the Android Keystore-backed secure storage.
/// The access token is also cached in memory to avoid a platform call per request.
class TokenStore {
  static const _accessKey = 'auth.access';
  static const _refreshKey = 'auth.refresh';

  final FlutterSecureStorage _storage;
  String? _access;
  bool _loaded = false;

  TokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  Future<String?> accessToken() async {
    if (!_loaded) {
      _access = await _storage.read(key: _accessKey);
      _loaded = true;
    }
    return _access;
  }

  Future<String?> refreshToken() => _storage.read(key: _refreshKey);

  Future<bool> hasRefreshToken() async => (await refreshToken()) != null;

  Future<void> save(String access, String refresh) async {
    _access = access;
    _loaded = true;
    await _storage.write(key: _accessKey, value: access);
    await _storage.write(key: _refreshKey, value: refresh);
  }

  Future<void> clear() async {
    _access = null;
    _loaded = true;
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }
}
