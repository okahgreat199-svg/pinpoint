import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api/models.dart';
import '../utils/constants/colors.dart';
import '../utils/constants/sizes.dart';

/// Label above a field, as in the kit's auth screens.
class LabeledField extends StatelessWidget {
  final String label;
  final TextEditingController controller;
  final bool obscure;
  final TextInputType? keyboard;
  final String? Function(String?)? validator;
  final TextCapitalization capitalization;
  final Iterable<String>? autofill;
  final TextInputAction action;
  final void Function(String)? onSubmitted;

  const LabeledField({
    super.key,
    required this.label,
    required this.controller,
    this.obscure = false,
    this.keyboard,
    this.validator,
    this.capitalization = TextCapitalization.none,
    this.autofill,
    this.action = TextInputAction.next,
    this.onSubmitted,
  });

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 15),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(label, style: t.labelMedium),
          const SizedBox(height: 8),
          TextFormField(
            controller: controller,
            obscureText: obscure,
            keyboardType: keyboard,
            validator: validator,
            textCapitalization: capitalization,
            autofillHints: autofill,
            textInputAction: action,
            onFieldSubmitted: onSubmitted,
            style: t.bodyLarge,
          ),
        ],
      ),
    );
  }
}

/// Accent button from the theme, with a busy state.
class PrimaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final bool busy;
  final IconData? icon;

  const PrimaryButton({super.key, required this.label, this.onPressed, this.busy = false, this.icon});

  @override
  Widget build(BuildContext context) {
    return ElevatedButton(
      onPressed: busy ? null : onPressed,
      child: busy
          ? const SizedBox(height: 22, width: 22, child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white))
          : Row(mainAxisSize: MainAxisSize.min, children: [
              if (icon != null) ...[Icon(icon, size: 20), const SizedBox(width: 8)],
              Flexible(child: Text(label, textAlign: TextAlign.center)),
            ]),
    );
  }
}

class SecondaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;

  const SecondaryButton({super.key, required this.label, this.onPressed, this.icon});

  @override
  Widget build(BuildContext context) {
    return OutlinedButton(
      onPressed: onPressed,
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        if (icon != null) ...[Icon(icon, size: 20), const SizedBox(width: 8)],
        Flexible(child: Text(label, textAlign: TextAlign.center)),
      ]),
    );
  }
}

/// Centred, width-limited page body (IKSizes.container) with the kit's padding.
class PageBody extends StatelessWidget {
  final Widget child;
  final EdgeInsets padding;

  const PageBody({super.key, required this.child, this.padding = const EdgeInsets.symmetric(horizontal: 20, vertical: 15)});

  @override
  Widget build(BuildContext context) => Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: IKSizes.container),
          child: Padding(padding: padding, child: child),
        ),
      );
}

Color statusColor(String status) => switch (status) {
      'Present' => IKColors.success,
      'Flagged' || 'Pending' => IKColors.warning,
      'Rejected' || 'Absent' || 'Revoked' => IKColors.danger,
      'Open' || 'Active' => IKColors.success,
      _ => IKColors.text,
    };

IconData statusIcon(String status) => switch (status) {
      'Present' || 'Active' => Icons.check_circle,
      'Flagged' || 'Pending' => Icons.flag,
      'Rejected' || 'Absent' || 'Revoked' => Icons.cancel,
      _ => Icons.radio_button_unchecked,
    };

/// Coloured status label. Always has text as well as colour (colour-blind safe).
class StatusChip extends StatelessWidget {
  final String status;
  final String? label;

  const StatusChip(this.status, {super.key, this.label});

  @override
  Widget build(BuildContext context) {
    final c = statusColor(status);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(color: c.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(999)),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Icon(statusIcon(status), size: 13, color: c),
        const SizedBox(width: 5),
        Text(label ?? humanise(status), style: TextStyle(color: c, fontSize: 12, fontWeight: FontWeight.w700, letterSpacing: 0.1)),
      ]),
    );
  }
}

/// Loading / error / data for an AsyncValue, with pull-to-refresh on error.
class AsyncBody<T> extends StatelessWidget {
  final AsyncValue<T> value;
  final Widget Function(T data) data;
  final VoidCallback onRetry;

  const AsyncBody({super.key, required this.value, required this.data, required this.onRetry});

  @override
  Widget build(BuildContext context) => value.when(
        data: data,
        error: (e, _) => ErrorView(message: e.toString(), onRetry: onRetry),
        loading: () => const Center(child: CircularProgressIndicator()),
      );
}

class ErrorView extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;

  const ErrorView({super.key, required this.message, this.onRetry});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(30),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.cloud_off_rounded, size: 44, color: Theme.of(context).colorScheme.onSurfaceVariant),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyLarge),
            if (onRetry != null) ...[const SizedBox(height: 16), SecondaryButton(label: 'Try again', onPressed: onRetry)],
          ]),
        ),
      );
}

class EmptyView extends StatelessWidget {
  final IconData icon;
  final String message;

  const EmptyView({super.key, required this.icon, required this.message});

  @override
  Widget build(BuildContext context) => Center(
        child: Padding(
          padding: const EdgeInsets.all(30),
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(icon, size: 44, color: Theme.of(context).colorScheme.onSurfaceVariant),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
          ]),
        ),
      );
}

void showSnack(BuildContext context, String message, {bool error = false}) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message), backgroundColor: error ? IKColors.danger : null, showCloseIcon: error));
}

/// Runs [action], showing its error as a snackbar. Returns true on success.
Future<bool> guarded(BuildContext context, Future<void> Function() action) async {
  try {
    await action();
    return true;
  } catch (e) {
    if (context.mounted) showSnack(context, e.toString(), error: true);
    return false;
  }
}

String hm(DateTime t) => '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}';

String dayLabel(DateTime t) {
  final today = DateTime.now();
  final d = DateTime(t.year, t.month, t.day);
  final diff = d.difference(DateTime(today.year, today.month, today.day)).inDays;
  if (diff == 0) return 'Today';
  if (diff == 1) return 'Tomorrow';
  if (diff == -1) return 'Yesterday';
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return '${weekdays[t.weekday - 1].substring(0, 3)} ${t.day} ${months[t.month - 1]}';
}

/// The app name set in type; Pinpoint has no logo.
class Wordmark extends StatelessWidget {
  final double size;
  const Wordmark({super.key, this.size = 28});

  @override
  Widget build(BuildContext context) => Text(
        'Pinpoint',
        style: TextStyle(
            fontFamily: 'PlusJakartaSans', fontSize: size, fontWeight: FontWeight.w800, letterSpacing: -size * 0.04,
            color: Theme.of(context).colorScheme.onSurface, height: 1),
      );
}

/// Small grey caption with an icon, for privacy and context notes under main content.
class InfoNote extends StatelessWidget {
  final IconData icon;
  final String text;
  const InfoNote({super.key, required this.icon, required this.text});

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    return Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Padding(padding: const EdgeInsets.only(top: 1), child: Icon(icon, size: 16, color: t.bodyMedium?.color)),
      const SizedBox(width: 8),
      Expanded(child: Text(text, style: t.bodySmall?.copyWith(height: 1.4))),
    ]);
  }
}

/// Shown wherever a student's location is taken (BRD: data minimisation).
const locationRetentionNote = 'Your exact location is deleted 30 days after the session. Only your attendance status is kept.';

/// Matches the Android launch screen (same image, size and white background), so the
/// hand-off from the system splash to Flutter is invisible while the session is restored.
class SplashScreen extends StatelessWidget {
  const SplashScreen({super.key});

  @override
  Widget build(BuildContext context) => Scaffold(
        backgroundColor: Colors.white,
        body: Center(child: Image.asset('assets/images/wordmark.png', width: 154, semanticLabel: 'Pinpoint')),
      );
}
