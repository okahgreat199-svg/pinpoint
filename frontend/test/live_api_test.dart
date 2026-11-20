// End-to-end check against a running API, using the app's own client, keys and signing.
// Skipped unless LIVE_API is set:
//
//   flutter test test/live_api_test.dart --dart-define=LIVE_API=http://localhost:5080 --dart-define=SEED_PASSWORD=...
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:seat_charter/core/api/api_client.dart';
import 'package:seat_charter/core/api/models.dart';
import 'package:seat_charter/core/api/seat_api.dart';
import 'package:seat_charter/core/auth/token_store.dart';
import 'package:seat_charter/core/device/device_keys.dart';

const liveApi = String.fromEnvironment('LIVE_API');
const password = String.fromEnvironment('SEED_PASSWORD');

// One store shared by the client and the logins, as in the app.
final tokens = TokenStore();

SeatApi _client() {
  final dio = Dio(BaseOptions(baseUrl: '$liveApi/api/v1', contentType: Headers.jsonContentType, validateStatus: (_) => true));
  return SeatApi(ApiClient(tokens, () async => null, dio: dio));
}

Future<UserProfile> _login(SeatApi api, String email) async {
  final r = await api.login(email, password);
  await tokens.save(r.accessToken, r.refreshToken);
  return r.user;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  // flutter_test blocks real HTTP by default.
  HttpOverrides.global = null;

  test('full flow: lecturer opens session, student binds + checks in', () async {
    FlutterSecureStorage.setMockInitialValues({});
    final api = _client();
    final raw = Dio(BaseOptions(baseUrl: '$liveApi/api/v1', contentType: Headers.jsonContentType, validateStatus: (_) => true));

    // Student id (for the admin reset).
    final studentUser = await _login(api, 'student4@seatcharter.dev');

    // Admin resets student4's devices so the new key below becomes Active (first device).
    await _login(api, 'admin@seatcharter.dev');
    final adminToken = await tokens.accessToken();
    final reset = await raw.post('/devices/users/${studentUser.id}/reset', options: Options(headers: {'Authorization': 'Bearer $adminToken'}));
    expect(reset.statusCode, 200, reason: '${reset.data}');

    // Lecturer opens a session on CS32005 anchored at the QMB (close any leftover first).
    await _login(api, 'lecturer@seatcharter.dev');
    final course = (await api.courses()).firstWhere((c) => c.code == 'CS32005');
    final timetable = await api.timetable();
    for (final e in timetable.where((e) => e.isOpen && e.courseId == course.id && e.sessionId != null)) {
      await api.closeSession(e.sessionId!);
    }
    final session = await api.startSession(courseId: course.id, lat: 56.4586, lng: -2.9828, accuracyM: 10, durationMinutes: 30);
    final qr = await api.qr(session.id);

    // Student binds this "phone" and checks in with what the camera would scan.
    await _login(api, 'student4@seatcharter.dev');
    final keys = DeviceKeys();
    final device = await api.registerDevice(publicKey: await keys.ensurePublicKey(), model: 'flutter test', hardwareId: 'flutter-test-${DateTime.now().microsecondsSinceEpoch}');
    expect(device.status, 'Active');
    await keys.saveDeviceId(device.id);

    final clientTime = DateTime.now().toUtc().toIso8601String();
    const lat = 56.45865, lng = -2.98285, acc = 9.0;
    final token = qr.qrPayload;
    final signature = await keys.sign(canonicalCheckInPayload(
        token: token, lat: lat, lng: lng, accuracyM: acc, isMocked: false, clientTime: clientTime, deviceId: device.id));

    final r = await api.checkIn({
      'token': token,
      'lat': lat,
      'lng': lng,
      'accuracyM': acc,
      'isMocked': false,
      'clientTime': clientTime,
      'deviceId': device.id,
      'signature': signature,
    });
    expect(r.statusCode, 201, reason: r.message);
    expect(r.data!.status, 'Present');

    // Tampering with the location after signing must fail the signature check.
    final tampered = await api.checkIn({
      'token': token,
      'lat': lat + 0.01,
      'lng': lng,
      'accuracyM': acc,
      'isMocked': false,
      'clientTime': clientTime,
      'deviceId': device.id,
      'signature': signature,
    });
    // Signature is checked before the duplicate rule, so this is a 422, not a 200 duplicate.
    expect(tampered.statusCode, 422);
    expect(tampered.data?.rejectionReason, 'InvalidSignature');

    // Lecturer sees the student present, then closes.
    await _login(api, 'lecturer@seatcharter.dev');
    final attendance = await api.attendance(session.id);
    expect(attendance.roster.firstWhere((x) => x.studentId == studentUser.id).status, 'Present');
    await api.closeSession(session.id);
  }, skip: liveApi.isEmpty ? 'LIVE_API not set' : false);
}
