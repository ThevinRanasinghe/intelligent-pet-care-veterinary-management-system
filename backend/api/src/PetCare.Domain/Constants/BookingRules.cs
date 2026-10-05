namespace PetCare.Domain.Constants;

/// <summary>
/// Fixed booking rules for consultation appointments. Operating hours are
/// 09:00–18:00, divided into exactly nine one-hour slots (09:00–10:00 …
/// 17:00–18:00). The client picks a date + one-hour slot; the end time is
/// always start + 1h and is never client-entered. The backend is the final
/// authority on these rules.
/// </summary>
public static class BookingRules
{
    /// <summary>First appointment slot starts at 09:00.</summary>
    public static readonly TimeOnly OpeningTime = new(9, 0);

    /// <summary>Clinic closes at 18:00 — the last slot ends here.</summary>
    public static readonly TimeOnly ClosingTime = new(18, 0);

    /// <summary>Every appointment slot is exactly one hour.</summary>
    public const int SlotDurationMinutes = 60;

    /// <summary>Latest allowed slot start (17:00, so the slot ends at 18:00).</summary>
    public static readonly TimeOnly LastSlotStart =
        ClosingTime.AddMinutes(-SlotDurationMinutes);

    /// <summary>The nine fixed one-hour slots of a working day.</summary>
    public static readonly IReadOnlyList<(TimeOnly Start, TimeOnly End)> DaySlots =
        Enumerable.Range(OpeningTime.Hour, ClosingTime.Hour - OpeningTime.Hour)
            .Select(h => (new TimeOnly(h, 0), new TimeOnly(h + 1, 0)))
            .ToList();

    /// <summary>Slot starts must be exactly on the hour (minute-aligned).</summary>
    public static bool IsHourAligned(TimeOnly time) =>
        time.Minute == 0 && time.Second == 0 && time.Millisecond == 0;

    /// <summary>
    /// A slot start is valid when it falls inside operating hours such that
    /// start + SlotDurationMinutes does not run past <see cref="ClosingTime"/>.
    /// </summary>
    public static bool IsWithinOperatingHours(TimeOnly start) =>
        start >= OpeningTime && start.AddMinutes(SlotDurationMinutes) <= ClosingTime;

    /// <summary>
    /// The (date, start) pair must not be in the past: the date may be today
    /// only while the slot's start is still ahead of the current time.
    /// </summary>
    public static bool IsInFuture(DateOnly date, TimeOnly start)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (date < today)
        {
            return false;
        }
        return date > today || start > TimeOnly.FromDateTime(DateTime.Now);
    }

    /// <summary>Shared message for a fully-booked slot (HTTP 409).</summary>
    public const string SlotUnavailableMessage =
        "This appointment slot is no longer available. Please select another time.";
}
