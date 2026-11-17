import 'package:flutter/material.dart';

import '../../core/api/models.dart';
import '../../core/checkin/checkin_service.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import 'scan_screen.dart';

/// "Shows result clearly" (BRD screen 11): big icon, plain sentence, what to do next.
class CheckInResultScreen extends StatelessWidget {
  final CheckInOutcome outcome;

  const CheckInResultScreen({super.key, required this.outcome});

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;

    final (IconData icon, Color color, String title, String body, bool canRetry) = switch (outcome) {
      CheckInDone(:final result) when result.isDuplicate => (Icons.check_rounded, IKColors.success, 'Already checked in', result.message, false),
      CheckInDone(:final result) when result.status == 'Present' => (Icons.check_rounded, IKColors.success, 'You\'re checked in', result.message, false),
      CheckInDone(:final result) when result.status == 'Flagged' => (
          Icons.flag_rounded,
          IKColors.warning,
          'Checked in, flagged for review',
          '${result.message}\n\nYou are counted as attending. Your lecturer will review: ${result.flags.map(humanise).join(', ').toLowerCase()}.',
          false
        ),
      CheckInDone(:final result) => (
          Icons.close_rounded,
          IKColors.danger,
          'Check-in rejected',
          result.message,
          result.rejectionReason == 'ExpiredToken' || result.rejectionReason == 'OffCampus' || result.rejectionReason == 'InvalidToken'
        ),
      CheckInQueued() => (
          Icons.cloud_upload_outlined,
          IKColors.warning,
          'Saved, waiting for signal',
          'You\'re offline. The check-in will be sent automatically when you reconnect. If that\'s after the code expired, it will be rejected and you should scan again.',
          true
        ),
      CheckInFailed(:final message) => (Icons.error_outline, IKColors.danger, 'Couldn\'t check in', message, true),
    };

    return Scaffold(
      appBar: AppBar(title: const Text('Check-in')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.all(30),
            child: Semantics(
              liveRegion: true,
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const SizedBox(height: 24),
                Center(
                  child: Container(
                    width: 112,
                    height: 112,
                    decoration: BoxDecoration(color: color.withValues(alpha: 0.12), shape: BoxShape.circle),
                    child: Icon(icon, size: 56, color: color),
                  ),
                ),
                const SizedBox(height: 28),
                Text(title, style: t.headlineLarge, textAlign: TextAlign.center),
                const SizedBox(height: 10),
                Text(body, style: t.bodyLarge?.copyWith(color: t.bodyMedium?.color), textAlign: TextAlign.center),
                const SizedBox(height: 40),
                if (canRetry) ...[
                  PrimaryButton(
                    label: 'Scan again',
                    icon: Icons.qr_code_scanner,
                    onPressed: () => Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const ScanScreen())),
                  ),
                  const SizedBox(height: 10),
                ],
                SecondaryButton(label: 'Done', onPressed: () => Navigator.of(context).popUntil((r) => r.isFirst)),
                if (outcome is CheckInDone) ...[
                  const SizedBox(height: 28),
                  const InfoNote(icon: Icons.lock_outline_rounded, text: locationRetentionNote),
                ],
              ]),
            ),
          ),
        ),
      ),
    );
  }
}
