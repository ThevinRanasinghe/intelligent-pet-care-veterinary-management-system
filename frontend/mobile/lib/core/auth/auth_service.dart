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

  /// Public PetOwner self-registration (POST /auth/register/pet-owner).
  /// Returns the created user summary; the caller still has to log in.
  Future<void> registerPetOwner({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    try {
      await _apiClient.post<dynamic>('/auth/register/pet-owner', body: {
        'firstName': firstName,
        'lastName': lastName,
        'email': email,
        'password': password,
        'confirmPassword': confirmPassword,
      });
    } on ApiError catch (e) {
      throw _mapError(e);
    }
  }

  /// PUT /auth/profile — updates first/last name and phone number.
  /// Returns the updated full name when the API provides one.
  Future<String?> updateProfile({
    required String firstName,
    required String lastName,
    String? phoneNumber,
  }) async {
    try {
      final json = await _apiClient.put<dynamic>('/auth/profile', body: {
        'firstName': firstName,
        'lastName': lastName,
        'phoneNumber': phoneNumber,
      });
      if (json is Map<String, dynamic>) {
        return json['fullName'] as String? ??
            '${json['firstName'] ?? ''} ${json['lastName'] ?? ''}'.trim();
      }
      return null;
    } on ApiError catch (e) {
      throw _mapError(e);
    }
  }

  /// PUT /auth/change-password — returns 204 on success.
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    try {
      await _apiClient.put<dynamic>('/auth/change-password', body: {
        'currentPassword': currentPassword,
        'newPassword': newPassword,
        'confirmNewPassword': confirmNewPassword,
      });
    } on ApiError catch (e) {
      throw _mapError(e);
    }
  }

  /// Persists a refreshed display name after a profile update.
  Future<void> updateStoredName(String name) async {
    final session = await _tokenStorage.getSession();
    if (session == null) return;
    await _tokenStorage.save(
      token: session.token,
      expiresAt: session.expiresAt,
      userId: session.userId,
      email: session.email,
      name: name,
      role: session.role,
    );
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
