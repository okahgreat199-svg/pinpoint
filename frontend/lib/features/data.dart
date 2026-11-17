import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api/api_client.dart';
import '../core/api/models.dart';
import '../core/auth/auth_controller.dart';
import '../core/providers.dart';
import '../core/settings/settings.dart';

/// Timetable with a cached copy for offline launches (BRD screen 4).
class Timetable {
  final List<TimetableEntry> entries;

  /// Set when the API was unreachable and [entries] came from the cache.
  final DateTime? cachedAt;

  const Timetable(this.entries, [this.cachedAt]);
}

final timetableProvider = FutureProvider.autoDispose<Timetable>((ref) async {
  final prefs = ref.watch(prefsProvider);
  final user = ref.watch(currentUserProvider);
  final key = 'cache.timetable.${user.id}';

  try {
    final entries = await ref.watch(apiProvider).timetable(days: 7);
    await prefs.setString(key, jsonEncode({'at': DateTime.now().toUtc().toIso8601String(), 'items': entries.map((e) => e.toJson()).toList()}));
    return Timetable(entries);
  } on ApiException catch (e) {
    final cached = prefs.getString(key);
    if (!e.isNetwork || cached == null) rethrow;
    final j = jsonDecode(cached) as Map<String, dynamic>;
    return Timetable(
      (j['items'] as List).map((e) => TimetableEntry.fromJson(e as Json)).toList(),
      DateTime.parse(j['at'] as String).toLocal(),
    );
  }
});

final coursesProvider = FutureProvider.autoDispose<List<Course>>((ref) => ref.watch(apiProvider).courses());

final schedulesProvider = FutureProvider.autoDispose.family<List<Schedule>, String>((ref, courseId) => ref.watch(apiProvider).schedules(courseId));

final studentsProvider = FutureProvider.autoDispose.family<List<EnrolledStudent>, String>((ref, courseId) => ref.watch(apiProvider).students(courseId));

final pendingDevicesProvider = FutureProvider.autoDispose<List<PendingDevice>>((ref) => ref.watch(apiProvider).pendingDevices());

final attendanceProvider = FutureProvider.autoDispose.family<SessionAttendance, String>((ref, sessionId) => ref.watch(apiProvider).attendance(sessionId));

final myAttendanceProvider = FutureProvider.autoDispose<List<CourseAttendance>>((ref) => ref.watch(apiProvider).myAttendance());
