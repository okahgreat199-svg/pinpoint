import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

/// Signed check-ins that failed on the network, kept for a retry (BRD key flow 1).
/// The request is stored already signed, so a retry sends exactly what was scanned and when;
/// the server still judges the QR time step, so a late retry comes back as ExpiredToken.
class QueuedCheckIn {
  final Map<String, dynamic> request;
  final DateTime queuedAt;

  const QueuedCheckIn(this.request, this.queuedAt);

  Map<String, dynamic> toJson() => {'request': request, 'queuedAt': queuedAt.toUtc().toIso8601String()};

  factory QueuedCheckIn.fromJson(Map<String, dynamic> j) =>
      QueuedCheckIn((j['request'] as Map).cast<String, dynamic>(), DateTime.parse(j['queuedAt'] as String).toLocal());
}

class CheckInQueue {
  static const _key = 'offline.checkins';

  /// Older than this and the QR step is long gone; drop instead of retrying.
  static const maxAge = Duration(hours: 2);

  final SharedPreferences _prefs;

  CheckInQueue(this._prefs);

  List<QueuedCheckIn> all() {
    final raw = _prefs.getStringList(_key) ?? const [];
    return raw
        .map((s) => QueuedCheckIn.fromJson(jsonDecode(s) as Map<String, dynamic>))
        .where((q) => DateTime.now().difference(q.queuedAt) < maxAge)
        .toList();
  }

  bool get isNotEmpty => all().isNotEmpty;

  Future<void> add(Map<String, dynamic> request) async {
    final items = all()..add(QueuedCheckIn(request, DateTime.now()));
    await _save(items);
  }

  Future<void> remove(QueuedCheckIn item) async {
    final items = all()..removeWhere((q) => q.request['clientTime'] == item.request['clientTime']);
    await _save(items);
  }

  Future<void> _save(List<QueuedCheckIn> items) =>
      _prefs.setStringList(_key, items.map((q) => jsonEncode(q.toJson())).toList());
}
