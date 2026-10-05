/**
 * Shared fixed-slot booking helpers. The backend enforces the same rules:
 * nine one-hour slots per working day, 09:00–17:00 starts, closing 18:00.
 */

/** Hour-aligned slot starts ("HH:mm"), 09:00 through 17:00. */
export const SLOT_STARTS = [
  "09:00",
  "10:00",
  "11:00",
  "12:00",
  "13:00",
  "14:00",
  "15:00",
  "16:00",
  "17:00",
] as const;

/** "09:00" → "09:00 AM", "17:00" → "05:00 PM". */
export function formatClock12(hhmm: string): string {
  const [hoursString, minutesString] = hhmm.split(":");
  const hours = Number(hoursString);
  const minutes = Number(minutesString ?? "0");
  if (Number.isNaN(hours)) return hhmm;
  const suffix = hours >= 12 ? "PM" : "AM";
  const hour12 = hours % 12 || 12;
  return `${String(hour12).padStart(2, "0")}:${String(minutes).padStart(2, "0")} ${suffix}`;
}

/** Slot label shown to the user, e.g. "09:00 AM – 10:00 AM". */
export function slotLabel(start: string, end: string): string {
  return `${formatClock12(start)} – ${formatClock12(end)}`;
}

/** Local-date → "yyyy-MM-dd" (no timezone drift). */
export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function todayIso(): string {
  return toIsoDate(new Date());
}
