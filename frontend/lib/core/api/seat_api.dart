import 'api_client.dart';
import 'models.dart';

/// One method per endpoint the app uses. Throws [ApiException] on failure unless noted.
class SeatApi {
  final ApiClient _c;

  SeatApi(this._c);

  // ── Auth ──
  Future<AuthResult> login(String email, String password) => _c.require(_c.post('/auth/login',
      auth: false, body: {'email': email, 'password': password}, parse: (j) => AuthResult.fromJson(j as Json)));

  Future<AuthResult> register({
    required String email,
    required String fullName,
    required String password,
    required UserGroup role,
    String? matricNo,
    String? inviteCode,
  }) =>
      _c.require(_c.post('/auth/register',
          auth: false,
          appCheck: true,
          body: {
            'email': email,
            'fullName': fullName,
            'password': password,
            'role': role == UserGroup.lecturer ? 'Lecturer' : 'Student',
            if (matricNo != null && matricNo.isNotEmpty) 'matricNo': matricNo,
            if (inviteCode != null && inviteCode.isNotEmpty) 'inviteCode': inviteCode,
          },
          parse: (j) => AuthResult.fromJson(j as Json)));

  Future<UserProfile> me() => _c.require(_c.get('/auth/me', parse: (j) => UserProfile.fromJson(j as Json)));

  Future<void> logout(String refreshToken) => _c.post('/auth/logout', body: {'refreshToken': refreshToken});

  // ── Devices ──
  Future<DeviceInfo> registerDevice({required String publicKey, required String model, String? hardwareId, String? pushToken}) =>
      _c.require(_c.post('/devices',
          appCheck: true,
          body: {'publicKey': publicKey, 'platform': 'Android', 'model': model, 'hardwareId': hardwareId, 'pushToken': pushToken},
          parse: (j) => DeviceInfo.fromJson(j as Json)));

  Future<List<DeviceInfo>> myDevices() =>
      _c.require(_c.get('/devices/me', parse: (j) => (j as List).map((e) => DeviceInfo.fromJson(e as Json)).toList()));

  Future<void> updatePushToken(String deviceId, String token) =>
      _c.require(_c.put('/devices/$deviceId/push-token', body: {'pushToken': token}));

  Future<List<PendingDevice>> pendingDevices() =>
      _c.require(_c.get('/devices/pending', parse: (j) => (j as List).map((e) => PendingDevice.fromJson(e as Json)).toList()));

  Future<void> approveDevice(String id) => _c.require(_c.post('/devices/$id/approve'));
  Future<void> rejectDevice(String id) => _c.require(_c.post('/devices/$id/reject'));

  // ── Courses ──
  Future<List<Course>> courses() => _c.require(_c.get('/courses',
      query: {'pageNumber': 1, 'pageSize': 100}, parse: (j) => _page(j).map(Course.fromJson).toList()));

  Future<Course> createCourse(String code, String title) =>
      _c.require(_c.post('/courses', body: {'code': code, 'title': title}, parse: (j) => Course.fromJson(j as Json)));

  Future<Course> joinCourse(String joinCode) =>
      _c.require(_c.post('/courses/join', body: {'joinCode': joinCode}, parse: (j) => Course.fromJson(j as Json)));

  Future<List<Schedule>> schedules(String courseId) => _c.require(
      _c.get('/courses/$courseId/schedules', parse: (j) => (j as List).map((e) => Schedule.fromJson(e as Json)).toList()));

  Future<Schedule> addSchedule(String courseId, {required String dayOfWeek, required String start, required String end, String? room}) =>
      _c.require(_c.post('/courses/$courseId/schedules',
          body: {'dayOfWeek': dayOfWeek, 'startTime': start, 'endTime': end, 'room': room},
          parse: (j) => Schedule.fromJson(j as Json)));

  Future<void> deleteSchedule(String courseId, String scheduleId) => _c.require(_c.delete('/courses/$courseId/schedules/$scheduleId'));

  Future<List<EnrolledStudent>> students(String courseId) => _c.require(
      _c.get('/courses/$courseId/students', parse: (j) => (j as List).map((e) => EnrolledStudent.fromJson(e as Json)).toList()));

  // ── Sessions ──
  Future<Session> startSession({required String courseId, String? scheduleId, required double lat, required double lng, required double accuracyM, bool isMocked = false, int? durationMinutes, bool confirmOffSchedule = false}) =>
      _c.require(_c.post('/sessions',
          body: {
            'courseId': courseId,
            'scheduleId': scheduleId,
            'anchorLat': lat,
            'anchorLng': lng,
            'anchorAccuracyM': accuracyM,
            'isMocked': isMocked,
            'durationMinutes': durationMinutes,
            'confirmOffSchedule': confirmOffSchedule,
          },
          parse: (j) => Session.fromJson(j as Json)));

  Future<Session> session(String id) => _c.require(_c.get('/sessions/$id', parse: (j) => Session.fromJson(j as Json)));

  Future<SessionQr> qr(String sessionId) => _c.require(_c.get('/sessions/$sessionId/qr', parse: (j) => SessionQr.fromJson(j as Json)));

  Future<void> closeSession(String id) => _c.require(_c.post('/sessions/$id/close'));

  Future<int> sendPresencePrompt(String id) =>
      _c.require(_c.post('/sessions/$id/prompt', parse: (j) => ((j as Json)['recipients'] as num).toInt()));

  Future<SessionAttendance> attendance(String sessionId) =>
      _c.require(_c.get('/sessions/$sessionId/attendance', parse: (j) => SessionAttendance.fromJson(j as Json)));

  // ── Check-ins ──

  /// Returns the raw response: 201/200 accepted, 422 rejected (data still has the result),
  /// anything else is an error. Network failures throw [ApiException] with statusCode 0.
  Future<ApiResponse<CheckInResult>> checkIn(Json signedRequest) =>
      _c.post('/checkins', appCheck: true, body: signedRequest, parse: (j) => CheckInResult.fromJson(j as Json));

  Future<PresenceResult> reportPresence(String checkInId, {required String kind, double? lat, double? lng, double? accuracyM}) =>
      _c.require(_c.post('/checkins/$checkInId/presence',
          appCheck: true,
          body: {'kind': kind, 'lat': lat, 'lng': lng, 'accuracyM': accuracyM},
          parse: (j) => PresenceResult.fromJson(j as Json)));

  Future<void> review(String checkInId, {required String decision, String? note}) =>
      _c.require(_c.patch('/checkins/$checkInId/review', body: {'decision': decision, 'note': note}));

  // ── Me ──
  Future<List<TimetableEntry>> timetable({int days = 7}) => _c.require(_c.get('/me/timetable',
      query: {'days': days}, parse: (j) => (j as List).map((e) => TimetableEntry.fromJson(e as Json)).toList()));

  Future<List<CourseAttendance>> myAttendance() => _c.require(_c.get('/me/attendance',
      parse: (j) => _list((j as Json)['courses']).map(CourseAttendance.fromJson).toList()));

  static Iterable<Json> _page(Object? j) => j is List ? j.cast<Json>() : _list((j as Json)['items'] ?? j['data']);
  static List<Json> _list(Object? j) => (j as List? ?? const []).cast<Json>();
}
