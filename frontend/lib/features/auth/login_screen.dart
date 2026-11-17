import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../../widgets/common.dart';
import 'onboarding_screen.dart';
import 'register_screen.dart';

/// BRD screen 2, laid out like the kit's sign-in screen.
class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _form = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _busy = true);
    await guarded(context, () => ref.read(authProvider.notifier).login(_email.text, _password.text));
    if (mounted) setState(() => _busy = false);
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final notice = switch (ref.watch(authProvider)) { SignedOut(:final message) => message, _ => null };

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 16),
            child: Form(
              key: _form,
              child: AutofillGroup(
                child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                  const SizedBox(height: 12),
                  const Align(alignment: Alignment.centerLeft, child: Wordmark(size: 24)),
                  const SizedBox(height: 56),
                  Text('Welcome back', style: t.displaySmall?.copyWith(fontSize: 30)),
                  const SizedBox(height: 8),
                  Text('Sign in with your university email.', style: t.bodyMedium?.copyWith(fontSize: 16)),
                  if (notice != null) ...[
                    const SizedBox(height: 15),
                    Text(notice, style: t.bodyMedium?.copyWith(color: Theme.of(context).colorScheme.error)),
                  ],
                  const SizedBox(height: 32),
                  LabeledField(
                    label: 'Email address',
                    controller: _email,
                    keyboard: TextInputType.emailAddress,
                    autofill: const [AutofillHints.email],
                    validator: (v) => (v == null || !v.contains('@')) ? 'Enter your email' : null,
                  ),
                  LabeledField(
                    label: 'Password',
                    controller: _password,
                    obscure: true,
                    autofill: const [AutofillHints.password],
                    action: TextInputAction.done,
                    onSubmitted: (_) => _submit(),
                    validator: (v) => (v == null || v.isEmpty) ? 'Enter your password' : null,
                  ),
                  const SizedBox(height: 10),
                  PrimaryButton(label: 'Sign in', busy: _busy, onPressed: _submit),
                  const SizedBox(height: 12),
                  TextButton(
                    onPressed: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const RegisterScreen())),
                    child: const Text('New here? Create an account'),
                  ),
                  TextButton(
                    onPressed: () => Navigator.of(context)
                        .push(MaterialPageRoute(builder: (_) => const OnboardingScreen(replay: true))),
                    style: TextButton.styleFrom(foregroundColor: t.bodyMedium?.color),
                    child: const Text('How Pinpoint works'),
                  ),
                ]),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
