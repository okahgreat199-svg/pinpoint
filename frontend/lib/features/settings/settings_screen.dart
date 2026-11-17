import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../core/config.dart';
import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../core/settings/settings.dart';
import '../../core/api/models.dart';

/// BRD screen 14 (+ screen 15, the dev panel, in debug builds).
class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(settingsProvider);
    final user = ref.watch(currentUserProvider);
    final t = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(children: [
        ListTile(
          leading: const Icon(Icons.person_outline),
          title: Text(user.fullName),
          subtitle: Text([user.email, if (user.matricNo != null) user.matricNo!, humanise(user.group.name)].join(' · ')),
        ),
        const Divider(),
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
          child: Text('Text size: ${(settings.textScale * 100).round()}%', style: t.titleMedium),
        ),
        Slider(
          value: settings.textScale,
          min: 0.8,
          max: 2.0,
          divisions: 6,
          label: '${(settings.textScale * 100).round()}%',
          onChanged: (v) => ref.read(settingsProvider.notifier).setTextScale(v),
        ),
        SwitchListTile(
          secondary: const Icon(Icons.contrast),
          title: const Text('High contrast'),
          value: settings.highContrast,
          onChanged: (v) => ref.read(settingsProvider.notifier).setHighContrast(v),
        ),
        ListTile(
          leading: const Icon(Icons.privacy_tip_outlined),
          title: const Text('Privacy notice'),
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PrivacyScreen())),
        ),
        if (kDebugMode)
          ListTile(
            leading: const Icon(Icons.bug_report_outlined),
            title: const Text('Developer panel'),
            subtitle: Text('Location: ${ref.read(locationProvider).simulated.label}'),
            onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const DevPanelScreen())),
          ),
        const Divider(),
        ListTile(
          leading: const Icon(Icons.logout),
          title: const Text('Sign out'),
          onTap: () => ref.read(authProvider.notifier).logout(),
        ),
        Padding(
          padding: const EdgeInsets.all(16),
          child: Text('Server: ${AppConfig.apiBaseUrl}', style: t.bodySmall),
        ),
      ]),
    );
  }
}

class PrivacyScreen extends StatelessWidget {
  const PrivacyScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    Widget section(String title, String body) => Padding(
          padding: const EdgeInsets.only(bottom: 18),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(title, style: t.headlineSmall),
            const SizedBox(height: 6),
            Text(body, style: t.bodyLarge),
          ]),
        );
    return Scaffold(
      appBar: AppBar(title: const Text('Privacy')),
      body: ListView(padding: const EdgeInsets.all(20), children: [
        section('What we collect',
            'Your name, university email and matric number; which sessions you checked in to and when; and one location reading per check-in or presence check.'),
        section('When location is used',
            'Only at the moment you check in or answer a "still here?" prompt. The app does not track you in the background.'),
        section('How long we keep it',
            'Exact coordinates are deleted 30 days after each session. Attendance records (present, flagged, absent) are kept for the course.'),
        section('Who can see it', 'Your lecturer sees your attendance status and the reasons for any flag. Nobody else does.'),
        section('Your phone', 'A private key created on this phone signs each check-in. It never leaves the phone.'),
      ]),
    );
  }
}

/// Debug builds only: fake a location for the demo (BRD screen 15).
class DevPanelScreen extends ConsumerStatefulWidget {
  const DevPanelScreen({super.key});

  @override
  ConsumerState<DevPanelScreen> createState() => _DevPanelScreenState();
}

class _DevPanelScreenState extends ConsumerState<DevPanelScreen> {
  @override
  Widget build(BuildContext context) {
    final location = ref.read(locationProvider);
    final queue = ref.read(checkInQueueProvider).all();
    return Scaffold(
      appBar: AppBar(title: const Text('Developer panel')),
      body: ListView(children: [
        const Padding(
          padding: EdgeInsets.all(16),
          child: Text('Simulated location applies to check-ins, presence checks and the lecturer anchor. Release builds ignore it.'),
        ),
        RadioGroup<SimulatedLocation>(
          groupValue: location.simulated,
          onChanged: (v) => setState(() => location.simulated = v!),
          child: Column(children: [
            for (final s in SimulatedLocation.values)
              RadioListTile<SimulatedLocation>(
                value: s,
                title: Text(s.label),
                subtitle: s.fix == null ? null : Text('${s.fix!.lat}, ${s.fix!.lng} ±${s.fix!.accuracyM.round()} m${s.fix!.isMocked ? ' · mocked' : ''}'),
              ),
          ]),
        ),
        const Divider(),
        ListTile(title: const Text('Queued offline check-ins'), trailing: Text('${queue.length}')),
      ]),
    );
  }
}
