import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/models.dart';
import '../../core/auth/auth_controller.dart';
import '../../core/config.dart';
import '../../widgets/common.dart';

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _form = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _matric = TextEditingController();
  final _password = TextEditingController();
  final _invite = TextEditingController();
  UserGroup _role = UserGroup.student;
  bool _busy = false;

  @override
  void dispose() {
    for (final c in [_name, _email, _matric, _password, _invite]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _busy = true);
    final ok = await guarded(
      context,
      () => ref.read(authProvider.notifier).register(
            email: _email.text,
            fullName: _name.text,
            password: _password.text,
            role: _role,
            matricNo: _role == UserGroup.student ? _matric.text : null,
            inviteCode: _role == UserGroup.lecturer ? _invite.text : null,
          ),
    );
    if (!mounted) return;
    setState(() => _busy = false);
    if (ok) Navigator.of(context).popUntil((r) => r.isFirst);
  }

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final student = _role == UserGroup.student;

    return Scaffold(
      appBar: AppBar(title: const Text('Create account')),
      body: SafeArea(
        child: SingleChildScrollView(
          child: PageBody(
            padding: const EdgeInsets.symmetric(horizontal: 30, vertical: 10),
            child: Form(
              key: _form,
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                if (AppConfig.showLecturerSignup) ...[
                  SegmentedButton<UserGroup>(
                    segments: const [
                      ButtonSegment(value: UserGroup.student, label: Text('Student'), icon: Icon(Icons.school_outlined)),
                      ButtonSegment(value: UserGroup.lecturer, label: Text('Lecturer'), icon: Icon(Icons.co_present_outlined)),
                    ],
                    selected: {_role},
                    onSelectionChanged: (s) => setState(() => _role = s.first),
                  ),
                  const SizedBox(height: 20),
                ],
                LabeledField(
                  label: 'Full name',
                  controller: _name,
                  capitalization: TextCapitalization.words,
                  autofill: const [AutofillHints.name],
                  validator: (v) => (v == null || v.trim().length < 2) ? 'Enter your name' : null,
                ),
                LabeledField(
                  label: 'University email',
                  controller: _email,
                  keyboard: TextInputType.emailAddress,
                  autofill: const [AutofillHints.email],
                  validator: (v) => (v == null || !v.contains('@')) ? 'Enter a valid email' : null,
                ),
                if (student)
                  LabeledField(
                    label: 'Matriculation number',
                    controller: _matric,
                    keyboard: TextInputType.number,
                    validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your matric number' : null,
                  )
                else
                  LabeledField(
                    label: 'Staff invite code',
                    controller: _invite,
                    capitalization: TextCapitalization.characters,
                    validator: (v) => (v == null || v.trim().isEmpty) ? 'Lecturer accounts need the invite code' : null,
                  ),
                LabeledField(
                  label: 'Password',
                  controller: _password,
                  obscure: true,
                  autofill: const [AutofillHints.newPassword],
                  action: TextInputAction.done,
                  validator: (v) => (v == null || v.length < 8) ? 'At least 8 characters' : null,
                ),
                Text('At least 8 characters with a number.', style: t.bodySmall),
                const SizedBox(height: 20),
                PrimaryButton(label: 'Create account', busy: _busy, onPressed: _submit),
              ]),
            ),
          ),
        ),
      ),
    );
  }
}
