import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import '../../widgets/common.dart';
import '../data.dart';

/// BRD screen 9: approve or deny a student's switch to a new phone.
class DeviceRequestsScreen extends ConsumerWidget {
  const DeviceRequestsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final pending = ref.watch(pendingDevicesProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Device requests')),
      body: RefreshIndicator(
        onRefresh: () => ref.refresh(pendingDevicesProvider.future),
        child: AsyncBody(
          value: pending,
          onRetry: () => ref.invalidate(pendingDevicesProvider),
          data: (list) => list.isEmpty
              ? ListView(children: const [SizedBox(height: 80), EmptyView(icon: Icons.verified_user_outlined, message: 'No pending device changes.')])
              : ListView.separated(
                  padding: const EdgeInsets.all(20),
                  itemCount: list.length,
                  separatorBuilder: (_, __) => const Divider(),
                  itemBuilder: (_, i) {
                    final d = list[i];
                    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(d.studentName, style: Theme.of(context).textTheme.titleLarge),
                      if (d.matricNo != null) Text(d.matricNo!),
                      const SizedBox(height: 4),
                      Text('New: ${d.model} · requested ${dayLabel(d.boundAt).toLowerCase()} ${hm(d.boundAt)}'),
                      Text('Current: ${d.currentDeviceModel ?? 'none'}'),
                      const SizedBox(height: 10),
                      Row(children: [
                        Expanded(
                          child: SecondaryButton(
                            label: 'Deny',
                            onPressed: () async {
                              if (await guarded(context, () => ref.read(apiProvider).rejectDevice(d.id))) ref.invalidate(pendingDevicesProvider);
                            },
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: PrimaryButton(
                            label: 'Approve',
                            onPressed: () async {
                              if (await guarded(context, () => ref.read(apiProvider).approveDevice(d.id))) ref.invalidate(pendingDevicesProvider);
                            },
                          ),
                        ),
                      ]),
                    ]);
                  },
                ),
        ),
      ),
    );
  }
}
