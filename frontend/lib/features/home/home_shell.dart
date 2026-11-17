import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/auth/auth_controller.dart';
import '../lecturer/courses_screen.dart';
import '../lecturer/device_requests_screen.dart';
import '../settings/settings_screen.dart';
import '../student/my_attendance_screen.dart';
import '../student/scan_screen.dart';
import 'timetable_screen.dart';

/// Bottom navigation, one tab set per role.
class HomeShell extends ConsumerStatefulWidget {
  const HomeShell({super.key});

  @override
  ConsumerState<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends ConsumerState<HomeShell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);

    final tabs = user.isLecturer
        ? const [
            (Icons.calendar_today_outlined, Icons.calendar_today_rounded, 'Today', TimetableScreen()),
            (Icons.school_outlined, Icons.school_rounded, 'Courses', CoursesScreen()),
            (Icons.smartphone_outlined, Icons.smartphone_rounded, 'Devices', DeviceRequestsScreen()),
            (Icons.tune_outlined, Icons.tune_rounded, 'Settings', SettingsScreen()),
          ]
        : const [
            (Icons.calendar_today_outlined, Icons.calendar_today_rounded, 'Today', TimetableScreen()),
            (Icons.qr_code_scanner_outlined, Icons.qr_code_scanner_rounded, 'Check in', ScanTab()),
            (Icons.insights_outlined, Icons.insights_rounded, 'Attendance', MyAttendanceScreen()),
            (Icons.tune_outlined, Icons.tune_rounded, 'Settings', SettingsScreen()),
          ];

    final index = _index.clamp(0, tabs.length - 1);
    return Scaffold(
      body: IndexedStack(index: index, children: [for (final t in tabs) t.$4]),
      bottomNavigationBar: DecoratedBox(
        decoration: BoxDecoration(border: Border(top: BorderSide(color: Theme.of(context).dividerColor))),
        child: NavigationBar(
          selectedIndex: index,
          onDestinationSelected: (i) => setState(() => _index = i),
          destinations: [for (final t in tabs) NavigationDestination(icon: Icon(t.$1), selectedIcon: Icon(t.$2), label: t.$3)],
        ),
      ),
    );
  }
}
