import { ChevronLeft, ChevronRight } from "lucide-react";
import type { MonthAvailabilityDay } from "../../../services/lookupsService";

/** Year + 1-based month currently shown by the calendar. */
export interface VisibleMonth {
  year: number;
  month: number;
}

export function currentMonth(): VisibleMonth {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

export function shiftMonth(month: VisibleMonth, delta: number): VisibleMonth {
  const date = new Date(month.year, month.month - 1 + delta, 1);
  return { year: date.getFullYear(), month: date.getMonth() + 1 };
}

const WEEKDAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
const MONTH_NAMES = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

interface BookingCalendarProps {
  /** Month currently displayed (drives the grid; parent fetches `days`). */
  visibleMonth: VisibleMonth;
  /** Per-day availability for the visible month, or null while loading. */
  days: MonthAvailabilityDay[] | null;
  loading?: boolean;
  /** Currently selected day, "yyyy-MM-dd". */
  selectedDate: string | null;
  onSelectDate: (iso: string) => void;
  onMonthChange: (month: VisibleMonth) => void;
}

/**
 * Monday→Sunday month grid fed by the month-availability endpoint: past
 * days are greyed out, fully-booked days are disabled and labelled, and
 * available days are selectable.
 */
export function BookingCalendar({
  visibleMonth,
  days,
  loading,
  selectedDate,
  onSelectDate,
  onMonthChange,
}: BookingCalendarProps) {
  const { year, month } = visibleMonth;
  const daysInMonth = new Date(year, month, 0).getDate();

  // Monday-first offset: Date.getDay() is Sunday-first.
  const firstWeekday = (new Date(year, month - 1, 1).getDay() + 6) % 7;

  const byIso = new Map<string, MonthAvailabilityDay>();
  (days ?? []).forEach((day) => byIso.set(day.date, day));

  const iso = (day: number) =>
    `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;

  const cells: (number | null)[] = [
    ...Array.from({ length: firstWeekday }, () => null),
    ...Array.from({ length: daysInMonth }, (_, index) => index + 1),
  ];
  while (cells.length % 7 !== 0) cells.push(null);

  return (
    <div className="booking-calendar" data-testid="booking-calendar">
      <div className="booking-calendar-header">
        <button
          type="button"
          className="booking-month-nav"
          aria-label="Previous month"
          onClick={() => onMonthChange(shiftMonth(visibleMonth, -1))}
        >
          <ChevronLeft size={16} />
        </button>

        <div className="booking-calendar-title">
          {MONTH_NAMES[month - 1]} {year}
        </div>

        <button
          type="button"
          className="booking-month-nav"
          aria-label="Next month"
          onClick={() => onMonthChange(shiftMonth(visibleMonth, +1))}
        >
          <ChevronRight size={16} />
        </button>
      </div>

      <div className="booking-calendar-weekdays">
        {WEEKDAY_LABELS.map((label) => (
          <span key={label} className="booking-weekday">
            {label}
          </span>
        ))}
      </div>

      {loading || days === null ? (
        <div className="booking-calendar-loading">Loading calendar…</div>
      ) : (
        <div className="booking-calendar-grid">
          {cells.map((day, index) => {
            if (day === null) {
              return <span key={`empty-${index}`} className="booking-day booking-day-empty" />;
            }

            const dateIso = iso(day);
            const entry = byIso.get(dateIso);
            const isPast = entry?.isPast ?? false;
            const fullyBooked = entry?.fullyBooked ?? false;
            const disabled = isPast || fullyBooked || !entry?.available;
            const selected = selectedDate === dateIso;

            const classNames = [
              "booking-day",
              isPast ? "booking-day-past" : "",
              fullyBooked ? "booking-day-full" : "",
              selected ? "booking-day-selected" : "",
            ]
              .filter(Boolean)
              .join(" ");

            return (
              <button
                key={dateIso}
                type="button"
                className={classNames}
                aria-label={dateIso}
                disabled={disabled}
                onClick={() => onSelectDate(dateIso)}
                title={
                  isPast
                    ? "This date has passed"
                    : fullyBooked
                      ? "Fully booked"
                      : undefined
                }
              >
                <span className="booking-day-number">{day}</span>
                {fullyBooked && (
                  <span className="booking-day-tag">Fully booked</span>
                )}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
