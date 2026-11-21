// Fills the open CS32005 session with a realistic mix of check-ins, for demoing the roster.
// Start a CS32005 session first (app or Swagger), then:
//
//   flutter test test/demo_roster_test.dart --dart-define=LIVE_API=http://localhost:5080 --dart-define=SEED_PASSWORD=...
//
// student1 in the room → Present; student2 200 m away → Flagged (far from lecturer);
// student3 with 120 m accuracy → Flagged (unverified location); student4 off campus → Rejected; student5 absent.
import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:seat_charter/core/api/api_client.dart';
import 'package:seat_charter/core/api/seat_api.dart';
import 'package:seat_charter/core/auth/token_store.dart';
import 'package:seat_charter/core/device/device_keys.dart';

const liveApi = String.fromEnvironment('LIVE_API');
const password = String.fromEnvironment('SEED_PASSWORD');

final tokens = TokenStore();
Dio _dio() => Dio(BaseOptions(baseUrl: '$liveApi/api/v1', contentType: Headers.jsonContentType, validateStatus: (_) => true));
final api = SeatApi(ApiClient(tokens, () async => null, dio: _dio()));

Future<String> _login(String email) async {
  final r = await api.login(email, password);
  await tokens.save(r.accessToken, r.refreshToken);
  return r.user.id;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  HttpOverrides.global = null;

  test('populate the open CS32005 session', () async {
    FlutterSecureStorage.setMockInitialValues({});

    await _login('lecturer@seatcharter.dev');
    final open = (await api.timetable()).where((e) => e.isOpen && e.courseCode == 'CS32005' && e.sessionId != null);
    expect(open, isNotEmpty, reason: 'Start a CS32005 session first.');
    final sessionId = open.first.sessionId!;
    // Re-runnable: skip students already on the register (the auth rate limit can stop a run part-way).
    // The cases run in order, so the number of students with a result is how many to skip.
    final done = (await api.attendance(sessionId)).roster.where((r) => r.status != 'Absent' || r.rejectedAttempts > 0).length;

    final cases = [
      ('student1@seatcharter.dev', 56.45865, -2.98285, 8.0),   // in the room
      ('student2@seatcharter.dev', 56.46040, -2.98280, 8.0),   // ~200 m from the anchor
      ('student3@seatcharter.dev', 56.45862, -2.98282, 120.0), // poor GPS
      ('student4@seatcharter.dev', 56.46890, -2.98740, 8.0),   // the Law, off campus
    ];

    for (final (email, lat, lng, acc) in cases.skip(done)) {
      // Fresh device per run: admin clears the student's phones so this key becomes Active.
      final studentId = await _login(email);
      await _login('admin@seatcharter.dev');
      final adminToken = await tokens.accessToken();
      await _dio().post('/devices/users/$studentId/reset', options: Options(headers: {'Authorization': 'Bearer $adminToken'}));

      await _login('lecturer@seatcharter.dev');
      final token = (await api.qr(sessionId)).qrPayload;

      await _login(email);
      FlutterSecureStorage.setMockInitialValues({});
      final keys = DeviceKeys();
      final device = await api.registerDevice(
          publicKey: await keys.ensurePublicKey(), model: 'Demo phone', hardwareId: 'demo-$email-${DateTime.now().microsecondsSinceEpoch}');
      final clientTime = DateTime.now().toUtc().toIso8601String();
      final signature = await keys.sign(canonicalCheckInPayload(
          token: token, lat: lat, lng: lng, accuracyM: acc, isMocked: false, clientTime: clientTime, deviceId: device.id));
      final r = await api.checkIn({
        'token': token, 'lat': lat, 'lng': lng, 'accuracyM': acc, 'isMocked': false,
        'clientTime': clientTime, 'deviceId': device.id, 'signature': signature,
      });
      // ignore: avoid_print
      print('$email → ${r.statusCode} ${r.data?.status} ${r.data?.flags ?? ''} ${r.data?.rejectionReason ?? ''}');
    }
  }, skip: liveApi.isEmpty ? 'LIVE_API not set' : false);
}
