import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pointycastle/export.dart';
import 'package:seat_charter/core/device/device_keys.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  test('canonical payload matches the API format', () {
    final p = canonicalCheckInPayload(
      token: 'abc',
      lat: 56.4586,
      lng: -2.9828,
      accuracyM: 8,
      isMocked: false,
      clientTime: '2026-11-25T10:00:00.000Z',
      deviceId: 'dev-1',
    );
    expect(p, 'abc|56.458600|-2.982800|8.0|false|2026-11-25T10:00:00.000Z|dev-1');
  });

  test('public key is a raw uncompressed P-256 point and survives reload', () async {
    final keys = DeviceKeys();
    final pub = base64.decode(await keys.ensurePublicKey());
    expect(pub.length, 65);
    expect(pub[0], 0x04);

    // A fresh instance reads the same key back from storage.
    expect(await DeviceKeys().ensurePublicKey(), base64.encode(pub));
  });

  test('signature is raw r|s and verifies against the public key', () async {
    final keys = DeviceKeys();
    final pub = base64.decode(await keys.ensurePublicKey());
    const payload = 'tok|56.458600|-2.982800|8.0|false|t|d';

    final sig = base64.decode(await keys.sign(payload));
    expect(sig.length, 64);

    final curve = ECCurve_secp256r1();
    final verifier = ECDSASigner(SHA256Digest())
      ..init(false, PublicKeyParameter<ECPublicKey>(ECPublicKey(curve.curve.decodePoint(pub), curve)));
    BigInt big(Uint8List b) => b.fold(BigInt.zero, (a, x) => (a << 8) | BigInt.from(x));
    final ok = verifier.verifySignature(
      Uint8List.fromList(utf8.encode(payload)),
      ECSignature(big(sig.sublist(0, 32)), big(sig.sublist(32))),
    );
    expect(ok, isTrue);
  });

  test('reset drops the key and device id', () async {
    final keys = DeviceKeys();
    await keys.ensurePublicKey();
    await keys.saveDeviceId('dev-1');
    await keys.reset();
    expect(await keys.hasKey(), isFalse);
    expect(await keys.deviceId(), isNull);
  });
}
