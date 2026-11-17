import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../../core/api/api_client.dart';
import '../../core/api/models.dart';
import '../../core/config.dart';
import '../../core/providers.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';
import 'attendance_review_screen.dart';

/// BRD screen 7 / key flow 4: full-screen rotating QR, polled every 5 s, screen kept awake.
class LiveQrScreen extends ConsumerStatefulWidget {
  final String sessionId;

  const LiveQrScreen({super.key, required this.sessionId});

  @override
  ConsumerState<LiveQrScreen> createState() => _LiveQrScreenState();
}

class _LiveQrScreenState extends ConsumerState<LiveQrScreen> {
  SessionQr? _qr;
  DateTime _fetchedAt = DateTime.now();
  String? _error;
  Timer? _poll;
  Timer? _tick;
  bool _ended = false;

  @override
  void initState() {
    super.initState();
    WakelockPlus.enable();
    _fetch();
    _poll = Timer.periodic(AppConfig.qrPollInterval, (_) => _fetch());
    _tick = Timer.periodic(const Duration(seconds: 1), (_) => _onTick());
  }

  @override
  void dispose() {
    _poll?.cancel();
    _tick?.cancel();
    WakelockPlus.disable();
    super.dispose();
  }

  int get _secondsLeft {
    final qr = _qr;
    if (qr == null) return 0;
    return (qr.secondsRemaining - DateTime.now().difference(_fetchedAt).inSeconds).clamp(0, qr.stepSeconds);
  }

  void _onTick() {
    if (!mounted) return;
    // Fetch right as the step rolls over so the code on screen is never stale.
    if (_qr != null && _secondsLeft == 0) _fetch();
    setState(() {});
  }

  Future<void> _fetch() async {
    if (_ended) return;
    try {
      final qr = await ref.read(apiProvider).qr(widget.sessionId);
      if (!mounted) return;
      setState(() {
        _qr = qr;
        _fetchedAt = DateTime.now();
        _error = null;
      });
    } on ApiException catch (e) {
      if (!mounted) return;
      // 409/400 once the session has closed (end time reached or closed elsewhere).
      if (!e.isRetryable && e.statusCode != 401) {
        _ended = true;
        _poll?.cancel();
      }
      setState(() => _error = e.message);
    }
  }

  Future<void> _sendPrompt() async {
    await guarded(context, () async {
      final n = await ref.read(apiProvider).sendPresencePrompt(widget.sessionId);
      if (mounted) showSnack(context, 'Presence check sent to $n student${n == 1 ? '' : 's'}.');
    });
  }

  Future<void> _end() async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('End session?'),
        content: const Text('Students who have not checked in will be marked absent.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Keep open')),
          TextButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('End session')),
        ],
      ),
    );
    if (confirm != true || !mounted) return;
    final ok = await guarded(context, () => ref.read(apiProvider).closeSession(widget.sessionId));
    if (!ok || !mounted) return;
    _ended = true;
    ref.invalidate(timetableProvider);
    Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => AttendanceReviewScreen(sessionId: widget.sessionId)));
  }

  @override
  Widget build(BuildContext context) {
    final qr = _qr;
    final t = Theme.of(context).textTheme;

    final code = qr == null
        ? const Center(child: CircularProgressIndicator())
        : LayoutBuilder(builder: (_, c) {
            final size = (c.biggest.shortestSide - 20).clamp(160.0, 520.0);
            return Center(
              child: Container(
                color: Colors.white,
                padding: const EdgeInsets.all(12),
                child: QrImageView(
                  data: qr.qrPayload,
                  size: size,
                  errorCorrectionLevel: QrErrorCorrectLevel.M,
                  semanticsLabel: 'Check-in QR code',
                ),
              ),
            );
          });

    final info = Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      if (qr != null) ...[
        Text('${qr.checkedInCount} / ${qr.enrolledCount}', style: t.headlineLarge?.copyWith(fontSize: 40), textAlign: TextAlign.center),
        Text('checked in', style: t.bodyLarge, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        LinearProgressIndicator(value: qr.stepSeconds == 0 ? 0 : _secondsLeft / qr.stepSeconds, color: IKColors.primary, minHeight: 6),
        const SizedBox(height: 6),
        Text('New code in ${_secondsLeft}s', style: t.bodyMedium, textAlign: TextAlign.center),
        const SizedBox(height: 12),
        Text('No camera? Enter code', style: t.bodySmall, textAlign: TextAlign.center),
        Text(qr.manualCode, style: t.headlineLarge?.copyWith(letterSpacing: 6, fontSize: 30), textAlign: TextAlign.center),
        Text('Ends ${hm(qr.endsAt)}', style: t.bodySmall, textAlign: TextAlign.center),
      ],
      if (_error != null)
        Padding(padding: const EdgeInsets.only(top: 8), child: Text(_error!, style: const TextStyle(color: IKColors.danger), textAlign: TextAlign.center)),
      const SizedBox(height: 16),
      SecondaryButton(label: 'Send presence check', icon: Icons.notifications_active_outlined, onPressed: _ended ? null : _sendPrompt),
      const SizedBox(height: 10),
      PrimaryButton(label: _ended ? 'View attendance' : 'End session', icon: _ended ? Icons.list_alt : Icons.stop, onPressed: _ended
          ? () => Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => AttendanceReviewScreen(sessionId: widget.sessionId)))
          : _end),
    ]);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Live QR'),
        actions: [
          IconButton(
            tooltip: 'Live roster',
            icon: const Icon(Icons.people_outline),
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AttendanceReviewScreen(sessionId: widget.sessionId))),
          ),
        ],
      ),
      body: SafeArea(
        child: OrientationBuilder(
          builder: (_, o) => o == Orientation.landscape
              ? Row(children: [
                  Expanded(flex: 3, child: code),
                  Expanded(flex: 2, child: SingleChildScrollView(padding: const EdgeInsets.all(16), child: info)),
                ])
              : Column(children: [
                  Expanded(child: code),
                  Flexible(child: SingleChildScrollView(padding: const EdgeInsets.fromLTRB(20, 0, 20, 16), child: info)),
                ]),
        ),
      ),
    );
  }
}
