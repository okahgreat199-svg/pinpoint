import 'dart:async';

import 'package:android_id/android_id.dart';
import 'package:device_info_plus/device_info_plus.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../api/models.dart';
import '../providers.dart';

sealed class AuthState {
  const AuthState();
}

class AuthLoading extends AuthState {
  const AuthLoading();
}

class SignedOut extends AuthState {
  final String? message;
  const SignedOut([this.message]);
}

class SignedIn extends AuthState {
  final UserProfile user;

  /// Status of the key held on *this* phone (Active, Pending, Revoked), or null if none is bound.
  /// Differs from [UserProfile.deviceStatus] after a reinstall: the account has an Active device,
  /// but this install's new key is still Pending.
  final String? thisDeviceStatus;

  const SignedIn(this.user, this.thisDeviceStatus);

  /// BR-4: students can only check in from an Active device. Lecturers aren't gated on it.
  bool get needsDeviceBinding => user.isStudent && thisDeviceStatus != 'Active';
}

class AuthController extends Notifier<AuthState> {
  StreamSubscription<String>? _pushRefresh;

  @override
  AuthState build() {
    ref.read(apiClientProvider).onSessionExpired = () => _signOutLocally('Your session expired. Sign in again.');
    ref.onDispose(() => _pushRefresh?.cancel());
    Future.microtask(_restore);
    return const AuthLoading();
  }

  UserProfile? get user => switch (state) { SignedIn(:final user) => user, _ => null };

  Future<void> _restore() async {
    final tokens = ref.read(tokenStoreProvider);
    if (!await tokens.hasRefreshToken()) {
      state = const SignedOut();
      return;
    }
    try {
      await _enter(await ref.read(apiProvider).me());
    } on ApiException catch (e) {
      // Offline at launch: tokens are kept, so signing in again once online is quick.
      state = SignedOut(e.isNetwork ? e.message : null);
    }
  }

  Future<void> login(String email, String password) async {
    final result = await ref.read(apiProvider).login(email.trim(), password);
    await ref.read(tokenStoreProvider).save(result.accessToken, result.refreshToken);
    await _enter(result.user);
  }

  Future<void> register({
    required String email,
    required String fullName,
    required String password,
    required UserGroup role,
    String? matricNo,
    String? inviteCode,
  }) async {
    final result = await ref.read(apiProvider).register(
        email: email.trim(), fullName: fullName.trim(), password: password, role: role, matricNo: matricNo?.trim(), inviteCode: inviteCode?.trim());
    await ref.read(tokenStoreProvider).save(result.accessToken, result.refreshToken);
    await _enter(result.user);
  }

  Future<void> refreshProfile() async {
    await _enter(await ref.read(apiProvider).me());
  }

  Future<void> logout() async {
    final tokens = ref.read(tokenStoreProvider);
    final refresh = await tokens.refreshToken();
    if (refresh != null) {
      try {
        await ref.read(apiProvider).logout(refresh);
      } on ApiException {
        // Best effort; the refresh token expires on its own.
      }
    }
    await _signOutLocally();
  }

  /// Device binding (BRD screen 3). Generates the keypair on first call and registers it.
  /// Returns the device status the server assigned: Active or Pending.
  Future<String> bindDevice() async {
    final keys = ref.read(deviceKeysProvider);
    final firebase = ref.read(firebaseProvider);
    final publicKey = await keys.ensurePublicKey();

    String model = 'Android device';
    String? hardwareId;
    if (defaultTargetPlatform == TargetPlatform.android) {
      final info = await DeviceInfoPlugin().androidInfo;
      model = '${info.manufacturer} ${info.model}';
      hardwareId = await const AndroidId().getId();
    }

    final device = await ref.read(apiProvider).registerDevice(
          publicKey: publicKey,
          model: model,
          hardwareId: hardwareId,
          pushToken: await firebase.pushToken(),
        );
    await keys.saveDeviceId(device.id);
    await refreshProfile();
    return device.status;
  }

  Future<void> _enter(UserProfile user) async {
    final keys = ref.read(deviceKeysProvider);
    String? thisDevice;

    if (user.isStudent) {
      final localId = await keys.deviceId();
      if (localId != null) {
        final mine = await ref.read(apiProvider).myDevices();
        thisDevice = mine.where((d) => d.id == localId).firstOrNull?.status;
        // Revoked or reset by an admin: the local key is useless, bind a fresh one.
        if (thisDevice == null || thisDevice == 'Revoked') {
          await keys.reset();
          thisDevice = null;
        }
      }
    }

    state = SignedIn(user, thisDevice);
    _syncPushToken();
  }

  /// Keeps the server's FCM token current so the 70% prompt reaches this phone.
  Future<void> _syncPushToken() async {
    final firebase = ref.read(firebaseProvider);
    final deviceId = await ref.read(deviceKeysProvider).deviceId();
    if (deviceId == null || !firebase.isReady) return;

    Future<void> push(String token) async {
      try {
        await ref.read(apiProvider).updatePushToken(deviceId, token);
      } on ApiException catch (e) {
        debugPrint('Push token sync failed: $e');
      }
    }

    final token = await firebase.pushToken();
    if (token != null) await push(token);
    await _pushRefresh?.cancel();
    _pushRefresh = firebase.pushTokenRefreshes.listen(push);
  }

  Future<void> _signOutLocally([String? message]) async {
    await ref.read(tokenStoreProvider).clear();
    await _pushRefresh?.cancel();
    state = SignedOut(message);
  }
}

final authProvider = NotifierProvider<AuthController, AuthState>(AuthController.new);

/// The signed-in user; only read this below the auth gate.
final currentUserProvider = Provider<UserProfile>((ref) {
  final s = ref.watch(authProvider);
  if (s is SignedIn) return s.user;
  throw StateError('Not signed in');
});
