import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';


class Fix {
  final double lat;
  final double lng;
  final double accuracyM;
  final bool isMocked;

  const Fix(this.lat, this.lng, this.accuracyM, {this.isMocked = false});
}

class LocationException implements Exception {
  final String message;

  /// True when the fix is in Settings (permission permanently denied, or services off).
  final bool needsSettings;

  const LocationException(this.message, {this.needsSettings = false});

  @override
  String toString() => message;
}

/// Demo-only location presets (BRD screen 15). Coordinates are around the Queen Mother Building.
enum SimulatedLocation {
  off('Real GPS', null),
  onCampus('On campus (QMB)', Fix(56.45860, -2.98280, 8)),
  away200m('200 m from QMB', Fix(56.46040, -2.98280, 8)),
  offCampus('Off campus (the Law)', Fix(56.46890, -2.98740, 8)),
  mocked('Mock-location app', Fix(56.45860, -2.98280, 5, isMocked: true)),
  poorAccuracy('Poor accuracy (120 m)', Fix(56.45860, -2.98280, 120));

  final String label;
  final Fix? fix;

  const SimulatedLocation(this.label, this.fix);
}

class LocationService {
  /// Only honoured in debug builds, so a release APK can never fake a fix.
  SimulatedLocation simulated = SimulatedLocation.off;

  /// A fix this good ends sampling early. The server flags anything worse than 50 m.
  static const _goodEnoughM = 25.0;

  /// How long a check-in waits for a better fix. The QR token is valid for 15–30 s, so this must stay short.
  static const _sampleWindow = Duration(seconds: 8);

  StreamSubscription<Position>? _warm;
  Position? _latest;

  static const _streamSettings = LocationSettings(accuracy: LocationAccuracy.best, distanceFilter: 0);

  /// Starts GPS while the student lines up the QR code. The first fixes a phone reports are often
  /// Wi-Fi or cell estimates (±100–500 m); a few seconds of GPS usually brings that under 20 m.
  Future<void> warmUp() async {
    if (_warm != null || (kDebugMode && simulated.fix != null) || !await hasPermission()) return;
    _warm = Geolocator.getPositionStream(locationSettings: _streamSettings).listen(_keep, onError: (_) {});
  }

  Future<void> coolDown() async {
    await _warm?.cancel();
    _warm = null;
  }

  void _keep(Position p) => _latest = p;

  Future<Fix> current() async {
    if (kDebugMode && simulated.fix != null) return simulated.fix!;

    if (!await Geolocator.isLocationServiceEnabled()) {
      throw const LocationException('Location is switched off. Turn it on to check in.', needsSettings: true);
    }

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) permission = await Geolocator.requestPermission();
    if (permission == LocationPermission.denied) {
      throw const LocationException('Location permission is needed to confirm you are on campus.');
    }
    if (permission == LocationPermission.deniedForever) {
      throw const LocationException('Location permission is blocked. Allow it in Settings.', needsSettings: true);
    }
    // Android 12+ lets people grant "approximate" location, which is only good to a couple of kilometres.
    if (await Geolocator.getLocationAccuracy() == LocationAccuracyStatus.reduced) {
      throw const LocationException('Pinpoint needs your precise location. In Settings, open Location and turn on "Use precise location".',
          needsSettings: true);
    }

    final warm = _latest;
    if (warm != null && _fresh(warm, const Duration(seconds: 20)) && warm.accuracy <= _goodEnoughM) return _fix(warm);

    final best = await _sample();
    if (best != null) return _fix(best);

    // No fix at all in the window (deep indoors): a recent last-known fix is better than nothing; the server flags poor accuracy.
    final last = await Geolocator.getLastKnownPosition();
    if (last != null && _fresh(last, const Duration(minutes: 2))) return _fix(last);
    throw const LocationException('Could not get your location. Move near a window and try again.');
  }

  /// Listens for up to [_sampleWindow] and returns the most accurate fix seen, stopping early once one is good enough.
  Future<Position?> _sample() async {
    Position? best = _latest != null && _fresh(_latest!, const Duration(seconds: 20)) ? _latest : null;
    final done = Completer<void>();
    final sub = Geolocator.getPositionStream(locationSettings: _streamSettings).listen((p) {
      _keep(p);
      if (best == null || p.accuracy < best!.accuracy) best = p;
      if (p.accuracy <= _goodEnoughM && !done.isCompleted) done.complete();
    }, onError: (_) {});
    await done.future.timeout(_sampleWindow, onTimeout: () {});
    await sub.cancel();
    return best;
  }

  static bool _fresh(Position p, Duration maxAge) => DateTime.now().difference(p.timestamp) < maxAge;

  static Fix _fix(Position p) => Fix(p.latitude, p.longitude, p.accuracy, isMocked: p.isMocked);

  Future<bool> hasPermission() async {
    final p = await Geolocator.checkPermission();
    return p == LocationPermission.always || p == LocationPermission.whileInUse;
  }

  Future<void> requestPermission() => Geolocator.requestPermission();

  Future<void> openSettings() => Geolocator.openAppSettings();
}
