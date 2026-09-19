import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/core/network/api_client.dart';

/// A fake [ApiClient] that returns canned data or throws [ApiError]
/// based on the path.  Used in both API-integration and widget tests.
class FakeApiClient extends ApiClient {
  final Map<String, dynamic> _responses = {};
  final Map<String, ApiError> _errors = {};
  int unauthorizedCallCount = 0;
  final List<String> requestedPaths = [];

  FakeApiClient() : super();

  void setResponse(String pathContains, dynamic response) {
    _responses[pathContains] = response;
  }

  void setError(String pathContains, ApiError error) {
    _errors[pathContains] = error;
  }

  @override
  Future<T> get<T>(String path, {T Function(Map<String, dynamic>)? fromJson}) async {
    requestedPaths.add(path);
    _checkError(path);
    final data = _findResponse(path);
    if (data == null) return null as T;
    if (fromJson != null && data is Map<String, dynamic>) {
      return fromJson(data);
    }
    return data as T;
  }

  @override
  Future<List<T>> getList<T>(String path, {required T Function(Map<String, dynamic>) fromJson}) async {
    requestedPaths.add(path);
    _checkError(path);
    final data = _findResponse(path);
    if (data == null) return [];
    return (data as List<dynamic>).map((e) => fromJson(e as Map<String, dynamic>)).toList();
  }

  @override
  Future<T> post<T>(String path, {Map<String, dynamic>? body, T Function(Map<String, dynamic>)? fromJson}) async {
    requestedPaths.add(path);
    _checkError(path);
    final data = _findResponse(path);
    if (data == null) return null as T;
    if (fromJson != null && data is Map<String, dynamic>) {
      return fromJson(data);
    }
    return data as T;
  }

  @override
  Future<void> delete(String path) async {
    requestedPaths.add(path);
    _checkError(path);
  }

  void _checkError(String path) {
    for (final entry in _errors.entries) {
      if (path.contains(entry.key)) {
        if (entry.value.statusCode == 401) {
          unauthorizedCallCount++;
          onUnauthorized?.call();
        }
        throw entry.value;
      }
    }
  }

  dynamic _findResponse(String path) {
    for (final entry in _responses.entries) {
      if (path.contains(entry.key)) return entry.value;
    }
    return null;
  }
}
