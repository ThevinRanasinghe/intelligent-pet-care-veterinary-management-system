import 'package:flutter/foundation.dart';
import '../../core/auth/auth_service.dart';
import '../../core/auth/token_storage.dart';

enum AuthStatus { unknown, authenticated, unauthenticated }

class AuthProvider extends ChangeNotifier {
  final AuthService _authService;
  AuthStatus _status = AuthStatus.unknown;
  AuthSession? _session;
  String? _errorMessage;

  AuthProvider(this._authService);

  AuthStatus get status => _status;
  AuthSession? get session => _session;
  String? get errorMessage => _errorMessage;
  bool get isAuthenticated => _status == AuthStatus.authenticated;
  bool get isPetOwner => _session?.role == 'PetOwner';
  bool get isClinicManager => _session?.role == 'ClinicManager';

  Future<void> init() async {
    _session = await _authService.restoreSession();
    _status = _session != null
        ? AuthStatus.authenticated
        : AuthStatus.unauthenticated;
    notifyListeners();
  }

  Future<bool> login(String email, String password) async {
    _errorMessage = null;
    try {
      _session = await _authService.login(email, password);
      _status = AuthStatus.authenticated;
      notifyListeners();
      return true;
    } catch (e) {
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      _status = AuthStatus.unauthenticated;
      notifyListeners();
      return false;
    }
  }

  /// PetOwner self-registration. Returns true on success; on failure the
  /// API message is exposed via [errorMessage].
  Future<bool> register({
    required String firstName,
    required String lastName,
    required String email,
    required String password,
    required String confirmPassword,
  }) async {
    _errorMessage = null;
    try {
      await _authService.registerPetOwner(
        firstName: firstName,
        lastName: lastName,
        email: email,
        password: password,
        confirmPassword: confirmPassword,
      );
      return true;
    } catch (e) {
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      notifyListeners();
      return false;
    }
  }

  /// PUT /auth/profile. On success the stored display name is refreshed.
  Future<bool> updateProfile({
    required String firstName,
    required String lastName,
    String? phoneNumber,
  }) async {
    _errorMessage = null;
    try {
      final fullName = await _authService.updateProfile(
        firstName: firstName,
        lastName: lastName,
        phoneNumber: phoneNumber,
      );
      if (fullName != null && fullName.isNotEmpty && _session != null) {
        await _authService.updateStoredName(fullName);
        _session = AuthSession(
          token: _session!.token,
          expiresAt: _session!.expiresAt,
          userId: _session!.userId,
          email: _session!.email,
          name: fullName,
          role: _session!.role,
        );
      }
      notifyListeners();
      return true;
    } catch (e) {
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      notifyListeners();
      return false;
    }
  }

  /// PUT /auth/change-password. Returns true on success.
  Future<bool> changePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmNewPassword,
  }) async {
    _errorMessage = null;
    try {
      await _authService.changePassword(
        currentPassword: currentPassword,
        newPassword: newPassword,
        confirmNewPassword: confirmNewPassword,
      );
      return true;
    } catch (e) {
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      notifyListeners();
      return false;
    }
  }

  Future<void> logout() async {
    await _authService.logout();
    _session = null;
    _status = AuthStatus.unauthenticated;
    notifyListeners();
  }

  void clearError() {
    _errorMessage = null;
    notifyListeners();
  }
}
