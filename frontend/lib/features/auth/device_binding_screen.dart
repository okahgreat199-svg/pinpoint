import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';

/// BRD screen 3: link this phone (ECDSA keypair) to the student account.
class DeviceBindingScreen extends ConsumerStatefulWidget {
  const DeviceBindingScreen({super.key});

  @override
  ConsumerState<DeviceBindingScreen> createState() => _DeviceBindingScreenState();
}

class _DeviceBindingScreenState extends ConsumerState<DeviceBindingScreen> {
  bool _busy = false;

  Future<void> _bind() async {
    setState(() => _busy = true);
    await guarded(context, () => ref.read(authProvider.notifier).bindDevice());
    if (mounted) setState(() => _busy = false);
  }

  Future<void> _checkAgain() async {
    setState(() => _busy = true);
    await guarded(context, () => ref.read(authProvider.notifier).refreshProfile());
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final auth = ref.watch(authProvider) as SignedIn;
    final pending = auth.thisDeviceStatus == 'Pending';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Link this phone'),
        actions: [IconButton(tooltip: 'Sign out', icon: const Icon(Icons.logout), onPressed: () => ref.read(authProvider.notifier).logout())],
      ),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.all(30),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Center(
                child: Container(
                  width: 96,
                  height: 96,
                  decoration: BoxDecoration(
                      color: (pending ? IKColors.warning : IKColors.primary).withValues(alpha: 0.12), shape: BoxShape.circle),
                  child: Icon(pending ? Icons.hourglass_top_rounded : Icons.phonelink_lock_rounded,
                      size: 44, color: pending ? IKColors.warning : IKColors.primary),
                ),
              ),
              const SizedBox(height: 28),
              Text(pending ? 'Waiting for approval' : 'Hi ${auth.user.firstName}, link this phone',
                  style: t.headlineLarge, textAlign: TextAlign.center),
              const SizedBox(height: 12),
              Text(
                pending
                    ? 'Your account is already linked to another phone, so a lecturer needs to approve this one. '
                        'Ask them to open Device requests. Until then you can\'t check in from here.'
                    : 'Pinpoint creates a private key that never leaves this phone and signs every check-in with it. '
                        'That way nobody can check in for you from another phone.',
                style: t.bodyLarge?.copyWith(color: t.bodyMedium?.color),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),
              if (pending)
                PrimaryButton(label: 'Check again', icon: Icons.refresh, busy: _busy, onPressed: _checkAgain)
              else
                PrimaryButton(label: 'Link this phone', icon: Icons.link, busy: _busy, onPressed: _bind),
              const SizedBox(height: 24),
              const InfoNote(icon: Icons.lock_outline_rounded, text: locationRetentionNote),
            ]),
          ),
        ),
      ),
    );
  }
}
