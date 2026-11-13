import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'app.dart';
import 'core/providers.dart';
import 'core/push/firebase_service.dart';
import 'core/settings/settings.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final prefs = await SharedPreferences.getInstance();
  final firebase = FirebaseService();
  await firebase.init();

  runApp(ProviderScope(
    overrides: [
      prefsProvider.overrideWithValue(prefs),
      firebaseProvider.overrideWithValue(firebase),
    ],
    // Errors are shown with a retry button; don't silently re-fire API calls.
    retry: (_, __) => null,
    child: const PinpointApp(),
  ));
}
