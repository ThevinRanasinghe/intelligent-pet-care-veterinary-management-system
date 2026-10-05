import 'package:petcare_mobile/core/network/api_error.dart';
import 'package:petcare_mobile/core/network/api_client.dart';

/// A fake [ApiClient] that returns canned data or throws [ApiError]
/// based on the path.  Used in both API-integration and widget tests.
///
/// Response/error keys are substring-matched against the request path.
/// A key may be prefixed with a method ("POST /consultations") to scope
/// it to that verb — handy when GET and POST share a path. Longer keys
/// win over shorter ones so "/consultations/CON-1/submit" beats
/// "POST /consultations" for the submit call.
class FakeApiClient extends ApiClient {
  final Map<String, dynamic> _responses = {};
  final Map<String, ApiError> _errors = {};
  int unauthorizedCallCount = 0;
  final List<String> requestedPaths = [];

  /// (method, path, body) of every POST/PUT, for request-body assertions.
  final List<({String method, String path, Map<String, dynamic>? body})>
      bodyLog = [];

  FakeApiClient() : super();

  /// Body of the last POST/PUT with a non-null body (convenience).
  Map<String, dynamic>? get lastBody {
    for (final entry in bodyLog.reversed) {
      if (entry.body != null) return entry.body;
    }
    return null;
  }

  Map<String, dynamic>? bodyFor(String method, String pathContains) {
    for (final entry in bodyLog) {
      if (entry.method == method && entry.path.contains(pathContains)) {
        return entry.body;
      }
    }
    return null;
  }

  void setResponse(String pathContains, dynamic response) {
    _responses[pathContains] = response;
  }

  void setError(String pathContains, ApiError error) {
    _errors[pathContains] = error;
  }

  @override
  Future<T> get<T>(String path,
      {T Function(Map<String, dynamic>)? fromJson}) async {
    requestedPaths.add(path);
    _checkError('GET', path);
    final data = _findResponse('GET', path);
    if (data == null) return null as T;
    if (fromJson != null && data is Map<String, dynamic>) {
      return fromJson(data);
    }
    return data as T;
  }

  @override
  Future<List<T>> getList<T>(String path,
      {required T Function(Map<String, dynamic>) fromJson}) async {
    requestedPaths.add(path);
    _checkError('GET', path);
    final data = _findResponse('GET', path);
    if (data == null) return [];
    return (data as List<dynamic>)
        .map((e) => fromJson(e as Map<String, dynamic>))
        .toList();
  }

  @override
  Future<T> post<T>(String path,
      {Map<String, dynamic>? body,
      T Function(Map<String, dynamic>)? fromJson}) async {
    requestedPaths.add(path);
    bodyLog.add((method: 'POST', path: path, body: body));
    _checkError('POST', path);
    final data = _findResponse('POST', path);
    if (data == null) return null as T;
    if (fromJson != null && data is Map<String, dynamic>) {
      return fromJson(data);
    }
    return data as T;
  }

  @override
  Future<T> put<T>(String path,
      {Map<String, dynamic>? body,
      T Function(Map<String, dynamic>)? fromJson}) async {
    requestedPaths.add(path);
    bodyLog.add((method: 'PUT', path: path, body: body));
    _checkError('PUT', path);
    final data = _findResponse('PUT', path);
    if (data == null) return null as T;
    if (fromJson != null && data is Map<String, dynamic>) {
      return fromJson(data);
    }
    return data as T;
  }

  @override
  Future<void> delete(String path) async {
    requestedPaths.add(path);
    _checkError('DELETE', path);
  }

  void _checkError(String method, String path) {
    for (final entry in _sortedEntries(_errors)) {
      if (_matches(entry.key, method, path)) {
        if (entry.value.statusCode == 401) {
          unauthorizedCallCount++;
          onUnauthorized?.call();
        }
        throw entry.value;
      }
    }
  }

  dynamic _findResponse(String method, String path) {
    for (final entry in _sortedEntries(_responses)) {
      if (_matches(entry.key, method, path)) return entry.value;
    }
    return null;
  }

  /// Longest keys first so more specific stubs win.
  List<MapEntry<String, T>> _sortedEntries<T>(Map<String, T> map) =>
      map.entries.toList()
        ..sort((a, b) => b.key.length.compareTo(a.key.length));

  bool _matches(String key, String method, String path) {
    if (key.contains(' ')) {
      final sp = key.indexOf(' ');
      return key.substring(0, sp) == method &&
          path.contains(key.substring(sp + 1));
    }
    return path.contains(key);
  }
}
