import { useEffect, useRef, useState } from 'react';
import { directionsUrl, loadGoogleMaps } from '../../../lib/googleMaps';
import { lookupsService, type OrganizationLookup } from '../../../services/lookupsService';

const DEFAULT_CENTER = { lat: 6.9271, lng: 79.8612 }; // Colombo
const DEFAULT_ZOOM = 12;

interface ClinicMapProps {
  clinics: OrganizationLookup[];
  selectedId?: string;
  onSelect: (clinic: OrganizationLookup) => void;
}

const cardStyle = {
  padding: '10px 12px',
  border: '1px solid #e4e4dc',
  borderRadius: '9px',
  background: '#ffffff',
  fontSize: '12px',
} as const;

const smallButtonStyle = {
  height: '30px',
  padding: '0 12px',
  border: '1px solid #d6aa00',
  borderRadius: '7px',
  background: '#f5c400',
  color: '#171717',
  fontSize: '11px',
  fontWeight: 700,
  cursor: 'pointer',
} as const;

function ClinicCard({
  clinic,
  distanceKm,
  selected,
  onSelect,
}: {
  clinic: OrganizationLookup;
  distanceKm?: number;
  selected: boolean;
  onSelect: (clinic: OrganizationLookup) => void;
}) {
  const hasCoords = clinic.latitude != null && clinic.longitude != null;

  return (
    <div data-testid="clinic-card" style={cardStyle}>
      <div style={{ fontWeight: 800, color: '#242424' }}>
        {clinic.name}
        {clinic.city ? ` — ${clinic.city}` : ''}
      </div>
      {clinic.address && (
        <div style={{ marginTop: '3px', color: '#666666' }}>{clinic.address}</div>
      )}
      {typeof distanceKm === 'number' && (
        <div style={{ marginTop: '3px', color: '#9a7200', fontWeight: 700 }}>
          {distanceKm.toFixed(2)} km away
        </div>
      )}
      <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginTop: '8px' }}>
        <button type="button" onClick={() => onSelect(clinic)} style={smallButtonStyle}>
          {selected ? 'Selected' : 'Select Clinic'}
        </button>
        {hasCoords && (
          <a
            href={directionsUrl(clinic.latitude as number, clinic.longitude as number)}
            target="_blank"
            rel="noopener"
            style={{ fontSize: '11px', fontWeight: 700, color: '#9a7200' }}
          >
            Get Directions
          </a>
        )}
      </div>
    </div>
  );
}

/**
 * Clinic picker for the owner booking flow. Renders one marker per clinic
 * that has stored coordinates (coordinate-less clinics are silently skipped
 * on the map but stay selectable in the list). When Maps cannot load the
 * list alone is rendered so booking is never blocked.
 */
export function ClinicMap({ clinics, selectedId, onSelect }: ClinicMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<any>(null);

  const [loadFailed, setLoadFailed] = useState(false);
  const [retryAttempt, setRetryAttempt] = useState(0);
  const [infoClinic, setInfoClinic] = useState<OrganizationLookup | null>(null);
  const [distances, setDistances] = useState<Record<string, number>>({});

  const located = clinics.filter((c) => c.latitude != null && c.longitude != null);

  useEffect(() => {
    let disposed = false;

    loadGoogleMaps()
      .then((g) => {
        if (disposed || !containerRef.current) return;

        const map = new g.maps.Map(containerRef.current, {
          center: DEFAULT_CENTER,
          zoom: DEFAULT_ZOOM,
        });
        mapRef.current = map;

        located.forEach((clinic) => {
          const marker = new g.maps.Marker({
            map,
            position: { lat: clinic.latitude, lng: clinic.longitude },
            title: clinic.name,
          });
          marker.addListener('click', () => setInfoClinic(clinic));
        });

        // Fit the map to the located clinics so every registered clinic is
        // visible without manual panning.
        if (located.length === 1) {
          map.setCenter({ lat: located[0].latitude, lng: located[0].longitude });
        } else if (located.length > 1) {
          const bounds = new g.maps.LatLngBounds();
          located.forEach((clinic) =>
            bounds.extend({ lat: clinic.latitude, lng: clinic.longitude }),
          );
          map.fitBounds(bounds);
        }

        if (!disposed) setLoadFailed(false);
      })
      .catch(() => {
        if (!disposed) setLoadFailed(true);
      });

    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clinics, retryAttempt]);

  const handleUseMyLocation = () => {
    if (typeof navigator === 'undefined' || !navigator.geolocation) return;

    navigator.geolocation.getCurrentPosition(
      async (position) => {
        const lat = position.coords.latitude;
        const lng = position.coords.longitude;
        mapRef.current?.setCenter({ lat, lng });
        try {
          const nearby = await lookupsService.getNearbyClinics(lat, lng);
          setDistances(
            Object.fromEntries(nearby.map((n) => [n.id, n.distanceKm])),
          );
        } catch {
          // Distances are nice-to-have; the map stays usable without them.
        }
      },
      () => {
        // Permission denied — keep the default centre, surface no error.
      },
    );
  };

  const list = (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
      {clinics.map((clinic) => (
        <ClinicCard
          key={clinic.id}
          clinic={clinic}
          distanceKm={distances[clinic.id]}
          selected={selectedId === clinic.id}
          onSelect={onSelect}
        />
      ))}
    </div>
  );

  if (loadFailed) {
    return (
      <div data-testid="clinic-map-fallback">
        <p style={{ margin: '0 0 10px', fontSize: '11px', color: '#735900' }}>
          Unable to load the clinic map. Please try again.
        </p>
        <button
          type="button"
          onClick={() => {
            setLoadFailed(false);
            setRetryAttempt((a) => a + 1);
          }}
          style={{ ...smallButtonStyle, marginBottom: '10px' }}
        >
          Try Again
        </button>
        <p style={{ margin: '0 0 8px', fontSize: '10px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.4px', color: '#9a7200' }}>
          Fallback clinic list
        </p>
        {list}
      </div>
    );
  }

  return (
    <div data-testid="clinic-map">
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '8px' }}>
        <button type="button" onClick={handleUseMyLocation} style={smallButtonStyle}>
          Use my location
        </button>
      </div>
      <div
        ref={containerRef}
        style={{ height: '280px', borderRadius: '8px', border: '1px solid #d8d8d2', overflow: 'hidden', marginBottom: '10px' }}
      />
      {infoClinic && (
        <div style={{ marginBottom: '10px' }}>
          <ClinicCard
            clinic={infoClinic}
            distanceKm={distances[infoClinic.id]}
            selected={selectedId === infoClinic.id}
            onSelect={onSelect}
          />
        </div>
      )}
      <p style={{ margin: '0 0 8px', fontSize: '10px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.4px', color: '#9a7200' }}>
        Or pick from the list
      </p>
      {list}
    </div>
  );
}

export default ClinicMap;
