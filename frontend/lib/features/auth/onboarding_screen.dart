import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../core/settings/settings.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';

/// BRD screen 1: explain why location is needed before asking for it.
class OnboardingScreen extends ConsumerStatefulWidget {
  /// Opened again from the login screen: just closes at the end.
  final bool replay;

  const OnboardingScreen({super.key, this.replay = false});

  @override
  ConsumerState<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends ConsumerState<OnboardingScreen> {
  final _pages = PageController();
  int _page = 0;

  static const _steps = [
    (Icons.qr_code_scanner_rounded, 'Scan to check in', 'Your lecturer shows a QR code that changes every few seconds. Scan it with Pinpoint, or with your camera, to check in.'),
    (Icons.phone_android_rounded, 'One phone, one student', 'Your account is linked to this phone so nobody can check in for you. Changing phones needs your lecturer to approve it.'),
    // Last, so "Skip" still lands here: students must see this before granting location.
    (Icons.location_on_outlined, 'Why we need your location',
        'Pinpoint takes one location reading when you check in, to confirm you are on campus, and one if you answer a "still here?" prompt. Nothing runs in the background.\n\nYour exact location is deleted 30 days after each session. Only your attendance status is kept.'),
  ];

  Future<void> _finish() async {
    if (widget.replay) return Navigator.of(context).pop();
    await ref.read(locationProvider).requestPermission();
    ref.read(settingsProvider.notifier).markOnboarded();
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final last = _page == _steps.length - 1;
    return Scaffold(
      body: SafeArea(
        child: PageBody(
          child: Column(children: [
            const SizedBox(height: 12),
            const Align(alignment: Alignment.centerLeft, child: Wordmark(size: 24)),
            Expanded(
              child: PageView.builder(
                controller: _pages,
                itemCount: _steps.length,
                onPageChanged: (i) => setState(() => _page = i),
                itemBuilder: (_, i) {
                  final (icon, title, body) = _steps[i];
                  return SingleChildScrollView(
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: 40),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        const SizedBox(height: 40),
                        Container(
                          width: 72,
                          height: 72,
                          decoration: BoxDecoration(color: IKColors.primary.withValues(alpha: 0.10), borderRadius: BorderRadius.circular(22)),
                          child: Icon(icon, size: 34, color: IKColors.primary),
                        ),
                        const SizedBox(height: 28),
                        Text(title, style: t.displaySmall?.copyWith(fontSize: 30, height: 1.15)),
                        const SizedBox(height: 14),
                        Text(body, style: t.bodyLarge?.copyWith(color: t.bodyMedium?.color)),
                      ]),
                    ),
                  );
                },
              ),
            ),
            Row(
              children: List.generate(
                _steps.length,
                (i) => AnimatedContainer(
                  duration: const Duration(milliseconds: 200),
                  margin: const EdgeInsets.only(right: 6),
                  width: i == _page ? 24 : 8,
                  height: 8,
                  decoration: BoxDecoration(
                    color: i == _page ? IKColors.primary : Theme.of(context).dividerColor,
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
              ),
            ),
            const SizedBox(height: 20),
            SizedBox(
              width: double.infinity,
              child: PrimaryButton(
                label: last ? (widget.replay ? 'Got it' : 'Allow location and continue') : 'Next',
                onPressed: last ? _finish : () => _pages.nextPage(duration: const Duration(milliseconds: 250), curve: Curves.easeOut),
              ),
            ),
            SizedBox(
              height: 48,
              child: last ? null : TextButton(onPressed: () => _pages.jumpToPage(_steps.length - 1), child: const Text('Skip')),
            ),
            const SizedBox(height: 4),
          ]),
        ),
      ),
    );
  }
}
