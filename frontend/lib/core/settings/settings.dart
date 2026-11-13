import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Overridden in main() once SharedPreferences has loaded.
final prefsProvider = Provider<SharedPreferences>((ref) => throw UnimplementedError());

class AppSettings {
  final double textScale;
  final bool highContrast;
  final bool onboarded;

  const AppSettings({this.textScale = 1.0, this.highContrast = false, this.onboarded = false});

  AppSettings copyWith({double? textScale, bool? highContrast, bool? onboarded}) => AppSettings(
        textScale: textScale ?? this.textScale,
        highContrast: highContrast ?? this.highContrast,
        onboarded: onboarded ?? this.onboarded,
      );
}

class SettingsController extends Notifier<AppSettings> {
  static const _scale = 'settings.textScale';
  static const _contrast = 'settings.highContrast';
  static const _onboarded = 'settings.onboarded';

  SharedPreferences get _prefs => ref.read(prefsProvider);

  @override
  AppSettings build() => AppSettings(
        textScale: _prefs.getDouble(_scale) ?? 1.0,
        highContrast: _prefs.getBool(_contrast) ?? false,
        onboarded: _prefs.getBool(_onboarded) ?? false,
      );

  void setTextScale(double v) {
    _prefs.setDouble(_scale, v);
    state = state.copyWith(textScale: v);
  }

  void setHighContrast(bool v) {
    _prefs.setBool(_contrast, v);
    state = state.copyWith(highContrast: v);
  }

  void markOnboarded() {
    _prefs.setBool(_onboarded, true);
    state = state.copyWith(onboarded: true);
  }
}

final settingsProvider = NotifierProvider<SettingsController, AppSettings>(SettingsController.new);
