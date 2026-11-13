import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:pointycastle/export.dart';

/// The device's ECDSA P-256 keypair. The private scalar never leaves secure storage
/// except into memory for signing; the API only ever sees the public point.
class DeviceKeys {
  static const _privateKey = 'device.private';
  static const _deviceIdKey = 'device.id';

  final FlutterSecureStorage _storage;
  final ECDomainParameters _curve = ECCurve_secp256r1();
  ECPrivateKey? _cached;

  DeviceKeys([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  Future<bool> hasKey() async => (await _storage.read(key: _privateKey)) != null;

  /// Creates the keypair if missing and returns the public key as base64 of the raw 65-byte point (04|X|Y).
  Future<String> ensurePublicKey() async {
    final priv = await _load() ?? await _generate();
    final q = (_curve.G * priv.d)!;
    return base64.encode(q.getEncoded(false));
  }

  /// Signs the UTF-8 bytes of [payload] with SHA-256; returns base64 of raw r|s (64 bytes).
  Future<String> sign(String payload) async {
    final priv = await _load();
    if (priv == null) throw StateError('No device key. Bind this device first.');

    // RFC 6979 deterministic k: no dependency on a good RNG at signing time.
    final signer = ECDSASigner(SHA256Digest(), HMac(SHA256Digest(), 64))..init(true, PrivateKeyParameter<ECPrivateKey>(priv));
    final sig = signer.generateSignature(Uint8List.fromList(utf8.encode(payload))) as ECSignature;

    final out = Uint8List(64)
      ..setRange(0, 32, _fixed32(sig.r))
      ..setRange(32, 64, _fixed32(sig.s));
    return base64.encode(out);
  }

  Future<String?> deviceId() => _storage.read(key: _deviceIdKey);
  Future<void> saveDeviceId(String id) => _storage.write(key: _deviceIdKey, value: id);

  /// Used after the API revokes this device: the next login binds a fresh key.
  Future<void> reset() async {
    _cached = null;
    await _storage.delete(key: _privateKey);
    await _storage.delete(key: _deviceIdKey);
  }

  Future<ECPrivateKey?> _load() async {
    if (_cached != null) return _cached;
    final hex = await _storage.read(key: _privateKey);
    if (hex == null) return null;
    return _cached = ECPrivateKey(BigInt.parse(hex, radix: 16), _curve);
  }

  Future<ECPrivateKey> _generate() async {
    final seed = Random.secure();
    final random = FortunaRandom()..seed(KeyParameter(Uint8List.fromList(List.generate(32, (_) => seed.nextInt(256)))));
    final generator = ECKeyGenerator()..init(ParametersWithRandom(ECKeyGeneratorParameters(_curve), random));
    final pair = generator.generateKeyPair();
    final priv = pair.privateKey;
    await _storage.write(key: _privateKey, value: priv.d!.toRadixString(16));
    return _cached = priv;
  }

  static Uint8List _fixed32(BigInt v) {
    final out = Uint8List(32);
    var x = v;
    for (var i = 31; i >= 0; i--) {
      out[i] = (x & BigInt.from(0xff)).toInt();
      x = x >> 8;
    }
    return out;
  }
}

/// The exact string the API verifies (see api/Domain/DeviceSignature.cs).
String canonicalCheckInPayload({
  required String token,
  required double lat,
  required double lng,
  required double accuracyM,
  required bool isMocked,
  required String clientTime,
  required String deviceId,
}) =>
    [token, lat.toStringAsFixed(6), lng.toStringAsFixed(6), accuracyM.toStringAsFixed(1), isMocked ? 'true' : 'false', clientTime, deviceId]
        .join('|');
