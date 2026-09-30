import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('../../lib/googleMaps', () => ({
  loadGoogleMaps: vi.fn(),
  MapsUnavailableError: class MapsUnavailableError extends Error {},
  directionsUrl: (lat: number, lng: number) =>
    `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`,
}));

import { loadGoogleMaps } from '../../lib/googleMaps';
import { RegisterPage } from '../../features/auth/RegisterPage';
import { firePlaceSelect, makeFakeGoogle } from '../fakeGoogle';

function jsonResponse(body: unknown, ok = true, status = 201) {
  return { ok, status, text: async () => (body === undefined ? '' : JSON.stringify(body)) } as Response;
}

function renderRegister() {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <RegisterPage />
    </MemoryRouter>,
  );
}

async function switchToOrganizationTab() {
  await userEvent.click(screen.getByRole('tab', { name: /veterinary organization/i }));
}

describe('RegisterPage — clinic location', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async () => jsonResponse({ id: 'user-1' })));
    vi.mocked(loadGoogleMaps).mockReset();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('shows a "Select Location on Map" option on the organization step', async () => {
    renderRegister();
    await switchToOrganizationTab();

    expect(screen.getByRole('button', { name: /select location on map/i })).toBeInTheDocument();
  });

  it('never renders manual coordinate inputs or the env-var name', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    renderRegister();
    await switchToOrganizationTab();

    // Before opening the picker.
    expect(screen.queryByLabelText(/^latitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^longitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /use coordinates/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/VITE_GOOGLE_MAPS_API_KEY/)).toBeNull();

    // While the picker is open with maps loaded.
    await userEvent.click(screen.getByRole('button', { name: /select location on map/i }));
    await screen.findByTestId('location-picker-map');
    expect(screen.queryByLabelText(/^latitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^longitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /use coordinates/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/VITE_GOOGLE_MAPS_API_KEY/)).toBeNull();
  });

  it('confirms a pin and shows "Exact clinic location selected" without coordinates', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    renderRegister();
    await switchToOrganizationTab();

    await userEvent.click(screen.getByRole('button', { name: /select location on map/i }));
    await screen.findByTestId('location-picker-map');

    // Click on the map places the pin; Confirm stores it on the form.
    act(() => {
      fake.__maps[0].fire('click', { latLng: { lat: () => 6.9, lng: () => 79.9 } });
    });
    await userEvent.click(screen.getByRole('button', { name: /confirm location/i }));

    expect(await screen.findByText(/exact clinic location selected/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /change location/i })).toBeInTheDocument();
    expect(screen.queryByText(/Latitude:/)).toBeNull();
    expect(screen.queryByText(/Longitude:/)).toBeNull();
  });

  it('reopens the picker via "Change Location"', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    renderRegister();
    await switchToOrganizationTab();

    await userEvent.click(screen.getByRole('button', { name: /select location on map/i }));
    await screen.findByTestId('location-picker-map');
    act(() => {
      fake.__maps[0].fire('click', { latLng: { lat: () => 6.9, lng: () => 79.9 } });
    });
    await userEvent.click(screen.getByRole('button', { name: /confirm location/i }));
    await screen.findByText(/exact clinic location selected/i);

    await userEvent.click(screen.getByRole('button', { name: /change location/i }));
    expect(await screen.findByTestId('location-picker-map')).toBeInTheDocument();
  });

  it('recenters the map and places the pin from a PlaceAutocompleteElement selection', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    renderRegister();
    await switchToOrganizationTab();

    await userEvent.click(screen.getByRole('button', { name: /select location on map/i }));
    await screen.findByTestId('location-picker-map');

    // The search host mounts a PlaceAutocompleteElement (not legacy Autocomplete).
    const host = screen.getByTestId('place-autocomplete-host');
    const searchEl = host.querySelector('[data-testid="place-autocomplete-element"]');
    expect(searchEl).not.toBeNull();

    // Choosing a suggestion moves the map and drops the pin.
    await act(async () => {
      firePlaceSelect(searchEl!, { lat: 6.95, lng: 80.01 });
    });
    expect(fake.__maps[0].centers.length).toBeGreaterThan(0);
    expect(fake.__markers[0]?.position).toEqual({ lat: 6.95, lng: 80.01 });

    await userEvent.click(screen.getByRole('button', { name: /confirm location/i }));
    expect(await screen.findByText(/exact clinic location selected/i)).toBeInTheDocument();
  });

  it('includes latitude and longitude in the registration POST body', async () => {
    const fetchMock = vi.mocked(fetch);
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps).mockResolvedValue(fake as never);
    const user = userEvent.setup();
    renderRegister();
    await switchToOrganizationTab();

    await user.type(screen.getByLabelText(/organization \/ clinic name/i), 'Map Clinic');
    await user.type(screen.getByLabelText(/clinic phone number/i), '+94 11 555 0100');
    await user.type(screen.getByLabelText(/organization contact email/i), 'map@clinic.lk');
    await user.type(screen.getByLabelText(/street address/i), '5 Galle Road');

    // Pick a pin and confirm.
    await user.click(screen.getByRole('button', { name: /select location on map/i }));
    await screen.findByTestId('location-picker-map');
    act(() => {
      fake.__maps[0].fire('click', { latLng: { lat: () => 6.9, lng: () => 79.9 } });
    });
    await user.click(screen.getByRole('button', { name: /confirm location/i }));

    await user.type(screen.getByLabelText(/^city/i), 'Colombo');
    await user.type(screen.getByLabelText(/^country/i), 'Sri Lanka');
    await user.click(screen.getByRole('button', { name: /continue to manager account/i }));

    await user.type(screen.getByLabelText(/manager first name/i), 'Amal');
    await user.type(screen.getByLabelText(/manager last name/i), 'Perera');
    await user.type(screen.getByLabelText(/manager login email/i), 'amal@clinic.lk');
    await user.type(screen.getByPlaceholderText('Min 8 characters (mixed case, digit, symbol)'), 'SecurePass1!');
    await user.type(screen.getByPlaceholderText('Repeat password'), 'SecurePass1!');
    await user.click(screen.getByRole('button', { name: /complete registration/i }));

    await screen.findByText(/awaiting Beacon administrator verification/i);

    const registerCall = fetchMock.mock.calls.find(([url]) =>
      String(url).endsWith('/auth/register/organization'),
    );
    expect(registerCall).toBeDefined();
    const body = JSON.parse(String(registerCall![1]?.body));
    expect(body.latitude).toBe(6.9);
    expect(body.longitude).toBe(79.9);
  });

  it('registers without a location — body carries null coordinates', async () => {
    const fetchMock = vi.mocked(fetch);
    vi.mocked(loadGoogleMaps).mockResolvedValue(makeFakeGoogle() as never);
    const user = userEvent.setup();
    renderRegister();
    await switchToOrganizationTab();

    await user.type(screen.getByLabelText(/organization \/ clinic name/i), 'No Map Clinic');
    await user.type(screen.getByLabelText(/clinic phone number/i), '+94 11 555 0200');
    await user.type(screen.getByLabelText(/organization contact email/i), 'nomap@clinic.lk');
    await user.type(screen.getByLabelText(/street address/i), '9 Kandy Road');
    await user.type(screen.getByLabelText(/^city/i), 'Kandy');
    await user.type(screen.getByLabelText(/^country/i), 'Sri Lanka');
    await user.click(screen.getByRole('button', { name: /continue to manager account/i }));

    await user.type(screen.getByLabelText(/manager first name/i), 'Nimal');
    await user.type(screen.getByLabelText(/manager last name/i), 'Silva');
    await user.type(screen.getByLabelText(/manager login email/i), 'nimal@clinic.lk');
    await user.type(screen.getByPlaceholderText('Min 8 characters (mixed case, digit, symbol)'), 'SecurePass1!');
    await user.type(screen.getByPlaceholderText('Repeat password'), 'SecurePass1!');
    await user.click(screen.getByRole('button', { name: /complete registration/i }));

    await screen.findByText(/awaiting Beacon administrator verification/i);

    const registerCall = fetchMock.mock.calls.find(([url]) =>
      String(url).endsWith('/auth/register/organization'),
    );
    expect(registerCall).toBeDefined();
    const body = JSON.parse(String(registerCall![1]?.body));
    expect(body.latitude).toBeNull();
    expect(body.longitude).toBeNull();
  });

  it('shows a retryable notice when maps fail to load, never naming the env var', async () => {
    const fake = makeFakeGoogle();
    vi.mocked(loadGoogleMaps)
      .mockRejectedValueOnce(new Error('load failed'))
      .mockResolvedValue(fake as never);
    renderRegister();
    await switchToOrganizationTab();

    await userEvent.click(screen.getByRole('button', { name: /select location on map/i }));
    const fallback = await screen.findByTestId('map-fallback');
    expect(fallback).toHaveTextContent(/map location is temporarily unavailable/i);
    expect(screen.queryByText(/VITE_GOOGLE_MAPS_API_KEY/)).toBeNull();
    expect(screen.queryByLabelText(/^latitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/^longitude$/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /use coordinates/i })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(vi.mocked(loadGoogleMaps)).toHaveBeenCalledTimes(2);
    expect(await screen.findByTestId('location-picker-map')).toBeInTheDocument();
  });
});
