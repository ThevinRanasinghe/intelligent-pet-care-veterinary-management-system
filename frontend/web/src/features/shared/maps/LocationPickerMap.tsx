import { useEffect, useRef, useState } from 'react';
import { loadGoogleMaps } from '../../../lib/googleMaps';

const DEFAULT_CENTER = { lat: 6.9271, lng: 79.8612 }; // Colombo
const DEFAULT_ZOOM = 12;

export interface MapLocation {
  lat: number;
  lng: number;
}

interface LocationPickerMapProps {
  value?: MapLocation;
  onConfirm: (location: MapLocation) => void;
  onCancel: () => void;
}

const buttonStyle = {
  height: '36px',
  padding: '0 14px',
  border: '1px solid #d5d5cf',
  borderRadius: '8px',
  fontSize: '11px',
  fontWeight: 700,
  cursor: 'pointer',
} as const;

/** Accepts both google.maps.LatLng (methods) and plain {lat,lng} literals. */
function toLocation(point: { lat: unknown; lng: unknown }): MapLocation | null {
  const lat = typeof point.lat === 'function' ? point.lat() : point.lat;
  const lng = typeof point.lng === 'function' ? point.lng() : point.lng;
  return typeof lat === 'number' && typeof lng === 'number' ? { lat, lng } : null;
}

/**
 * Interactive pin picker for the organization registration flow. When the
 * Maps API cannot load, a non-blocking notice with a retry keeps registration
 * usable — the location is optional and can be added later.
 */
export function LocationPickerMap({ value, onConfirm, onCancel }: LocationPickerMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const searchHostRef = useRef<HTMLDivElement | null>(null);
  const markerRef = useRef<any>(null);

  const [pin, setPin] = useState<MapLocation | null>(value ?? null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let disposed = false;
    let disposedCleanup: (() => void) | null = null;

    loadGoogleMaps()
      .then((g) => {
        if (disposed || !containerRef.current) return;

        const map = new g.maps.Map(containerRef.current, {
          center: value ?? DEFAULT_CENTER,
          zoom: DEFAULT_ZOOM,
        });

        const placePin = (location: MapLocation) => {
          if (markerRef.current) {
            markerRef.current.setPosition(location);
          } else {
            markerRef.current = new g.maps.Marker({
              map,
              position: location,
              draggable: true,
            });
            markerRef.current.addListener('dragend', () => {
              const dragged = toLocation(markerRef.current.getPosition());
              if (dragged) setPin(dragged);
            });
          }
          setPin(location);
        };

        map.addListener('click', (event: { latLng?: { lat: unknown; lng: unknown } }) => {
          const location = event.latLng ? toLocation(event.latLng) : null;
          if (location) placePin(location);
        });

        if (value) placePin(value);

        const host = searchHostRef.current;
        if (host && g.maps.places?.PlaceAutocompleteElement) {
          // Places API (New): the element renders its own search input and
          // fires `gmp-select` with a PlacePrediction. fetchFields resolves
          // the selection's LatLng; the pin stays authoritative.
          const placeSelect = new g.maps.places.PlaceAutocompleteElement();
          placeSelect.setAttribute?.('placeholder', 'Search for your clinic address');
          placeSelect.style?.setProperty?.('width', '100%');
          host.appendChild(placeSelect);
          placeSelect.addEventListener('gmp-select', async (event: any) => {
            const place = event?.placePrediction?.toPlace?.();
            if (!place) return;
            await place.fetchFields({ fields: ['location'] });
            const location = place.location ? toLocation(place.location) : null;
            if (location) {
              map.setCenter(location);
              placePin(location);
            }
          });
          disposedCleanup = () => placeSelect.remove();
        } else if (host) {
          // Places library unavailable — keep the layout stable; the map
          // click pin still works as the primary input.
          const input = document.createElement('input');
          input.type = 'text';
          input.setAttribute('aria-label', 'Search address');
          input.placeholder = 'Search for your clinic address';
          input.disabled = true;
          Object.assign(input.style, {
            width: '100%', height: '38px', marginBottom: '8px', padding: '0 12px',
            border: '1px solid #d8d8d2', borderRadius: '8px', fontSize: '12px',
          });
          host.appendChild(input);
          disposedCleanup = () => input.remove();
        }
      })
      .catch(() => {
        if (!disposed) setLoadFailed(true);
      });

    return () => {
      disposed = true;
      disposedCleanup?.();
    };
    // The initial value seeds the picker; later edits go through the pin.
    // `attempt` re-runs the load when the user picks "Try Again".
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attempt]);

  const actions = (
    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '10px' }}>
      <button type="button" onClick={onCancel} style={{ ...buttonStyle, background: '#ffffff', color: '#444444' }}>
        Cancel
      </button>
      <button
        type="button"
        disabled={!pin}
        onClick={() => pin && onConfirm(pin)}
        style={{ ...buttonStyle, border: '1px solid #d6aa00', background: '#f5c400', color: '#171717', opacity: pin ? 1 : 0.55 }}
      >
        Confirm Location
      </button>
    </div>
  );

  if (loadFailed) {
    return (
      <div
        data-testid="map-fallback"
        style={{ padding: '14px', border: '1px solid #eadf9c', borderRadius: '9px', background: '#fffbea' }}
      >
        <p style={{ margin: '0 0 10px', fontSize: '12px', color: '#735900' }}>
          Map location is temporarily unavailable. You can continue registration and add the clinic location later.
        </p>
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
          <button
            type="button"
            onClick={onCancel}
            style={{ ...buttonStyle, background: '#ffffff', color: '#444444' }}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => {
              setLoadFailed(false);
              setAttempt((a) => a + 1);
            }}
            style={{ ...buttonStyle, border: '1px solid #d6aa00', background: '#f5c400', color: '#171717' }}
          >
            Try Again
          </button>
        </div>
      </div>
    );
  }

  return (
    <div data-testid="location-picker-map">
      <p style={{ margin: '0 0 8px', fontSize: '12px', fontWeight: 700, color: '#4b4b4b' }}>
        Select Clinic Location
      </p>
      <div
        ref={searchHostRef}
        data-testid="place-autocomplete-host"
        style={{ marginBottom: '8px' }}
      />
      <div
        ref={containerRef}
        style={{ height: '300px', borderRadius: '8px', border: '1px solid #d8d8d2', overflow: 'hidden' }}
      />
      <p style={{ margin: '8px 0 0', fontSize: '11px', color: '#555555' }}>
        {pin
          ? '✓ Pin placed — drag to adjust, then confirm.'
          : 'Search for the area, then click the map to place the pin on your clinic. Drag the pin to adjust.'}
      </p>
      {actions}
    </div>
  );
}

export default LocationPickerMap;
