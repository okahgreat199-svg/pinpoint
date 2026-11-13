import 'dart:async';

import 'package:app_links/app_links.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/auth/auth_controller.dart';
import 'core/checkin/checkin_service.dart';
import 'core/providers.dart';
import 'core/push/firebase_service.dart';
import 'core/settings/settings.dart';
import 'features/auth/device_binding_screen.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/onboarding_screen.dart';
import 'features/home/home_shell.dart';
import 'features/student/presence_prompt_screen.dart';
import 'features/student/scan_screen.dart';
import 'utils/theme/theme.dart';
import 'widgets/common.dart';

final navigatorKey = GlobalKey<NavigatorState>();

class PinpointApp extends ConsumerStatefulWidget {
  const PinpointApp({super.key});

  @override
  ConsumerState<PinpointApp> createState() => _PinpointAppState();
}

class _PinpointAppState extends ConsumerState<PinpointApp> with WidgetsBindingObserver {
  final _links = AppLinks();
  final _subs = <StreamSubscription<dynamic>>[];
  Timer? _queueTimer;

  /// A link or prompt that arrived before the user was signed in and bound.
  Object? _pending;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);

    // Deep link from the stock camera: https://<host>/c/<token> (Android App Links).
    _subs.add(_links.uriLinkStream.listen(_onLink));
    _links.getInitialLink().then((uri) => uri == null ? null : _onLink(uri));

    final firebase = ref.read(firebaseProvider);
    _subs.add(firebase.prompts.listen(_onPrompt));
    WidgetsBinding.instance.addPostFrameCallback((_) => firebase.deliverInitialMessage());

    _queueTimer = Timer.periodic(const Duration(seconds: 30), (_) => _flushQueue());
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    for (final s in _subs) {
      s.cancel();
    }
    _queueTimer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _flushQueue();
  }

  bool get _ready => switch (ref.read(authProvider)) { SignedIn s => !s.needsDeviceBinding, _ => false };

  void _onLink(Uri uri) {
    if (!uri.path.startsWith('/c/')) return;
    _pending = uri.toString();
    _deliverPending();
  }

  void _onPrompt(PresencePrompt p) {
    _pending = p;
    _deliverPending();
  }

  void _deliverPending() {
    final p = _pending;
    final nav = navigatorKey.currentState;
    if (p == null || nav == null || !_ready) return;
    final user = ref.read(currentUserProvider);
    if (!user.isStudent) {
      _pending = null;
      return;
    }
    _pending = null;
    switch (p) {
      case String link:
        nav.push(MaterialPageRoute(builder: (_) => CheckInProgressScreen(scanned: link)));
      case PresencePrompt prompt:
        nav.push(MaterialPageRoute(builder: (_) => PresencePromptScreen(prompt: prompt)));
    }
  }

  Future<void> _flushQueue() async {
    if (!_ready || !ref.read(checkInQueueProvider).isNotEmpty) return;
    final results = await ref.read(checkInServiceProvider).flushQueue();
    final ctx = navigatorKey.currentContext;
    if (results.isEmpty || ctx == null || !ctx.mounted) return;
    final r = results.last;
    showSnack(ctx, r.accepted ? 'Offline check-in sent: ${r.status.toLowerCase()}.' : 'Offline check-in rejected: ${r.message}', error: !r.accepted);
  }

  @override
  Widget build(BuildContext context) {
    final settings = ref.watch(settingsProvider);
    final auth = ref.watch(authProvider);

    // Once signed in and bound, hand over any link/prompt that arrived early.
    ref.listen(authProvider, (_, next) => WidgetsBinding.instance.addPostFrameCallback((_) {
          // Signing out (or a failed refresh) drops anything pushed on top of home.
          if (next is SignedOut) navigatorKey.currentState?.popUntil((r) => r.isFirst);
          _deliverPending();
          _flushQueue();
        }));

    final Widget home = switch (auth) {
      _ when !settings.onboarded => const OnboardingScreen(),
      AuthLoading() => const SplashScreen(),
      SignedOut() => const LoginScreen(),
      SignedIn s when s.needsDeviceBinding => const DeviceBindingScreen(),
      SignedIn() => const HomeShell(),
    };

    return MaterialApp(
      title: 'Pinpoint',
      navigatorKey: navigatorKey,
      debugShowCheckedModeBanner: false,
      themeMode: ThemeMode.system,
      theme: settings.highContrast ? IKAppTheme.highContrast(IKAppTheme.lightTheme) : IKAppTheme.lightTheme,
      darkTheme: settings.highContrast ? IKAppTheme.highContrast(IKAppTheme.darkTheme) : IKAppTheme.darkTheme,
      builder: (context, child) => MediaQuery(
        data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(settings.textScale)),
        child: child!,
      ),
      home: home,
    );
  }
}
