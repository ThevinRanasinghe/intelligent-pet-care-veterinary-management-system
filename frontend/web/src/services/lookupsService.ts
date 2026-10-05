import { apiRequest } from './api';

/** Active clinic shown in the owner booking flow. */
export interface OrganizationLookup {
  id: string;
  name: string;
  city: string | null;
  address: string | null;
  /** Clinic pin — null while the organization has no stored location. */
  latitude: number | null;
  longitude: number | null;
}

/** A nearby clinic with its straight-line distance from the user. */
export interface NearbyClinic {
  id: string;
  name: string;
  address: string;
  city: string;
  latitude: number;
  longitude: number;
  distanceKm: number;
}

/** One fixed one-hour slot of a working day ("09:00"–"10:00" …). */
export interface AvailabilitySlot {
  /** Slot start, "HH:mm". */
  start: string;
  /** Slot end — always start + 1 hour. */
  end: string;
  available: boolean;
  /** Active veterinarians free for the slot (empty when unavailable). */
  availableVeterinarianIds: string[];
}

export interface DayAvailability {
  /** "yyyy-MM-dd" */
  date: string;
  isPast: boolean;
  slots: AvailabilitySlot[];
}

export interface MonthAvailabilityDay {
  /** "yyyy-MM-dd" */
  date: string;
  available: boolean;
  fullyBooked: boolean;
  isPast: boolean;
}

export const lookupsService = {
  /** Active organizations an owner can book at. */
  getActiveOrganizations(): Promise<OrganizationLookup[]> {
    return apiRequest('/lookups/organizations');
  },

  /** Clinics within `radiusKm` of the user's location, nearest first. */
  getNearbyClinics(
    latitude: number,
    longitude: number,
    radiusKm = 50,
  ): Promise<NearbyClinic[]> {
    const params = new URLSearchParams({
      latitude: String(latitude),
      longitude: String(longitude),
      radiusKm: String(radiusKm),
    });
    return apiRequest(`/consultations/nearby-clinics?${params.toString()}`);
  },

  /** Slot availability for one day; pass veterinarianId for the manager
   *  assign flow (availability of that specific vet). */
  getAvailability(
    organizationId: string,
    date: string,
    veterinarianId?: string,
  ): Promise<DayAvailability> {
    const params = new URLSearchParams({ organizationId, date });
    if (veterinarianId) params.set('veterinarianId', veterinarianId);
    return apiRequest(`/consultations/availability?${params.toString()}`);
  },

  /** Month overview: one entry per calendar day of (year, month 1-12). */
  getMonthAvailability(
    organizationId: string,
    year: number,
    month: number,
  ): Promise<MonthAvailabilityDay[]> {
    const params = new URLSearchParams({
      organizationId,
      year: String(year),
      month: String(month),
    });
    return apiRequest(`/consultations/availability/month?${params.toString()}`);
  },
};
