import '../network/api_client.dart';
import '../network/api_error.dart';
import 'token_storage.dart';

class AuthService {
  final ApiClient _apiClient;
  final TokenStorage _tokenStorage;

  AuthService(this._apiClient, this._tokenStorage);

  Future<AuthSession> login(String email, String password) async {
    try {
      final json = await _apiClient.post<dynamic>('/auth/login', body: {
        'email': email,
        'password': password,
      });
      final map = json as Map<String, dynamic>;
      final session = AuthSession(
        token: map['token'] as String,
        expiresAt: map['expiresAt'] as String,
        userId: map['userId'] as String,
        email: map['email'] as String,
        name: map['name'] as String,
        role: map['role'] as String,
      );
      await _tokenStorage.save(
        token: session.token,
        expiresAt: session.expiresAt,
        userId: session.userId,
        email: session.email,
        name: session.name,
        role: session.role,
      );
      _apiClient.setToken(session.token);
      return session;
    } on ApiError catch (e) {
      throw _mapError(e);
    }
  }

  Future<void> logout() async {
    await _tokenStorage.clear();
    _apiClient.setToken(null);
  }

  Future<AuthSession?> restoreSession() async {
    final session = await _tokenStorage.getSession();
    if (session != null) {
      _apiClient.setToken(session.token);
    }
    return session;
  }

  String _mapError(ApiError e) {
    final body = e.body;
    if (body is Map<String, dynamic>) {
      return body['detail'] as String? ?? body['title'] as String? ?? e.message;
    }
    if (body is String) return body;
    return e.message;
  }
}
