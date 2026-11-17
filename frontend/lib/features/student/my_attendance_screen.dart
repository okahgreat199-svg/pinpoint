import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/models.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';

/// BRD screen 13: history per course with percentage.
class MyAttendanceScreen extends ConsumerWidget {
  const MyAttendanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final value = ref.watch(myAttendanceProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('My attendance')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(myAttendanceProvider.future),
        child: AsyncBody(
          value: value,
          onRetry: () => ref.invalidate(myAttendanceProvider),
          data: (courses) => courses.isEmpty
              ? ListView(children: const [SizedBox(height: 80), EmptyView(icon: Icons.insights_outlined, message: 'No courses yet. Join one from the Check in tab.')])
              : ListView(padding: const EdgeInsets.all(20), children: [for (final c in courses) _CourseCard(course: c)]),
        ),
      ),
    );
  }
}

class _CourseCard extends StatelessWidget {
  final CourseAttendance course;

  const _CourseCard({required this.course});

  @override
  Widget build(BuildContext context) {
    final t = Theme.of(context).textTheme;
    final pct = course.percentage;
    final color = pct >= 80 ? IKColors.success : (pct >= 60 ? IKColors.warning : IKColors.danger);

    return Card(
      margin: const EdgeInsets.only(bottom: 15),
      child: ExpansionTile(
        shape: const Border(),
        title: Text('${course.courseCode} · ${course.courseTitle}', style: t.titleLarge),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 8),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            LinearProgressIndicator(value: course.totalSessions == 0 ? 0 : pct / 100, color: color, minHeight: 8),
            const SizedBox(height: 6),
            Text(
              course.totalSessions == 0
                  ? 'No sessions yet'
                  : '${pct.round()}% · ${course.present} present, ${course.flagged} flagged, ${course.absent} absent of ${course.totalSessions}',
              style: t.bodyMedium,
            ),
          ]),
        ),
        children: [
          for (final s in course.sessions)
            ListTile(
              dense: true,
              title: Text('${dayLabel(s.startsAt)} ${hm(s.startsAt)}'),
              subtitle: s.flags.isEmpty && s.lastRejectionReason == null
                  ? null
                  : Text([
                      ...s.flags.map(humanise),
                      if (s.lastRejectionReason != null && s.outcome == 'Absent') 'last attempt: ${humanise(s.lastRejectionReason!).toLowerCase()}',
                      if (s.reviewed) 'reviewed',
                    ].join(' · ')),
              trailing: StatusChip(s.outcome),
            ),
        ],
      ),
    );
  }
}
