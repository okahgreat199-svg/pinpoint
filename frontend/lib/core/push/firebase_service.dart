import 'dart:async';

import 'package:firebase_app_check/firebase_app_check.dart';
import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

/// The FCM data payload for the 70% presence prompt.
class PresencePrompt {
  final String sessionId;
  final String checkInId;
  final DateTime? respondBy;

  const PresencePrompt(this.sessionId, this.checkInId, this.respondBy);

  static PresencePrompt? fromData(Map<String, dynamic> data) {
    if (data['type'] != 'presence_prompt' || data['checkInId'] == null) return null;
    return PresencePrompt(data['sessionId'] as String? ?? '', data['checkInId'] as String,
        DateTime.tryParse(data['respondBy'] as String? ?? '')?.toLocal());
  }
}

/// Firebase is optional: without google-services.json the app still runs, just with
/// no push and no App Check header (fine while the API has AppCheck__Enabled=false).
class FirebaseService {
  bool _ready = false;
  final _prompts = StreamController<PresencePrompt>.broadcast();

  bool get isReady => _ready;

  /// Prompts from a notification tap or a foreground message.
  Stream<PresencePrompt> get prompts => _prompts.stream;

  Future<void> init() async {
    try {
      await Firebase.initializeApp();
      await FirebaseAppCheck.instance.activate(
        providerAndroid: kDebugMode ? const AndroidDebugProvider() : const AndroidPlayIntegrityProvider(),
      );
      _ready = true;
    } catch (e) {
      debugPrint('Firebase not configured, push and App Check disabled: $e');
      return;
    }

    FirebaseMessaging.onMessage.listen(_handle);
    FirebaseMessaging.onMessageOpenedApp.listen(_handle);
  }

  /// Call once the navigator exists, so a cold start from the notification lands on the prompt.
  Future<void> deliverInitialMessage() async {
    if (!_ready) return;
    final initial = await FirebaseMessaging.instance.getInitialMessage();
    if (initial != null) _handle(initial);
  }

  void _handle(RemoteMessage m) {
    final prompt = PresencePrompt.fromData(m.data);
    if (prompt != null) _prompts.add(prompt);
  }

  Future<String?> appCheckToken() async {
    if (!_ready) return null;
    try {
      return await FirebaseAppCheck.instance.getToken();
    } catch (e) {
      debugPrint('App Check token unavailable: $e');
      return null;
    }
  }

  Future<String?> pushToken() async {
    if (!_ready) return null;
    try {
      await FirebaseMessaging.instance.requestPermission();
      // The newer register()/onRegistered API hands out installation IDs; the API's
      // FirebaseAdmin sends to registration tokens, which is what getToken() returns.
      // ignore: deprecated_member_use
      return await FirebaseMessaging.instance.getToken();
    } catch (e) {
      debugPrint('FCM token unavailable: $e');
      return null;
    }
  }

  // ignore: deprecated_member_use
  Stream<String> get pushTokenRefreshes => _ready ? FirebaseMessaging.instance.onTokenRefresh : const Stream.empty();
}
