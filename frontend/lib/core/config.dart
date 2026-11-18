/// Build-time configuration. Nothing secret lives here; values come from `--dart-define`.
///
///   flutter run --dart-define=API_BASE_URL=https://seatcharter.example.com
class AppConfig {
  AppConfig._();

  /// Android emulator reaches the host machine on 10.0.2.2.
  static const String apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5080');

  static String get apiRoot => '$apiBaseUrl/api/v1';

  /// Shown on the register screen for lecturers; the API checks it.
  static const bool showLecturerSignup = bool.fromEnvironment('SHOW_LECTURER_SIGNUP', defaultValue: true);

  static const Duration qrPollInterval = Duration(seconds: 5);
}
