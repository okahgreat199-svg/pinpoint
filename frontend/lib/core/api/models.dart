// Hand-written mirrors of the API DTOs (api/DTOs/Responses). Only the fields the app uses.

typedef Json = Map<String, dynamic>;

DateTime _dt(Object? v) => DateTime.parse(v as String).toLocal();
DateTime? _dtOrNull(Object? v) => v == null ? null : _dt(v);
double? _d(Object? v) => (v as num?)?.toDouble();
List<String> _strings(Object? v) => (v as List? ?? const []).cast<String>();
List<T> _list<T>(Object? v, T Function(Json) f) => (v as List? ?? const []).map((e) => f(e as Json)).toList();

enum UserGroup { admin, lecturer, student }

UserGroup _group(String s) => UserGroup.values.firstWhere((g) => g.name == s.toLowerCase(), orElse: () => UserGroup.student);

class AuthResult {
  final String accessToken;
  final String refreshToken;
  final UserProfile user;

  AuthResult.fromJson(Json j)
      : accessToken = j['accessToken'],
        refreshToken = j['refreshToken'],
        user = UserProfile.fromJson(j['user']);
}

class UserProfile {
  final String id;
  final String email;
  final String fullName;
  final UserGroup group;
  final String? matricNo;

  /// Active, Pending, Revoked, or null when no device is bound yet.
  final String? deviceStatus;

  const UserProfile({required this.id, required this.email, required this.fullName, required this.group, this.matricNo, this.deviceStatus});

  UserProfile.fromJson(Json j)
      : id = j['id'],
        email = j['email'],
        fullName = j['fullName'],
        group = _group(j['userGroup']),
        matricNo = j['matricNo'],
        deviceStatus = j['deviceStatus'];

  bool get isLecturer => group == UserGroup.lecturer || group == UserGroup.admin;
  bool get isStudent => group == UserGroup.student;
  /// "Dr Jane Smith" greets as "Dr Smith", "Jane Smith" as "Jane".
  String get firstName {
    final parts = fullName.trim().split(RegExp(r'\s+'));
    const titles = {'dr', 'prof', 'professor', 'mr', 'mrs', 'ms', 'miss', 'mx'};
    if (parts.length > 1 && titles.contains(parts.first.toLowerCase().replaceAll('.', ''))) {
      return '${parts.first} ${parts.last}';
    }
    return parts.first;
  }
}

class DeviceInfo {
  final String id;
  final String model;
  final String status;
  final DateTime boundAt;

  DeviceInfo.fromJson(Json j)
      : id = j['id'],
        model = j['model'],
        status = j['status'],
        boundAt = _dt(j['boundAt']);
}

class PendingDevice {
  final String id;
  final String studentName;
  final String? matricNo;
  final String model;
  final String? currentDeviceModel;
  final DateTime boundAt;

  PendingDevice.fromJson(Json j)
      : id = j['id'],
        studentName = j['studentName'],
        matricNo = j['matricNo'],
        model = j['model'],
        currentDeviceModel = j['currentDeviceModel'],
        boundAt = _dt(j['boundAt']);
}

class Course {
  final String id;
  final String code;
  final String title;
  final String lecturerName;
  final String? joinCode;
  final int studentCount;

  Course.fromJson(Json j)
      : id = j['id'],
        code = j['code'],
        title = j['title'],
        lecturerName = j['lecturerName'] ?? '',
        joinCode = j['joinCode'],
        studentCount = (j['studentCount'] as num?)?.toInt() ?? 0;
}

class EnrolledStudent {
  final String studentId;
  final String fullName;
  final String email;
  final String? matricNo;

  EnrolledStudent.fromJson(Json j)
      : studentId = j['studentId'],
        fullName = j['fullName'],
        email = j['email'],
        matricNo = j['matricNo'];
}

const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

class Schedule {
  final String id;
  final String dayOfWeek;
  final String startTime;
  final String endTime;
  final String? room;
  final double? roomLat;
  final double? roomLng;

  Schedule.fromJson(Json j)
      : id = j['id'],
        dayOfWeek = j['dayOfWeek'],
        startTime = j['startTime'],
        endTime = j['endTime'],
        room = j['room'],
        roomLat = _d(j['roomLat']),
        roomLng = _d(j['roomLng']);

  String get timeRange => '${startTime.substring(0, 5)}–${endTime.substring(0, 5)}';
}

class TimetableEntry {
  final String courseId;
  final String courseCode;
  final String courseTitle;
  final String? scheduleId;
  final String? sessionId;
  final String? room;
  final DateTime startsAt;
  final DateTime endsAt;

  /// Upcoming, Open, Closed, Ended (TimetableStatus).
  final String status;
  final String? myCheckInStatus;

  TimetableEntry.fromJson(Json j)
      : courseId = j['courseId'],
        courseCode = j['courseCode'],
        courseTitle = j['courseTitle'],
        scheduleId = j['scheduleId'],
        sessionId = j['sessionId'],
        room = j['room'],
        startsAt = _dt(j['startsAt']),
        endsAt = _dt(j['endsAt']),
        status = j['status'],
        myCheckInStatus = j['myCheckInStatus'];

  Json toJson() => {
        'courseId': courseId,
        'courseCode': courseCode,
        'courseTitle': courseTitle,
        'scheduleId': scheduleId,
        'sessionId': sessionId,
        'room': room,
        'startsAt': startsAt.toUtc().toIso8601String(),
        'endsAt': endsAt.toUtc().toIso8601String(),
        'status': status,
        'myCheckInStatus': myCheckInStatus,
      };

  bool get isOpen => status == 'Open';
}

class Session {
  final String id;
  final String courseId;
  final String courseCode;
  final String courseTitle;
  final String? room;
  final DateTime startsAt;
  final DateTime endsAt;
  final String status;
  final double anchorLat;
  final double anchorLng;
  final DateTime? promptSentAt;

  Session.fromJson(Json j)
      : id = j['id'],
        courseId = j['courseId'],
        courseCode = j['courseCode'],
        courseTitle = j['courseTitle'],
        room = j['room'],
        startsAt = _dt(j['startsAt']),
        endsAt = _dt(j['endsAt']),
        status = j['status'],
        anchorLat = _d(j['anchorLat'])!,
        anchorLng = _d(j['anchorLng'])!,
        promptSentAt = _dtOrNull(j['promptSentAt']);

  bool get isOpen => status == 'Open';
}

class SessionQr {
  final String token;
  final String qrPayload;
  final String manualCode;
  final int step;
  final int stepSeconds;
  final int secondsRemaining;
  final DateTime endsAt;
  final int checkedInCount;
  final int enrolledCount;

  SessionQr.fromJson(Json j)
      : token = j['token'],
        qrPayload = (j['qrPayload'] as String?)?.isNotEmpty == true ? j['qrPayload'] : j['token'],
        manualCode = j['manualCode'],
        step = (j['step'] as num).toInt(),
        stepSeconds = (j['stepSeconds'] as num).toInt(),
        secondsRemaining = (j['secondsRemaining'] as num).toInt(),
        endsAt = _dt(j['endsAt']),
        checkedInCount = (j['checkedInCount'] as num).toInt(),
        enrolledCount = (j['enrolledCount'] as num).toInt();
}

class SessionAttendance {
  final Session session;
  final int enrolled;
  final int present;
  final int flagged;
  final int absent;
  final List<RosterRow> roster;
  final List<RejectedAttempt> rejected;

  SessionAttendance.fromJson(Json j)
      : session = Session.fromJson(j['session']),
        enrolled = (j['enrolled'] as num).toInt(),
        present = (j['present'] as num).toInt(),
        flagged = (j['flagged'] as num).toInt(),
        absent = (j['absent'] as num).toInt(),
        roster = _list(j['roster'], RosterRow.fromJson),
        rejected = _list(j['rejected'], RejectedAttempt.fromJson);
}

class RosterRow {
  final String studentId;
  final String fullName;
  final String? matricNo;
  final String? checkInId;

  /// Present, Flagged or Absent.
  final String status;
  final List<String> flags;
  final DateTime? at;
  final double? distanceFromAnchorM;
  final double? accuracyM;
  final bool isMocked;
  final int rejectedAttempts;
  final DateTime? reviewedAt;
  final String? reviewNote;
  final List<PresenceEvent> presenceEvents;

  RosterRow.fromJson(Json j)
      : studentId = j['studentId'],
        fullName = j['fullName'],
        matricNo = j['matricNo'],
        checkInId = j['checkInId'],
        status = j['status'],
        flags = _strings(j['flags']),
        at = _dtOrNull(j['at']),
        distanceFromAnchorM = _d(j['distanceFromAnchorM']),
        accuracyM = _d(j['accuracyM']),
        isMocked = j['isMocked'] ?? false,
        rejectedAttempts = (j['rejectedAttempts'] as num?)?.toInt() ?? 0,
        reviewedAt = _dtOrNull(j['reviewedAt']),
        reviewNote = j['reviewNote'],
        presenceEvents = _list(j['presenceEvents'], PresenceEvent.fromJson);
}

class PresenceEvent {
  final String kind;
  final String result;
  final DateTime at;

  PresenceEvent.fromJson(Json j)
      : kind = j['kind'],
        result = j['result'],
        at = _dt(j['at']);
}

class RejectedAttempt {
  final String fullName;
  final DateTime at;
  final String reason;

  RejectedAttempt.fromJson(Json j)
      : fullName = j['fullName'],
        at = _dt(j['at']),
        reason = j['reason'];
}

class CheckInResult {
  final String? checkInId;
  final String? sessionId;

  /// Present, Flagged, Rejected.
  final String status;
  final String? rejectionReason;
  final List<String> flags;
  final String message;
  final bool isDuplicate;
  final double? distanceFromAnchorM;
  final double? anchorLat;
  final double? anchorLng;
  final double? geofenceRadiusM;
  final DateTime? sessionEndsAt;

  CheckInResult.fromJson(Json j)
      : checkInId = j['checkInId'],
        sessionId = j['sessionId'],
        status = j['status'],
        rejectionReason = j['rejectionReason'],
        flags = _strings(j['flags']),
        message = j['message'] ?? '',
        isDuplicate = j['isDuplicate'] ?? false,
        distanceFromAnchorM = _d(j['distanceFromAnchorM']),
        anchorLat = _d(j['anchorLat']),
        anchorLng = _d(j['anchorLng']),
        geofenceRadiusM = _d(j['geofenceRadiusM']),
        sessionEndsAt = _dtOrNull(j['sessionEndsAt']);

  bool get accepted => status != 'Rejected';
}

class PresenceResult {
  final String result;
  final String checkInStatus;

  PresenceResult.fromJson(Json j)
      : result = j['result'],
        checkInStatus = j['checkInStatus'];
}

class CourseAttendance {
  final String courseId;
  final String courseCode;
  final String courseTitle;
  final int totalSessions;
  final int present;
  final int flagged;
  final int absent;
  final double percentage;
  final List<MySessionAttendance> sessions;

  CourseAttendance.fromJson(Json j)
      : courseId = j['courseId'],
        courseCode = j['courseCode'],
        courseTitle = j['courseTitle'],
        totalSessions = (j['totalSessions'] as num).toInt(),
        present = (j['present'] as num).toInt(),
        flagged = (j['flagged'] as num).toInt(),
        absent = (j['absent'] as num).toInt(),
        percentage = _d(j['attendancePercentage']) ?? 0,
        sessions = _list(j['sessions'], MySessionAttendance.fromJson);
}

class MySessionAttendance {
  final String sessionId;
  final DateTime startsAt;
  final String outcome;
  final List<String> flags;
  final String? lastRejectionReason;
  final bool reviewed;

  MySessionAttendance.fromJson(Json j)
      : sessionId = j['sessionId'],
        startsAt = _dt(j['startsAt']),
        outcome = j['outcome'],
        flags = _strings(j['flags']),
        lastRejectionReason = j['lastRejectionReason'],
        reviewed = j['reviewed'] ?? false;
}

/// Turns enum names from the API ("FarFromLecturer") into readable text.
String humanise(String value) => value.replaceAllMapped(RegExp(r'(?<=[a-z])(?=[A-Z])'), (_) => ' ').toLowerCase().replaceFirstMapped(RegExp(r'^.'), (m) => m[0]!.toUpperCase());
