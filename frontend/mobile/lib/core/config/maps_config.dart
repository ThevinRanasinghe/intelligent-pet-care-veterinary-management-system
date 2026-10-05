class MapsConfig {
  const MapsConfig._();

  /// Google Maps JavaScript SDK key for the web build, supplied at compile
  /// time via --dart-define=GOOGLE_MAPS_API_KEY=...
  /// (see tool/flutter_web.ps1). Without a key the clinic picker falls back
  /// to its list view. Android reads the key from android/local.properties.
  static const String googleMapsApiKey =
      String.fromEnvironment('GOOGLE_MAPS_API_KEY', defaultValue: '');
}
