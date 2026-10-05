import 'dart:async';
import 'dart:js_interop';
import 'dart:js_interop_unsafe';

import 'package:web/web.dart' as web;

/// Injects the Google Maps JavaScript SDK on demand, using the key supplied
/// via --dart-define=GOOGLE_MAPS_API_KEY=... so no key ever lives in
/// web/index.html or other tracked files.
Future<bool>? _pending;

bool _mapsApiAvailable() {
  if (!web.window.hasProperty('google'.toJS).toDart) return false;
  final google = web.window.getProperty<JSObject?>('google'.toJS);
  return google != null && google.hasProperty('maps'.toJS).toDart;
}

Future<bool> _inject(String apiKey) {
  final completer = Completer<bool>();
  final script = web.document.createElement('script') as web.HTMLScriptElement
    ..src =
        'https://maps.googleapis.com/maps/api/js?key=$apiKey&loading=async'
    ..async = true
    ..defer = true;
  script.onLoad.first.then((_) => completer.complete(true));
  script.onError.first.then((_) => completer.complete(false));
  web.document.head?.appendChild(script);
  return completer.future;
}

/// Ensures `google.maps` is available on the page. Returns false when no key
/// was configured or the script failed to load — callers must degrade.
Future<bool> ensureGoogleMapsLoaded(String apiKey) {
  if (_mapsApiAvailable()) return Future.value(true);
  if (apiKey.isEmpty) return Future.value(false);
  return _pending ??= _inject(apiKey);
}
