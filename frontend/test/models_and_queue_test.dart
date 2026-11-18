import 'package:flutter_test/flutter_test.dart';
import 'package:seat_charter/core/api/models.dart';
import 'package:seat_charter/core/offline/checkin_queue.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  test('rejected check-in result parses from a 422 body', () {
    final r = CheckInResult.fromJson({
      'checkInId': 'c1',
      'status': 'Rejected',
      'rejectionReason': 'ExpiredToken',
      'flags': <String>[],
      'message': 'That code has expired.',
      'at': '2026-11-25T10:00:00Z',
      'isDuplicate': false,
    });
    expect(r.accepted, isFalse);
    expect(r.rejectionReason, 'ExpiredToken');
  });

  test('qr payload falls back to the bare token when no deep link host is set', () {
    final q = SessionQr.fromJson({
      'token': 'tok',
      'qrPayload': '',
      'manualCode': '123456',
      'step': 1,
      'stepSeconds': 15,
      'secondsRemaining': 7,
      'endsAt': '2026-11-25T11:00:00Z',
      'checkedInCount': 3,
      'enrolledCount': 5,
    });
    expect(q.qrPayload, 'tok');
  });

  test('humanise splits enum names', () {
    expect(humanise('FarFromLecturer'), 'Far from lecturer');
    expect(humanise('Present'), 'Present');
  });

  test('offline queue keeps signed requests and drops stale ones', () async {
    SharedPreferences.setMockInitialValues({
      'offline.checkins': [
        '{"request":{"clientTime":"old"},"queuedAt":"2020-01-01T00:00:00Z"}',
      ],
    });
    final queue = CheckInQueue(await SharedPreferences.getInstance());
    expect(queue.all(), isEmpty);

    await queue.add({'token': 't', 'clientTime': 'a'});
    await queue.add({'token': 't', 'clientTime': 'b'});
    expect(queue.all().map((q) => q.request['clientTime']), ['a', 'b']);

    await queue.remove(queue.all().first);
    expect(queue.all().single.request['clientTime'], 'b');
  });
}
