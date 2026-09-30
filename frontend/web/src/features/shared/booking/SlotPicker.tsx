import type { AvailabilitySlot } from "../../../services/lookupsService";
import { slotLabel } from "./bookingSlots";

interface SlotPickerProps {
  /** The nine slots from the day-availability endpoint (null = no day yet). */
  slots: AvailabilitySlot[] | null;
  /** Currently selected slot start, "HH:mm". */
  selectedStart: string | null;
  onSelect: (start: string) => void;
  loading?: boolean;
  /**
   * Extra per-slot gate — used by the manager assign flow to only enable
   * slots where the chosen veterinarian is in availableVeterinarianIds.
   * `slot.available` is always applied; this is AND-ed on top.
   */
  isEnabled?: (slot: AvailabilitySlot) => boolean;
  /** Text shown when no date is selected yet. */
  emptyHint?: string;
}

/**
 * Renders the nine fixed one-hour slots for a day. Unavailable slots are
 * disabled; slot times are shown in 12-hour format.
 */
export function SlotPicker({
  slots,
  selectedStart,
  onSelect,
  loading,
  isEnabled,
  emptyHint = "Select a date to see the available time slots.",
}: SlotPickerProps) {
  if (loading) {
    return <div className="booking-slots-hint">Loading time slots…</div>;
  }

  if (!slots) {
    return <div className="booking-slots-hint">{emptyHint}</div>;
  }

  return (
    <div className="booking-slots" role="group" aria-label="Time slots">
      {slots.map((slot) => {
        const enabled = slot.available && (isEnabled ? isEnabled(slot) : true);
        const selected = selectedStart === slot.start;
        return (
          <button
            key={slot.start}
            type="button"
            className={`booking-slot${selected ? " booking-slot-selected" : ""}`}
            aria-label={slotLabel(slot.start, slot.end)}
            disabled={!enabled}
            onClick={() => onSelect(slot.start)}
            title={!slot.available ? "Booked" : undefined}
          >
            {slotLabel(slot.start, slot.end)}
          </button>
        );
      })}
    </div>
  );
}
