import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/checkin/checkin_service.dart';
import '../../core/location/location_service.dart';
import '../../core/push/firebase_service.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';

/// BRD screen 12: opened from the 70% notification. One button.
class PresencePromptScreen extends ConsumerStatefulWidget {
  final PresencePrompt prompt;

  const PresencePromptScreen({super.key, required this.prompt});

  @override
  ConsumerState<PresencePromptScreen> createState() => _PresencePromptScreenState();
}

class _PresencePromptScreenState extends ConsumerState<PresencePromptScreen> {
  bool _busy = false;
  String? _done;
  String? _error;

  Future<void> _confirm() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final r = await ref.read(checkInServiceProvider).answerPrompt(widget.prompt.checkInId);
      ref.invalidate(myAttendanceProvider);
      setState(() => _done = r.result == 'Present' ? 'Thanks, you\'re marked as still here.' : 'Recorded: ${r.result}. Your lecturer will review it.');
    } on LocationException catch (e) {
      setState(() => _error = e.message);
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final by = widget.prompt.respondBy;
    return Scaffold(
      appBar: AppBar(title: const Text('Presence check')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.all(30),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const SizedBox(height: 30),
              Icon(_done != null ? Icons.check_circle : Icons.front_hand_outlined, size: 110, color: _done != null ? IKColors.success : IKColors.primary),
              const SizedBox(height: 20),
              Text(_done ?? 'Are you still in the lecture?', style: t.headlineLarge, textAlign: TextAlign.center),
              if (_done == null) ...[
                const SizedBox(height: 10),
                Text(
                  'Tap the button to confirm. We take one location reading now.${by != null ? ' Answer by ${hm(by)}.' : ''}',
                  style: t.bodyLarge,
                  textAlign: TextAlign.center,
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(_error!, style: const TextStyle(color: IKColors.danger), textAlign: TextAlign.center),
                ],
                const SizedBox(height: 40),
                SizedBox(height: 64, child: PrimaryButton(label: 'I\'m still here', icon: Icons.check, busy: _busy, onPressed: _confirm)),
              ] else ...[
                const SizedBox(height: 40),
                SecondaryButton(label: 'Done', onPressed: () => Navigator.of(context).maybePop()),
              ],
            ]),
          ),
        ),
      ),
    );
  }
}
