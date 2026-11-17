import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/models.dart';
import '../../core/providers.dart';
import '../../utils/constants/colors.dart';
import '../../widgets/common.dart';
import '../data.dart';

/// BRD screen 8: roster by status; tap a flagged student to see why and resolve.
class AttendanceReviewScreen extends ConsumerStatefulWidget {
  final String sessionId;

  const AttendanceReviewScreen({super.key, required this.sessionId});

  @override
  ConsumerState<AttendanceReviewScreen> createState() => _AttendanceReviewScreenState();
}

class _AttendanceReviewScreenState extends ConsumerState<AttendanceReviewScreen> {
  String _filter = 'All';

  Future<void> _resolve(RosterRow row) async {
    final note = TextEditingController();
    final decision = await showModalBottomSheet<String>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.viewInsetsOf(ctx).bottom + 20),
        child: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(row.fullName, style: Theme.of(ctx).textTheme.headlineMedium),
            if (row.matricNo != null) Text(row.matricNo!),
            const SizedBox(height: 12),
            _Evidence(row: row),
            const SizedBox(height: 12),
            LabeledField(label: 'Note (saved to the audit log)', controller: note, action: TextInputAction.done),
            Row(children: [
              Expanded(child: SecondaryButton(label: 'Mark absent', icon: Icons.close, onPressed: () => Navigator.pop(ctx, 'Absent'))),
              const SizedBox(width: 10),
              Expanded(child: PrimaryButton(label: 'Mark present', icon: Icons.check, onPressed: () => Navigator.pop(ctx, 'Present'))),
            ]),
          ]),
        ),
      ),
    );
    if (decision == null || row.checkInId == null || !mounted) return;
    final ok = await guarded(
        context, () => ref.read(apiProvider).review(row.checkInId!, decision: decision, note: note.text.trim().isEmpty ? null : note.text.trim()));
    if (ok) ref.invalidate(attendanceProvider(widget.sessionId));
  }

  @override
  Widget build(BuildContext context) {
    final value = ref.watch(attendanceProvider(widget.sessionId));
    final t = Theme.of(context).textTheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Attendance')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(attendanceProvider(widget.sessionId).future),
        child: AsyncBody(
          value: value,
          onRetry: () => ref.invalidate(attendanceProvider(widget.sessionId)),
          data: (a) {
            final rows = _filter == 'All' ? a.roster : a.roster.where((r) => r.status == _filter).toList();
            return ListView(padding: const EdgeInsets.all(20), children: [
              Text('${a.session.courseCode} · ${dayLabel(a.session.startsAt)} ${hm(a.session.startsAt)}', style: t.headlineSmall),
              Text(a.session.isOpen ? 'Session open' : 'Session closed', style: t.bodyMedium),
              const SizedBox(height: 15),
              Wrap(spacing: 8, runSpacing: 8, children: [
                for (final (label, n) in [('All', a.enrolled), ('Present', a.present), ('Flagged', a.flagged), ('Absent', a.absent)])
                  ChoiceChip(label: Text('$label $n'), selected: _filter == label, onSelected: (_) => setState(() => _filter = label)),
              ]),
              const SizedBox(height: 10),
              if (rows.isEmpty) const Padding(padding: EdgeInsets.all(20), child: Text('Nobody here.', textAlign: TextAlign.center)),
              for (final r in rows)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(r.fullName),
                  subtitle: Text([
                    if (r.at != null) hm(r.at!),
                    if (r.flags.isNotEmpty) r.flags.map(humanise).join(', '),
                    if (r.reviewedAt != null) 'reviewed',
                    if (r.rejectedAttempts > 0) '${r.rejectedAttempts} rejected attempt${r.rejectedAttempts == 1 ? '' : 's'}',
                  ].join(' · ')),
                  trailing: StatusChip(r.status),
                  onTap: r.checkInId == null ? null : () => _resolve(r),
                ),
              if (a.rejected.isNotEmpty) ...[
                const SizedBox(height: 20),
                Text('Rejected attempts', style: t.headlineSmall),
                for (final x in a.rejected)
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.block, color: IKColors.danger),
                    title: Text(x.fullName),
                    subtitle: Text('${hm(x.at)} · ${humanise(x.reason)}'),
                  ),
              ],
            ]);
          },
        ),
      ),
    );
  }
}

class _Evidence extends StatelessWidget {
  final RosterRow row;

  const _Evidence({required this.row});

  static const _explain = {
    'FarFromLecturer': 'Checked in more than 75 m from where the session was started.',
    'LocationUnverified': 'Location accuracy was too poor to confirm distance.',
    'MockLocation': 'The phone reported a mock-location app.',
    'ClusterOutlier': 'Far from where most of the class checked in.',
    'LeftEarly': 'Left the room before 70% of the session.',
    'NoPresenceResponse': 'Did not answer the presence check in time.',
  };

  @override
  Widget build(BuildContext context) {
    final lines = <String>[
      for (final f in row.flags) '• ${humanise(f)}: ${_explain[f] ?? ''}',
      if (row.distanceFromAnchorM != null) '• ${row.distanceFromAnchorM!.round()} m from anchor (±${row.accuracyM?.round() ?? '?'} m accuracy)',
      for (final p in row.presenceEvents) '• ${humanise(p.kind)} at ${hm(p.at)}: ${humanise(p.result)}',
      if (row.reviewNote != null) '• Earlier note: ${row.reviewNote}',
    ];
    return Text(lines.isEmpty ? 'No flags.' : lines.join('\n'), style: Theme.of(context).textTheme.bodyLarge);
  }
}
