/**
 * Tiny fake of the google.maps API surface used by the map components.
 * Instances record their `addListener` callbacks so tests can trigger
 * clicks/drags via `fire(name, arg)`.
 */
export function makeFakeGoogle() {
  const maps: FakeMap[] = [];
  const markers: FakeMarker[] = [];
  const placeElements: HTMLElement[] = [];

  class FakeMap {
    listeners: Record<string, ((arg?: unknown) => void)[]> = {};
    centers: unknown[] = [];
    fitBoundsCalls: unknown[] = [];

    constructor(
      public el: unknown,
      public opts: unknown,
    ) {
      maps.push(this);
    }

    addListener(name: string, cb: (arg?: unknown) => void) {
      (this.listeners[name] ??= []).push(cb);
    }

    setCenter(center: unknown) {
      this.centers.push(center);
    }

    fitBounds(bounds: unknown) {
      this.fitBoundsCalls.push(bounds);
    }

    fire(name: string, arg?: unknown) {
      (this.listeners[name] ?? []).forEach((cb) => cb(arg));
    }
  }

  class FakeMarker {
    listeners: Record<string, ((arg?: unknown) => void)[]> = {};
    position: { lat: number; lng: number };

    constructor(
      public opts: { map: unknown; position: { lat: number; lng: number }; title?: string },
    ) {
      markers.push(this);
      this.position = opts.position;
    }

    addListener(name: string, cb: (arg?: unknown) => void) {
      (this.listeners[name] ??= []).push(cb);
    }

    setPosition(position: { lat: number; lng: number }) {
      this.position = position;
    }

    getPosition() {
      const p = this.position;
      return { lat: () => p.lat, lng: () => p.lng };
    }

    fire(name: string, arg?: unknown) {
      (this.listeners[name] ?? []).forEach((cb) => cb(arg));
    }
  }

  class FakeInfoWindow {
    constructor(public opts: unknown) {}
    open() {}
  }

  class FakeLatLngBounds {
    points: unknown[] = [];
    extend(point: unknown) {
      this.points.push(point);
    }
  }

  /**
   * Places API (New) — google.maps.places.PlaceAutocompleteElement is a DOM
   * element. Tests dispatch `gmp-select` with a fake placePrediction.
   * `new FakePlaceAutocompleteElement()` returns the element itself.
   */
  const FakePlaceAutocompleteElement = function () {
    const el = document.createElement('div');
    el.setAttribute('data-testid', 'place-autocomplete-element');
    placeElements.push(el);
    return el;
  } as unknown as new () => HTMLElement;

  return {
    maps: {
      Map: FakeMap,
      Marker: FakeMarker,
      InfoWindow: FakeInfoWindow,
      LatLngBounds: FakeLatLngBounds,
      event: {
        addListener(target: FakeMap | FakeMarker, name: string, cb: (arg?: unknown) => void) {
          target.addListener(name, cb);
        },
      },
      places: { PlaceAutocompleteElement: FakePlaceAutocompleteElement },
      importLibrary: async () => ({
        PlaceAutocompleteElement: FakePlaceAutocompleteElement,
      }),
    },
    __maps: maps,
    __markers: markers,
    __placeElements: placeElements,
  };
}

/** Fire a `gmp-select` event on a fake PlaceAutocompleteElement. */
export function firePlaceSelect(el: Element, location: { lat: number; lng: number }) {
  const event = new Event('gmp-select');
  (event as any).placePrediction = {
    toPlace: () => ({
      fetchFields: async () => undefined,
      location: { lat: () => location.lat, lng: () => location.lng },
    }),
  };
  el.dispatchEvent(event);
}
