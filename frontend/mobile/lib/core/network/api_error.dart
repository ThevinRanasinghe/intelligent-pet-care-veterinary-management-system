class ApiError implements Exception {
  final int statusCode;
  final String message;
  final dynamic body;

  ApiError(this.statusCode, this.message, [this.body]);

  @override
  String toString() => 'ApiError($statusCode): $message';
}
