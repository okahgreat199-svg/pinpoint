import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/models.dart';
import '../../core/auth/auth_controller.dart';
import '../../utils/constants/colors.dart';
import '../../utils/constants/sizes.dart';
import '../../widgets/common.dart';
import '../data.dart';
import '../lecturer/attendance_review_screen.dart';
import '../lecturer/live_qr_screen.dart';
import '../lecturer/start_session_screen.dart';
import '../student/scan_screen.dart';

/// BRD screen 4: today's and upcoming sessions.
class TimetableScreen extends ConsumerWidget {
  const TimetableScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = ref.watch(currentUserProvider);
    final timetable = ref.watch(timetableProvider);

    return Scaffold(
      appBar: AppBar(
        toolbarHeight: 84,
        title: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(_today(), style: Theme.of(context).textTheme.labelMedium),
          const SizedBox(height: 2),
          Text('Hi ${user.firstName}'),
        ]),
      ),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(timetableProvider.future),
        child: AsyncBody(
          value: timetable,
          onRetry: () => ref.invalidate(timetableProvider),
          data: (t) {
            final items = t.entries.where((e) => e.status != 'Ended' || e.sessionId != null).toList();
            if (items.isEmpty) {
              return ListView(children: [
                const SizedBox(height: 80),
                EmptyView(
                  icon: Icons.event_available,
                  message: user.isLecturer
                      ? 'No sessions this week. Add schedule slots under Courses, or start a session from a course.'
                      : 'Nothing on your timetable this week. Join a course from the Check in tab.',
                ),
              ]);
            }
            return ListView(
              padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
              children: [
                if (t.cachedAt != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Text('Offline: showing the timetable saved ${dayLabel(t.cachedAt!).toLowerCase()} at ${hm(t.cachedAt!)}.',
                        style: TextStyle(color: IKColors.warning)),
                  ),
                ..._grouped(context, ref, items, user.isLecturer),
              ],
            );
          },
        ),
      ),
    );
  }

  List<Widget> _grouped(BuildContext context, WidgetRef ref, List<TimetableEntry> items, bool lecturer) {
    final out = <Widget>[];
    String? day;
    for (final e in items) {
      final label = dayLabel(e.startsAt);
      if (label != day) {
        day = label;
        out.add(Padding(
          padding: const EdgeInsets.only(top: 18, bottom: 10),
          child: Text(label.toUpperCase(), style: Theme.of(context).textTheme.labelSmall),
        ));
      }
      out.add(_EntryCard(entry: e, lecturer: lecturer));
    }
    return out;
  }

  static String _today() {
    const months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
    final now = DateTime.now();
    return '${weekdays[now.weekday - 1]}, ${now.day} ${months[now.month - 1]}';
  }
}

class _EntryCard extends ConsumerWidget {
  final TimetableEntry entry;
  final bool lecturer;

  const _EntryCard({required this.entry, required this.lecturer});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = Theme.of(context).textTheme;
    final e = entry;
    final open = e.isOpen;

    VoidCallback? onTap;
    String? action;
    if (lecturer) {
      if (open && e.sessionId != null) {
        action = 'Show QR';
        onTap = () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => LiveQrScreen(sessionId: e.sessionId!)));
      } else if (e.sessionId != null) {
        action = 'Review';
        onTap = () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AttendanceReviewScreen(sessionId: e.sessionId!)));
      } else if (e.status == 'Upcoming') {
        action = 'Start';
        onTap = () => Navigator.of(context).push(MaterialPageRoute(
            builder: (_) => StartSessionScreen(courseId: e.courseId, courseLabel: '${e.courseCode} ${e.courseTitle}', scheduleId: e.scheduleId)));
      }
    } else if (open && e.myCheckInStatus == null) {
      action = 'Check in';
      onTap = () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ScanScreen()));
    }

    final scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Card(
        shape: open
            ? RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(IKSizes.radiusXl), side: const BorderSide(color: IKColors.success, width: 1.5))
            : null,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
              SizedBox(
                width: 56,
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(hm(e.startsAt), style: t.headlineSmall),
                  const SizedBox(height: 2),
                  Text(hm(e.endsAt), style: t.bodySmall),
                ]),
              ),
              Container(width: 1, height: 44, margin: const EdgeInsets.symmetric(horizontal: 12), color: Theme.of(context).dividerColor),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(e.courseCode, style: t.labelSmall?.copyWith(color: IKColors.primary)),
                  const SizedBox(height: 2),
                  Text(e.courseTitle, style: t.titleLarge, maxLines: 2, overflow: TextOverflow.ellipsis),
                  if (e.room != null) ...[
                    const SizedBox(height: 4),
                    Row(children: [
                      Icon(Icons.place_outlined, size: 14, color: scheme.onSurfaceVariant),
                      const SizedBox(width: 4),
                      Flexible(child: Text(e.room!, style: t.bodySmall)),
                    ]),
                  ],
                  if (open || e.myCheckInStatus != null) ...[
                    const SizedBox(height: 10),
                    Wrap(spacing: 6, runSpacing: 4, children: [
                      if (open) const StatusChip('Open', label: 'Live now'),
                      if (e.myCheckInStatus != null) StatusChip(e.myCheckInStatus!),
                    ]),
                  ],
                ]),
              ),
              if (action != null)
                Container(
                  margin: const EdgeInsets.only(left: 8),
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
                  decoration: BoxDecoration(
                    color: open ? IKColors.primary : IKColors.primary.withValues(alpha: 0.10),
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(action, style: t.labelMedium?.copyWith(color: open ? Colors.white : IKColors.primary)),
                ),
            ]),
          ),
        ),
      ),
    );
  }
}
