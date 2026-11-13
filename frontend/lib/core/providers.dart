import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/api_client.dart';
import 'api/seat_api.dart';
import 'auth/token_store.dart';
import 'device/device_keys.dart';
import 'location/location_service.dart';
import 'offline/checkin_queue.dart';
import 'push/firebase_service.dart';
import 'settings/settings.dart';

/// Overridden in main() after Firebase has been initialised (or failed to).
final firebaseProvider = Provider<FirebaseService>((ref) => throw UnimplementedError());

final tokenStoreProvider = Provider<TokenStore>((ref) => TokenStore());
final deviceKeysProvider = Provider<DeviceKeys>((ref) => DeviceKeys());
final locationProvider = Provider<LocationService>((ref) => LocationService());
final checkInQueueProvider = Provider<CheckInQueue>((ref) => CheckInQueue(ref.watch(prefsProvider)));

final apiClientProvider = Provider<ApiClient>((ref) {
  final firebase = ref.watch(firebaseProvider);
  return ApiClient(ref.watch(tokenStoreProvider), firebase.appCheckToken);
});

final apiProvider = Provider<SeatApi>((ref) => SeatApi(ref.watch(apiClientProvider)));
