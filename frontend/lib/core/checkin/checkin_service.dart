import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../device/device_keys.dart';
import '../providers.dart';

/// What the scan screen shows after an attempt.
sealed class CheckInOutcome {
  const CheckInOutcome();
}

class CheckInDone extends CheckInOutcome {
  final CheckInResult result;
  const CheckInDone(this.result);
}

/// No network: saved and will be retried; the server decides later whether it was in time.
class CheckInQueued extends CheckInOutcome {
  const CheckInQueued();
}

class CheckInFailed extends CheckInOutcome {
  final String message;
  const CheckInFailed(this.message);
}

class CheckInService {
  final Ref _ref;

  CheckInService(this._ref);

  /// BRD key flow 1: scanned string → fresh fix → sign → POST.
  Future<CheckInOutcome> checkIn(String scanned) async {
    final keys = _ref.read(deviceKeysProvider);
    final deviceId = await keys.deviceId();
    if (deviceId == null) return const CheckInFailed('This phone is not linked to your account yet.');

    final fix = await _ref.read(locationProvider).current();   // throws LocationException
    final clientTime = DateTime.now().toUtc().toIso8601String();
    final token = scanned.trim();

    final request = <String, dynamic>{
      'token': token,
      'lat': fix.lat,
      'lng': fix.lng,
      'accuracyM': fix.accuracyM,
      'isMocked': fix.isMocked,
      'clientTime': clientTime,
      'deviceId': deviceId,
      'signature': await keys.sign(canonicalCheckInPayload(
        token: token,
        lat: fix.lat,
        lng: fix.lng,
        accuracyM: fix.accuracyM,
        isMocked: fix.isMocked,
        clientTime: clientTime,
        deviceId: deviceId,
      )),
    };

    return _submit(request, queueOnFailure: true);
  }

  /// Retries queued attempts. Returns the results that came back (accepted or rejected).
  Future<List<CheckInResult>> flushQueue() async {
    final queue = _ref.read(checkInQueueProvider);
    final results = <CheckInResult>[];
    for (final item in queue.all()) {
      final outcome = await _submit(item.request, queueOnFailure: false);
      switch (outcome) {
        case CheckInDone(:final result):
          results.add(result);
          await queue.remove(item);
        case CheckInFailed():
          await queue.remove(item);   // a definite no from the server; don't keep retrying
        case CheckInQueued():
          return results;             // still offline, try again later
      }
    }
    return results;
  }

  Future<CheckInOutcome> _submit(Map<String, dynamic> request, {required bool queueOnFailure}) async {
    try {
      final r = await _ref.read(apiProvider).checkIn(request);
      // 201 accepted, 200 duplicate, 422 rejected (the body still says why).
      if ((r.isSuccess || r.statusCode == 422) && r.data != null) return CheckInDone(r.data!);
      if (r.statusCode >= 500) return await _queue(request, queueOnFailure);
      return CheckInFailed(r.message);
    } on ApiException catch (e) {
      if (e.isRetryable) return await _queue(request, queueOnFailure);
      return CheckInFailed(e.message);
    }
  }

  Future<CheckInOutcome> _queue(Map<String, dynamic> request, bool enabled) async {
    if (enabled) await _ref.read(checkInQueueProvider).add(request);
    return const CheckInQueued();
  }

  /// BRD key flow 3: the 70% prompt. Takes a foreground fix and reports it.
  Future<PresenceResult> answerPrompt(String checkInId) async {
    final fix = await _ref.read(locationProvider).current();
    return _ref.read(apiProvider).reportPresence(checkInId, kind: 'Prompt70', lat: fix.lat, lng: fix.lng, accuracyM: fix.accuracyM);
  }
}

final checkInServiceProvider = Provider<CheckInService>((ref) => CheckInService(ref));
