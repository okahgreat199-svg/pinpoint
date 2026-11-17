import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../core/checkin/checkin_service.dart';
import '../../core/location/location_service.dart';
import '../../core/providers.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';
import 'checkin_result_screen.dart';

/// The Check in tab. The camera only starts on the full-screen scanner, never in the background tab.
class ScanTab extends ConsumerWidget {
  const ScanTab({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = Theme.of(context).textTheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Check in')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.all(30),
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              const Icon(Icons.qr_code_scanner, size: 96, color: IKColors.primary),
              const SizedBox(height: 20),
              Text('Scan the code on the lecturer\'s screen', style: t.headlineLarge, textAlign: TextAlign.center),
              const SizedBox(height: 8),
              Text('Your camera app works too: it opens Pinpoint straight to check-in.', style: t.bodyLarge, textAlign: TextAlign.center),
              const SizedBox(height: 30),
              PrimaryButton(
                label: 'Scan QR code',
                icon: Icons.camera_alt_outlined,
                onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ScanScreen())),
              ),
              const SizedBox(height: 12),
              SecondaryButton(
                label: 'Enter 6-digit code',
                icon: Icons.dialpad,
                onPressed: () => _enterCode(context),
              ),
              const SizedBox(height: 40),
              SecondaryButton(
                label: 'Join a course',
                icon: Icons.group_add_outlined,
                onPressed: () => _joinCourse(context, ref),
              ),
            ]),
          ),
        ),
      ),
    );
  }

  Future<void> _enterCode(BuildContext context) async {
    final code = TextEditingController();
    final entered = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Enter code'),
        content: TextField(
          controller: code,
          autofocus: true,
          keyboardType: TextInputType.number,
          maxLength: 6,
          inputFormatters: [FilteringTextInputFormatter.digitsOnly],
          style: const TextStyle(fontSize: 28, letterSpacing: 8),
          textAlign: TextAlign.center,
          onSubmitted: (v) => Navigator.pop(ctx, v),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          TextButton(onPressed: () => Navigator.pop(ctx, code.text), child: const Text('Check in')),
        ],
      ),
    );
    if (entered != null && entered.length == 6 && context.mounted) {
      Navigator.of(context).push(MaterialPageRoute(builder: (_) => CheckInProgressScreen(scanned: entered)));
    }
  }

  /// BRD screen 10.
  Future<void> _joinCourse(BuildContext context, WidgetRef ref) async {
    final code = TextEditingController();
    final joined = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Join a course'),
        content: TextField(
          controller: code,
          autofocus: true,
          textCapitalization: TextCapitalization.characters,
          decoration: const InputDecoration(hintText: 'Join code from your lecturer'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          TextButton(
            onPressed: () async {
              try {
                final course = await ref.read(apiProvider).joinCourse(code.text.trim().toUpperCase());
                if (ctx.mounted) Navigator.pop(ctx, '${course.code} ${course.title}');
              } catch (e) {
                if (ctx.mounted) showSnack(ctx, e.toString(), error: true);
              }
            },
            child: const Text('Join'),
          ),
        ],
      ),
    );
    if (joined != null && context.mounted) {
      ref.invalidate(timetableProvider);
      ref.invalidate(myAttendanceProvider);
      showSnack(context, 'Joined $joined');
    }
  }
}

/// BRD screen 11: camera scanner.
class ScanScreen extends ConsumerStatefulWidget {
  const ScanScreen({super.key});

  @override
  ConsumerState<ScanScreen> createState() => _ScanScreenState();
}

class _ScanScreenState extends ConsumerState<ScanScreen> {
  final _controller = MobileScannerController(formats: const [BarcodeFormat.qrCode], detectionSpeed: DetectionSpeed.noDuplicates);
  late final _location = ref.read(locationProvider);
  bool _handled = false;

  @override
  void initState() {
    super.initState();
    _location.warmUp();   // GPS locks on while the student lines up the code
  }

  @override
  void dispose() {
    _location.coolDown();   // the check-in reuses the fix it already has
    _controller.dispose();
    super.dispose();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_handled) return;
    final value = capture.barcodes.map((b) => b.rawValue).whereType<String>().firstOrNull;
    if (value == null) return;
    _handled = true;
    HapticFeedback.mediumImpact();
    Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => CheckInProgressScreen(scanned: value)));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        title: const Text('Scan QR'),
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        actions: [IconButton(tooltip: 'Torch', color: Colors.white, icon: const Icon(Icons.flash_on), onPressed: _controller.toggleTorch)],
      ),
      body: Stack(fit: StackFit.expand, children: [
        MobileScanner(
          controller: _controller,
          onDetect: _onDetect,
          errorBuilder: (_, error) => ErrorView(message: 'Camera unavailable: ${error.errorCode.name}. Use the 6-digit code instead.'),
        ),
        Center(
          child: Container(
            width: 260,
            height: 260,
            decoration: BoxDecoration(border: Border.all(color: Colors.white, width: 3)),
          ),
        ),
        const Positioned(
          left: 20,
          right: 20,
          bottom: 40,
          child: Text('Point at the QR code on the lecturer\'s screen', textAlign: TextAlign.center, style: TextStyle(color: Colors.white, fontSize: 16)),
        ),
      ]),
    );
  }
}

/// Runs the check-in (location → sign → POST) and hands over to the result screen.
/// Also the target of deep links (`https://host/c/<token>` from the stock camera).
class CheckInProgressScreen extends ConsumerStatefulWidget {
  final String scanned;

  const CheckInProgressScreen({super.key, required this.scanned});

  @override
  ConsumerState<CheckInProgressScreen> createState() => _CheckInProgressScreenState();
}

class _CheckInProgressScreenState extends ConsumerState<CheckInProgressScreen> {
  String _stage = 'Getting your location…';
  LocationException? _locationError;

  @override
  void initState() {
    super.initState();
    _run();
  }

  Future<void> _run() async {
    setState(() {
      _locationError = null;
      _stage = 'Getting your location…';
    });
    CheckInOutcome outcome;
    try {
      outcome = await ref.read(checkInServiceProvider).checkIn(widget.scanned);
    } on LocationException catch (e) {
      if (mounted) setState(() => _locationError = e);
      return;
    } catch (e) {
      outcome = CheckInFailed(e.toString());
    }
    if (!mounted) return;
    ref.invalidate(timetableProvider);
    ref.invalidate(myAttendanceProvider);
    Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => CheckInResultScreen(outcome: outcome)));
  }

  @override
  Widget build(BuildContext context) {
    final err = _locationError;
    return Scaffold(
      appBar: AppBar(title: const Text('Checking in')),
      body: Center(
        child: PageBody(
          padding: const EdgeInsets.all(30),
          child: err == null
              ? Column(mainAxisSize: MainAxisSize.min, children: [
                  const CircularProgressIndicator(),
                  const SizedBox(height: 20),
                  Text(_stage, style: Theme.of(context).textTheme.bodyLarge, textAlign: TextAlign.center),
                ])
              : Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  const Icon(Icons.location_off, size: 72, color: IKColors.danger),
                  const SizedBox(height: 16),
                  Text(err.message, style: Theme.of(context).textTheme.headlineSmall, textAlign: TextAlign.center),
                  const SizedBox(height: 8),
                  const Text('Scan again quickly once fixed: the code changes every 15 seconds.', textAlign: TextAlign.center),
                  const SizedBox(height: 20),
                  if (err.needsSettings) ...[
                    SecondaryButton(label: 'Open settings', onPressed: () => ref.read(locationProvider).openSettings()),
                    const SizedBox(height: 10),
                  ],
                  PrimaryButton(label: 'Try again', onPressed: _run),
                ]),
        ),
      ),
    );
  }
}
