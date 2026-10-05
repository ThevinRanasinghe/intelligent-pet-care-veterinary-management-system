class ApiConfig {
  const ApiConfig._();

  /// Override at compile time with --dart-define=API_BASE_URL=...
  /// or at runtime by editing this fallback.
  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5080/api',
  );
}
