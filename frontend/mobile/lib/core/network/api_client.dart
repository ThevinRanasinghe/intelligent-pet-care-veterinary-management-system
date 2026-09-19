import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import '../config/api_config.dart';
import 'api_error.dart';

/// Thin HTTP wrapper that:
/// - attaches the Bearer token when one is set
/// - throws [ApiError] on non-2xx responses
/// - clears the token on 401 via the [onUnauthorized] callback
class ApiClient {
  final http.Client _client;
  String? token;
  FutureOr<void> Function()? onUnauthorized;

  ApiClient({http.Client? client, this.onUnauthorized})
      : _client = client ?? http.Client();

  void setToken(String? token) {
    this.token = token;
  }

  Future<T> get<T>(String path, {T Function(Map<String, dynamic>)? fromJson}) {
    return _request<T>('GET', path, fromJson: fromJson);
  }

  Future<List<T>> getList<T>(String path, {required T Function(Map<String, dynamic>) fromJson}) async {
    final json = await _requestRaw('GET', path);
    final list = json as List<dynamic>;
    return list.map((e) => fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<T> post<T>(String path, {Map<String, dynamic>? body, T Function(Map<String, dynamic>)? fromJson}) {
    return _request<T>('POST', path, body: body, fromJson: fromJson);
  }

  Future<void> delete(String path) async {
    await _requestRaw('DELETE', path);
  }

  Future<dynamic> _requestRaw(String method, String path, {Map<String, dynamic>? body}) async {
    final uri = Uri.parse('${ApiConfig.baseUrl}$path');
    final headers = <String, String>{
      'Content-Type': 'application/json',
      if (token != null) 'Authorization': 'Bearer $token',
    };

    final request = http.Request(method, uri);
    request.headers.addAll(headers);
    if (body != null) request.body = jsonEncode(body);
    final streamed = await _client.send(request);
    final response = await http.Response.fromStream(streamed);

    if (response.statusCode == 401) {
      setToken(null);
      await onUnauthorized?.call();
    }

    if (response.statusCode < 200 || response.statusCode >= 300) {
      final text = response.body;
      dynamic parsed;
      try {
        parsed = text.isEmpty ? null : jsonDecode(text);
      } catch (_) {
        parsed = text;
      }
      final message = response.statusCode == 403
          ? 'Access denied. You do not have permission to perform this action.'
          : 'API request failed: ${response.statusCode}';
      throw ApiError(response.statusCode, message, parsed);
    }

    if (response.statusCode == 204 || response.body.isEmpty) {
      return null;
    }

    return jsonDecode(response.body);
  }

  Future<T> _request<T>(String method, String path, {Map<String, dynamic>? body, T Function(Map<String, dynamic>)? fromJson}) async {
    final json = await _requestRaw(method, path, body: body);
    if (json == null) return null as T;
    if (fromJson != null) {
      return fromJson(json as Map<String, dynamic>);
    }
    return json as T;
  }
}
