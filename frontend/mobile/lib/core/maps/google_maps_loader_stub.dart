/// No-op on non-web platforms — the Maps SDK key is configured natively
/// (AndroidManifest placeholder on Android, AppDelegate on iOS).
Future<bool> ensureGoogleMapsLoaded(String apiKey) async => false;
