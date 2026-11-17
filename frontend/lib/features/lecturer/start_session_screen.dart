import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_client.dart';
import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';
import 'live_qr_screen.dart';

/// BRD screen 6: capture the anchor (lecturer's location), confirm, open.
class StartSessionScreen extends ConsumerStatefulWidget {
  final String courseId;
  final String courseLabel;
  final String? scheduleId;

  const StartSessionScreen({super.key, required this.courseId, required this.courseLabel, this.scheduleId});

  @override
  ConsumerState<StartSessionScreen> createState() => _StartSessionScreenState();
}

class _StartSessionScreenState extends ConsumerState<StartSessionScreen> {
  Fix? _fix;
  String? _error;
  bool _locating = false;
  bool _starting = false;
  int _minutes = 60;

  @override
  void initState() {
    super.initState();
    _locate();
  }

  Future<void> _locate() async {
    setState(() {
      _locating = true;
      _error = null;
    });
    try {
      final fix = await ref.read(locationProvider).current();
      if (mounted) setState(() => _fix = fix);
    } on LocationException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  Future<void> _start({bool confirmOffSchedule = false}) async {
    final fix = _fix!;
    setState(() => _starting = true);
    try {
      final session = await ref.read(apiProvider).startSession(
            courseId: widget.courseId,
            scheduleId: widget.scheduleId,
            lat: fix.lat,
            lng: fix.lng,
            accuracyM: fix.accuracyM,
            isMocked: fix.isMocked,
            // A timetabled slot already has an end time; ad-hoc sessions use the picker.
            durationMinutes: widget.scheduleId == null ? _minutes : null,
            confirmOffSchedule: confirmOffSchedule,
          );
      ref.invalidate(timetableProvider);
      if (mounted) Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => LiveQrScreen(sessionId: session.id)));
    } on ApiException catch (e) {
      // 428: outside the slot's time. A warning, not a block: catch-up classes are legitimate.
      if (e.statusCode == 428 && mounted) {
        setState(() => _starting = false);
        if (await _confirmOffSchedule(e.message)) await _start(confirmOffSchedule: true);
      } else if (mounted) {
        showSnack(context, e.message, error: true);
      }
    } catch (e) {
      if (mounted) showSnack(context, e.toString(), error: true);
    } finally {
      if (mounted) setState(() => _starting = false);
    }
  }

  Future<bool> _confirmOffSchedule(String message) async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          icon: const Icon(Icons.schedule_rounded),
          title: const Text('Not the scheduled time'),
          content: Text(message),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
            TextButton(onPressed: () => Navigator.pop(context, true), child: const Text('Start anyway')),
          ],
        ),
      ) ??
      false;

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final fix = _fix;
    final poor = fix != null && fix.accuracyM > 50;

    return Scaffold(
      appBar: AppBar(title: const Text('Start session')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text(widget.courseLabel, style: t.headlineLarge),
              const SizedBox(height: 20),
              Text('Start the session from the lecture room. Your location becomes the anchor: students far from it are flagged for review, not rejected. You need to be on campus, with mock locations off.', style: t.bodyLarge?.copyWith(color: t.bodyMedium?.color)),
              const SizedBox(height: 20),
              Card(
                child: ListTile(
                  leading: _locating
                      ? const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2))
                      : Icon(_error != null ? Icons.location_off : Icons.my_location, color: _error != null ? IKColors.danger : IKColors.success),
                  title: Text(_locating
                      ? 'Getting your location…'
                      : _error ?? 'Location captured (±${fix!.accuracyM.round()} m)'),
                  subtitle: fix == null ? null : Text('${fix.lat.toStringAsFixed(5)}, ${fix.lng.toStringAsFixed(5)}'),
                  trailing: IconButton(tooltip: 'Retry', icon: const Icon(Icons.refresh), onPressed: _locating ? null : _locate),
                ),
              ),
              if (poor)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text('Accuracy is poor indoors. Move near a window and retry for a better anchor.', style: TextStyle(color: IKColors.warning)),
                ),
              if (widget.scheduleId == null) ...[
                const SizedBox(height: 20),
                Text('Length', style: t.headlineSmall),
                const SizedBox(height: 8),
                SegmentedButton<int>(
                  segments: const [
                    ButtonSegment(value: 30, label: Text('30 min')),
                    ButtonSegment(value: 60, label: Text('1 h')),
                    ButtonSegment(value: 120, label: Text('2 h')),
                  ],
                  selected: {_minutes},
                  onSelectionChanged: (s) => setState(() => _minutes = s.first),
                ),
              ],
              const SizedBox(height: 30),
              PrimaryButton(label: 'Open session', icon: Icons.qr_code_2, busy: _starting, onPressed: fix == null ? null : _start),
            ]),
          ),
        ),
      ),
    );
  }
}
