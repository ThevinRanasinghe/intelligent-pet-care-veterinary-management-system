/**
 * Dynamic loader for the Google Maps JavaScript API. The script is injected
 * once and the resulting promise memoized so every consumer shares the same
 * load. A missing API key or a script failure rejects the promise so callers
 * can render their manual fallback instead of crashing.
 */
export class MapsUnavailableError extends Error {
  constructor(message = 'missing key') {
    super(message);
    this.name = 'MapsUnavailableError';
  }
}

let mapsPromise: Promise<typeof google> | null = null;

export function loadGoogleMaps(): Promise<typeof google> {
  if (mapsPromise) return mapsPromise;

  const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
  if (!key) return Promise.reject(new MapsUnavailableError('missing key'));

  const pending = new Promise<typeof google>((resolve, reject) => {
    if (typeof google !== 'undefined' && google.maps) {
      resolve(google);
      return;
    }

    const script = document.createElement('script');
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(
      key,
    )}&libraries=places&loading=async`;
    script.async = true;
    script.onload = () => resolve(google);
    script.onerror = () => reject(new MapsUnavailableError('script failed to load'));
    document.head.appendChild(script);
  });

  // Clear the memoized promise on failure so a later call can retry the load.
  mapsPromise = pending;
  pending.catch(() => {
    if (mapsPromise === pending) mapsPromise = null;
  });

  return mapsPromise;
}

/** External "Get Directions" link opened in a new tab. */
export function directionsUrl(lat: number, lng: number): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`;
}
