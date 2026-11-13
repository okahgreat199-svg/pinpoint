import 'dart:async';

import 'package:dio/dio.dart';

import '../auth/token_store.dart';
import '../config.dart';

/// The API wraps every response, errors included, in
/// `{ responseData, statusCode, status, responseMessage }`.
class ApiResponse<T> {
  final T? data;
  final int statusCode;
  final bool status;
  final String message;

  const ApiResponse({required this.data, required this.statusCode, required this.status, required this.message});

  bool get isSuccess => statusCode >= 200 && statusCode <= 299;
}

class ApiException implements Exception {
  final int statusCode;
  final String message;

  /// The raw `responseData`, e.g. a rejected check-in still carries its result.
  final Object? data;

  const ApiException(this.statusCode, this.message, [this.data]);

  /// 0 = no response (offline, DNS, timeout).
  bool get isNetwork => statusCode == 0;
  bool get isRetryable => isNetwork || statusCode >= 500;

  @override
  String toString() => message;
}

typedef AppCheckTokenSource = Future<String?> Function();

/// Thin wrapper over Dio: attaches the bearer token, refreshes once on 401,
/// adds the App Check header where the API requires it, and unwraps the envelope.
class ApiClient {
  final Dio _dio;
  final TokenStore _tokens;
  final AppCheckTokenSource _appCheck;

  /// Called when refresh fails, so the app can drop back to login.
  void Function()? onSessionExpired;

  Completer<bool>? _refreshing;

  ApiClient(this._tokens, this._appCheck, {Dio? dio})
      : _dio = dio ??
            Dio(BaseOptions(
              baseUrl: AppConfig.apiRoot,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 15),
              contentType: Headers.jsonContentType,
              // Status codes are handled from the envelope, not by Dio throwing.
              validateStatus: (_) => true,
            ));

  Future<ApiResponse<T>> get<T>(String path, {Map<String, dynamic>? query, T Function(Object? json)? parse}) =>
      _send('GET', path, query: query, parse: parse);

  Future<ApiResponse<T>> post<T>(String path, {Object? body, bool appCheck = false, bool auth = true, T Function(Object? json)? parse}) =>
      _send('POST', path, body: body, appCheck: appCheck, auth: auth, parse: parse);

  Future<ApiResponse<T>> put<T>(String path, {Object? body, T Function(Object? json)? parse}) =>
      _send('PUT', path, body: body, parse: parse);

  Future<ApiResponse<T>> patch<T>(String path, {Object? body, T Function(Object? json)? parse}) =>
      _send('PATCH', path, body: body, parse: parse);

  Future<ApiResponse<T>> delete<T>(String path, {T Function(Object? json)? parse}) =>
      _send('DELETE', path, parse: parse);

  /// Awaits [request] and returns its data, throwing [ApiException] on any non-2xx.
  Future<T> require<T>(Future<ApiResponse<T>> request) async {
    final r = await request;
    if (!r.isSuccess) throw ApiException(r.statusCode, r.message);
    return r.data as T;
  }

  Future<ApiResponse<T>> _send<T>(
    String method,
    String path, {
    Object? body,
    Map<String, dynamic>? query,
    bool appCheck = false,
    bool auth = true,
    T Function(Object? json)? parse,
  }) async {
    var response = await _raw(method, path, body, query, appCheck, auth);

    if (response.statusCode == 401 && auth && await _tokens.hasRefreshToken()) {
      if (await _refresh()) {
        response = await _raw(method, path, body, query, appCheck, auth);
      } else {
        onSessionExpired?.call();
      }
    }

    return _unwrap(response, parse);
  }

  Future<Response<dynamic>> _raw(String method, String path, Object? body, Map<String, dynamic>? query, bool appCheck, bool auth) async {
    final headers = <String, dynamic>{};
    if (auth) {
      final access = await _tokens.accessToken();
      if (access != null) headers['Authorization'] = 'Bearer $access';
    }
    if (appCheck) {
      final token = await _appCheck();
      if (token != null) headers['X-Firebase-AppCheck'] = token;
    }

    try {
      return await _dio.request<dynamic>(path,
          data: body, queryParameters: query, options: Options(method: method, headers: headers));
    } on DioException catch (e) {
      throw ApiException(0, _networkMessage(e));
    }
  }

  ApiResponse<T> _unwrap<T>(Response<dynamic> response, T Function(Object? json)? parse) {
    final json = response.data;
    final code = response.statusCode ?? 0;
    if (json is! Map<String, dynamic>) {
      return ApiResponse(data: null, statusCode: code, status: false, message: _fallbackMessage(code));
    }
    final raw = json['responseData'];
    final ok = code >= 200 && code <= 299;
    return ApiResponse<T>(
      data: raw == null ? null : (parse == null ? raw as T? : _tryParse(parse, raw)),
      statusCode: (json['statusCode'] as num?)?.toInt() ?? code,
      status: json['status'] as bool? ?? ok,
      message: json['responseMessage'] as String? ?? _fallbackMessage(code),
    );
  }

  static T? _tryParse<T>(T Function(Object? json) parse, Object raw) {
    try {
      return parse(raw);
    } catch (_) {
      return null;
    }
  }

  /// Single-flight refresh: concurrent 401s wait on the same attempt (refresh tokens rotate).
  Future<bool> _refresh() async {
    final inFlight = _refreshing;
    if (inFlight != null) return inFlight.future;

    final completer = _refreshing = Completer<bool>();
    try {
      final access = await _tokens.accessToken();
      final refresh = await _tokens.refreshToken();
      if (access == null || refresh == null) {
        completer.complete(false);
        return false;
      }
      final r = await _raw('POST', '/auth/refresh', {'accessToken': access, 'refreshToken': refresh}, null, false, false);
      final data = (r.data is Map ? r.data['responseData'] : null) as Map<String, dynamic>?;
      final ok = r.statusCode == 200 && data != null;
      if (ok) {
        await _tokens.save(data['accessToken'] as String, data['refreshToken'] as String);
      } else {
        await _tokens.clear();
      }
      completer.complete(ok);
      return ok;
    } on ApiException {
      // Offline: keep the tokens, the next call can try again.
      completer.complete(false);
      return false;
    } finally {
      _refreshing = null;
    }
  }

  static String _networkMessage(DioException e) => switch (e.type) {
        DioExceptionType.connectionTimeout || DioExceptionType.receiveTimeout || DioExceptionType.sendTimeout =>
          'The server took too long to respond.',
        _ => 'No connection to the server.',
      };

  static String _fallbackMessage(int code) => switch (code) {
        429 => 'Too many attempts. Wait a minute and try again.',
        >= 500 => 'The server had a problem. Try again.',
        _ => 'Unexpected response ($code).',
      };
}
