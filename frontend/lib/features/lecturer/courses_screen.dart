import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/models.dart';
import '../../core/providers.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';
import 'start_session_screen.dart';

/// BRD screen 5: courses, join codes, schedule slots.
class CoursesScreen extends ConsumerWidget {
  const CoursesScreen({super.key});

  Future<void> _create(BuildContext context, WidgetRef ref) async {
    final code = TextEditingController();
    final title = TextEditingController();
    final form = GlobalKey<FormState>();
    final created = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('New course'),
        content: Form(
          key: form,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            LabeledField(label: 'Code', controller: code, capitalization: TextCapitalization.characters, validator: (v) => v!.trim().isEmpty ? 'Required' : null),
            LabeledField(label: 'Title', controller: title, capitalization: TextCapitalization.words, validator: (v) => v!.trim().isEmpty ? 'Required' : null),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')),
          TextButton(
            onPressed: () async {
              if (!form.currentState!.validate()) return;
              final ok = await guarded(ctx, () => ref.read(apiProvider).createCourse(code.text.trim(), title.text.trim()));
              if (ok && ctx.mounted) Navigator.pop(ctx, true);
            },
            child: const Text('Create'),
          ),
        ],
      ),
    );
    if (created == true) ref.invalidate(coursesProvider);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final courses = ref.watch(coursesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Courses')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _create(context, ref),
        icon: const Icon(Icons.add),
        label: const Text('New course'),
        backgroundColor: IKColors.primary,
        foregroundColor: Colors.white,
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(coursesProvider.future),
        child: AsyncBody(
          value: courses,
          onRetry: () => ref.invalidate(coursesProvider),
          data: (list) => list.isEmpty
              ? ListView(children: const [SizedBox(height: 80), EmptyView(icon: Icons.class_outlined, message: 'No courses yet. Create one to get a join code.')])
              : ListView.separated(
                  padding: const EdgeInsets.only(bottom: 90),
                  itemCount: list.length,
                  separatorBuilder: (_, __) => const Divider(height: 1),
                  itemBuilder: (_, i) {
                    final c = list[i];
                    return ListTile(
                      title: Text('${c.code} · ${c.title}'),
                      subtitle: Text('${c.studentCount} students · join code ${c.joinCode ?? '—'}'),
                      trailing: const Icon(Icons.chevron_right),
                      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CourseDetailScreen(course: c))),
                    );
                  },
                ),
        ),
      ),
    );
  }
}

class CourseDetailScreen extends ConsumerWidget {
  final Course course;

  const CourseDetailScreen({super.key, required this.course});

  Future<void> _addSlot(BuildContext context, WidgetRef ref) async {
    final added = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (_) => _AddSlotSheet(courseId: course.id),
    );
    if (added == true) {
      ref.invalidate(schedulesProvider(course.id));
      ref.invalidate(timetableProvider);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = Theme.of(context).textTheme;
    final schedules = ref.watch(schedulesProvider(course.id));
    final students = ref.watch(studentsProvider(course.id));

    return Scaffold(
      appBar: AppBar(title: Text(course.code)),
      body: ListView(padding: const EdgeInsets.all(20), children: [
        Text(course.title, style: t.headlineLarge),
        const SizedBox(height: 15),
        Card(
          child: ListTile(
            leading: const Icon(Icons.key),
            title: const Text('Join code'),
            subtitle: Text(course.joinCode ?? '—', style: t.headlineMedium?.copyWith(letterSpacing: 4)),
            trailing: IconButton(
              tooltip: 'Copy',
              icon: const Icon(Icons.copy),
              onPressed: () {
                Clipboard.setData(ClipboardData(text: course.joinCode ?? ''));
                showSnack(context, 'Join code copied');
              },
            ),
          ),
        ),
        const SizedBox(height: 15),
        PrimaryButton(
          label: 'Start a session now',
          icon: Icons.play_arrow,
          onPressed: () => Navigator.of(context).push(MaterialPageRoute(
              builder: (_) => StartSessionScreen(courseId: course.id, courseLabel: '${course.code} ${course.title}'))),
        ),
        const SizedBox(height: 25),
        Row(children: [
          Expanded(child: Text('Weekly slots', style: t.headlineSmall)),
          TextButton.icon(onPressed: () => _addSlot(context, ref), icon: const Icon(Icons.add), label: const Text('Add slot')),
        ]),
        AsyncBody(
          value: schedules,
          onRetry: () => ref.invalidate(schedulesProvider(course.id)),
          data: (list) => list.isEmpty
              ? const Padding(padding: EdgeInsets.symmetric(vertical: 10), child: Text('No slots yet.'))
              : Column(children: [
                  for (final s in list)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.schedule),
                      title: Text('${s.dayOfWeek} ${s.timeRange}'),
                      subtitle: s.room == null ? null : Text(s.room!),
                      trailing: IconButton(
                        tooltip: 'Delete slot',
                        icon: const Icon(Icons.delete_outline),
                        onPressed: () async {
                          final ok = await guarded(context, () => ref.read(apiProvider).deleteSchedule(course.id, s.id));
                          if (ok) ref.invalidate(schedulesProvider(course.id));
                        },
                      ),
                    ),
                ]),
        ),
        const SizedBox(height: 20),
        Text('Students', style: t.headlineSmall),
        AsyncBody(
          value: students,
          onRetry: () => ref.invalidate(studentsProvider(course.id)),
          data: (list) => list.isEmpty
              ? const Padding(padding: EdgeInsets.symmetric(vertical: 10), child: Text('Nobody has joined yet. Share the join code.'))
              : Column(children: [
                  for (final s in list)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.person_outline),
                      title: Text(s.fullName),
                      subtitle: Text([s.matricNo, s.email].whereType<String>().join(' · ')),
                    ),
                ]),
        ),
      ]),
    );
  }
}

class _AddSlotSheet extends ConsumerStatefulWidget {
  final String courseId;

  const _AddSlotSheet({required this.courseId});

  @override
  ConsumerState<_AddSlotSheet> createState() => _AddSlotSheetState();
}

class _AddSlotSheetState extends ConsumerState<_AddSlotSheet> {
  String _day = weekdays[DateTime.now().weekday - 1];
  TimeOfDay _start = const TimeOfDay(hour: 10, minute: 0);
  TimeOfDay _end = const TimeOfDay(hour: 11, minute: 0);
  final _room = TextEditingController();
  bool _busy = false;

  static String _fmt(TimeOfDay t) => '${t.hour.toString().padLeft(2, '0')}:${t.minute.toString().padLeft(2, '0')}:00';

  Future<void> _pick(bool start) async {
    final picked = await showTimePicker(context: context, initialTime: start ? _start : _end);
    if (picked != null) setState(() => start ? _start = picked : _end = picked);
  }

  Future<void> _save() async {
    setState(() => _busy = true);
    final ok = await guarded(
        context,
        () => ref.read(apiProvider).addSchedule(widget.courseId,
            dayOfWeek: _day, start: _fmt(_start), end: _fmt(_end), room: _room.text.trim().isEmpty ? null : _room.text.trim()));
    if (!mounted) return;
    setState(() => _busy = false);
    if (ok) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.viewInsetsOf(context).bottom + 20),
      child: SingleChildScrollView(
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('Add weekly slot', style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 15),
          DropdownButtonFormField<String>(
            initialValue: _day,
            decoration: const InputDecoration(labelText: 'Day'),
            items: [for (final d in weekdays) DropdownMenuItem(value: d, child: Text(d))],
            onChanged: (v) => setState(() => _day = v!),
          ),
          const SizedBox(height: 10),
          Row(children: [
            Expanded(child: SecondaryButton(label: 'From ${_start.format(context)}', onPressed: () => _pick(true))),
            const SizedBox(width: 10),
            Expanded(child: SecondaryButton(label: 'To ${_end.format(context)}', onPressed: () => _pick(false))),
          ]),
          const SizedBox(height: 15),
          LabeledField(label: 'Room (optional)', controller: _room, capitalization: TextCapitalization.words, action: TextInputAction.done),
          PrimaryButton(label: 'Save slot', busy: _busy, onPressed: _save),
        ]),
      ),
    );
  }
}
