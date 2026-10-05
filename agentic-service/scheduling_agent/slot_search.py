"""Deterministic appointment-window selection for the Scheduling Agent.

The LLM never picks slots. This module answers, in code:

    "Given the owner's preferred date/time and the required number of
    consecutive slots, what is the best valid window among real
    AppointmentSlot records?"

Search order (business rule — the preferred DATE outranks the preferred
TIME):

    1. Preferred date + exact preferred time.
    2. Preferred date + nearest valid window anywhere inside opening
       hours (before or after the preferred time).
    3. Nearest future date within SCHEDULING_MAX_FUTURE_DAYS, again
       searching the whole opening-hours window of each date.

Only real ``AppointmentSlot`` rows with status ``Available`` are
eligible. Slots are never invented; an empty appointment list does not
imply bookable time.

Opening hours mirror the backend's ``BookingRules`` (09:00-18:00, the
existing source of truth — there is no per-branch hours table) and are
overridable via environment so a deployment can tighten them without a
code change.
"""
import logging
import os
from dataclasses import dataclass, field
from datetime import date as date_type, datetime, time as time_type, timedelta
from typing import Any, Dict, List, Optional, Sequence

logger = logging.getLogger("SchedulingSlotSearch")


def _env_int(name: str, default: int) -> int:
    try:
        value = int(os.getenv(name, str(default)))
        return value if value > 0 else default
    except ValueError:
        return default


def scheduling_open_hour() -> int:
    """First slot hour. Mirrors BookingRules.OpeningTime (09:00)."""
    return _env_int("SCHEDULING_OPEN_HOUR", 9)


def scheduling_close_hour() -> int:
    """Last permitted end hour. Mirrors BookingRules.ClosingTime (18:00)."""
    return _env_int("SCHEDULING_CLOSE_HOUR", 18)


def scheduling_max_future_days() -> int:
    """How far past the preferred date the search may look."""
    return _env_int("SCHEDULING_MAX_FUTURE_DAYS", 30)


def scheduling_max_slots() -> int:
    """Upper bound on consecutive slots one appointment may occupy."""
    return _env_int("SCHEDULING_MAX_SLOTS", 4)


# ------------------------------------------------------------------ parsing


def _parse_date(raw: Any) -> Optional[date_type]:
    if raw is None:
        return None
    text = str(raw).strip()
    if not text:
        return None
    # ISO datetime ("2026-10-06T09:00:00...") or plain date.
    head = text[:10]
    try:
        return date_type.fromisoformat(head)
    except ValueError:
        pass
    # Fallback: US-style serialisation seen in the DB text dumps.
    for fmt in ("%m/%d/%Y", "%d/%m/%Y"):
        try:
            return datetime.strptime(head, fmt).date()
        except ValueError:
            continue
    return None


def _parse_time(raw: Any) -> Optional[time_type]:
    if raw is None:
        return None
    if isinstance(raw, time_type):
        return raw
    text = str(raw).strip()
    if not text:
        return None
    for fmt in ("%H:%M:%S", "%H:%M"):
        try:
            return datetime.strptime(text[:8] if fmt == "%H:%M:%S" else text[:5], fmt).time()
        except ValueError:
            continue
    # Handle embedded datetime ("2026-10-31T09:00:00").
    if "T" in text:
        try:
            return datetime.fromisoformat(text).time().replace(microsecond=0)
        except ValueError:
            return None
    return None


def _minutes(t: time_type) -> int:
    return t.hour * 60 + t.minute


def parse_preferred(raw: Any) -> tuple:
    """Split the consultation's preferredDate into (date, time).

    ``preferredDate`` is a DateTime on the backend: the owner picks a
    date and an hour-aligned preferred time, which the API stores as one
    timestamp. Returns (date|None, "HH:MM"|None).
    """
    if raw is None:
        return None, None
    text = str(raw).strip()
    if "T" in text:
        try:
            dt = datetime.fromisoformat(text)
            return dt.date(), dt.strftime("%H:%M")
        except ValueError:
            pass
    # Date-only input carries no preferred time.
    d = _parse_date(text)
    return d, None


# ------------------------------------------------------------------ slots


@dataclass(frozen=True)
class _Slot:
    id: str
    vet: str
    day: date_type
    start: time_type
    end: time_type
    branch: str


def _eligible_slots(
    slots: Sequence[Dict[str, Any]],
    open_min: int,
    close_min: int,
    today: date_type,
    vet_id: Optional[str],
    branch: Optional[str],
) -> List[_Slot]:
    out: List[_Slot] = []
    for raw in slots or []:
        if str(raw.get("status", "")).lower() != "available":
            continue
        day = _parse_date(raw.get("date"))
        start = _parse_time(raw.get("startTime"))
        end = _parse_time(raw.get("endTime"))
        sid = raw.get("id")
        vet = raw.get("veterinarianId")
        if not (day and start and end and sid and vet):
            continue
        if day < today:
            continue  # past slots are never bookable
        if vet_id and str(vet) != str(vet_id):
            continue
        if branch and str(raw.get("branch") or "").strip().lower() != branch.strip().lower():
            continue
        if not (_minutes(start) >= open_min and _minutes(end) <= close_min and end > start):
            continue  # outside opening hours / malformed window
        out.append(_Slot(str(sid), str(vet), day, start, end, str(raw.get("branch") or "")))
    return out


def _consecutive_windows(day_slots: List[_Slot], required: int) -> List[List[_Slot]]:
    """All sliding windows of ``required`` strictly consecutive slots.

    Input must be one vet/one date sorted by start. Consecutive means
    ``slot[i].end == slot[i+1].start`` — gaps disqualify the window.
    """
    windows: List[List[_Slot]] = []
    ordered = sorted(day_slots, key=lambda s: s.start)
    for i in range(0, len(ordered) - required + 1):
        group = ordered[i : i + required]
        if all(group[j].end == group[j + 1].start for j in range(required - 1)):
            windows.append(group)
    return windows


def _day_windows(
    slots: List[_Slot], day: date_type, required: int
) -> List[List[_Slot]]:
    """Consecutive windows on ``day`` — per veterinarian, then merged."""
    by_vet: Dict[str, List[_Slot]] = {}
    for s in slots:
        if s.day == day:
            by_vet.setdefault(s.vet, []).append(s)
    windows: List[List[_Slot]] = []
    for vet_slots in by_vet.values():
        windows.extend(_consecutive_windows(vet_slots, required))
    return sorted(windows, key=lambda w: w[0].start)


# ------------------------------------------------------------------ result


@dataclass
class SlotSelection:
    found: bool
    reason_code: str = "NO_VALID_SLOT"  # NO_VALID_SLOT | INVALID_DURATION | OK
    reason: str = ""
    date: Optional[str] = None
    start_time: Optional[str] = None
    end_time: Optional[str] = None
    veterinarian_id: Optional[str] = None
    branch: Optional[str] = None
    slot_ids: List[str] = field(default_factory=list)
    slot_count: int = 0
    required_slots: int = 0
    estimated_duration_minutes: int = 0
    requested_date: Optional[str] = None
    requested_time: Optional[str] = None
    used_preferred_time: bool = False
    used_preferred_date: bool = False
    fallback_used: bool = False
    fallback_type: str = "none"  # none | same_day_nearest_time | next_available_date
    searched_dates: List[str] = field(default_factory=list)
    candidate_windows: int = 0

    def to_dict(self) -> Dict[str, Any]:
        return {
            "found": self.found,
            "reasonCode": self.reason_code,
            "reason": self.reason,
            "date": self.date,
            "startTime": self.start_time,
            "endTime": self.end_time,
            "veterinarianId": self.veterinarian_id,
            "branch": self.branch,
            "slotIds": self.slot_ids,
            "slotCount": self.slot_count,
            "requiredSlots": self.required_slots,
            "estimatedDurationMinutes": self.estimated_duration_minutes,
            "requestedDate": self.requested_date,
            "requestedTime": self.requested_time,
            "usedPreferredTime": self.used_preferred_time,
            "usedPreferredDate": self.used_preferred_date,
            "fallbackUsed": self.fallback_used,
            "fallbackType": self.fallback_type,
            "searchedDates": self.searched_dates,
            "candidateWindows": self.candidate_windows,
        }


def resolve_required_slots(
    consultation_assessment: Optional[Dict[str, Any]],
) -> tuple:
    """Derive (required_slots, estimated_minutes, source) deterministically.

    The consultation agent's structured output may carry
    ``requiredSlots`` / ``estimatedDurationMinutes``. Anything missing,
    non-integer or out of range is rejected or defaults to one slot —
    unsupported free-form text never drives the count.
    """
    assessment = consultation_assessment or {}
    raw_slots = assessment.get("requiredSlots")
    raw_minutes = assessment.get("estimatedDurationMinutes")

    slots: Optional[int] = None
    if isinstance(raw_slots, bool):  # bool is an int subclass — exclude it
        raw_slots = None
    if isinstance(raw_slots, int):
        slots = raw_slots

    minutes: Optional[int] = None
    if isinstance(raw_minutes, bool):
        raw_minutes = None
    if isinstance(raw_minutes, int) and raw_minutes > 0:
        minutes = raw_minutes

    max_slots = scheduling_max_slots()

    if slots is not None:
        if slots < 1:
            return 0, 0, "invalid"
        if slots > max_slots:
            return 0, 0, "exceeds_max"
        return slots, slots * 60, "assessment"

    if minutes is not None:
        slots = -(-minutes // 60)  # ceil division
        if slots > max_slots:
            return 0, 0, "exceeds_max"
        return slots, minutes, "duration"

    return 1, 60, "default"


def find_appointment_window(
    *,
    slots: Sequence[Dict[str, Any]],
    preferred_date: Optional[str],
    preferred_time: Optional[str],
    required_slots: int,
    today: Optional[date_type] = None,
    veterinarian_id: Optional[str] = None,
    branch: Optional[str] = None,
    max_future_days: Optional[int] = None,
    open_hour: Optional[int] = None,
    close_hour: Optional[int] = None,
) -> SlotSelection:
    """Deterministic consecutive-window search. Pure — easy to unit test."""
    today = today or date_type.today()
    max_days = max_future_days if max_future_days is not None else scheduling_max_future_days()
    open_min = (open_hour if open_hour is not None else scheduling_open_hour()) * 60
    close_min = (close_hour if close_hour is not None else scheduling_close_hour()) * 60

    pref_day = _parse_date(preferred_date)
    pref_time = _parse_time(preferred_time)
    pref_min = _minutes(pref_time) if pref_time else None

    sel = SlotSelection(
        found=False,
        required_slots=required_slots,
        estimated_duration_minutes=required_slots * 60,
        requested_date=pref_day.isoformat() if pref_day else None,
        requested_time=pref_time.strftime("%H:%M") if pref_time else None,
    )

    if required_slots < 1 or required_slots > scheduling_max_slots():
        sel.reason_code = "INVALID_DURATION"
        sel.reason = (
            f"requiredSlots={required_slots} is outside the supported range "
            f"1..{scheduling_max_slots()}."
        )
        return sel

    if pref_day is None:
        # No preference at all — search from tomorrow's closest future day.
        pref_day = today + timedelta(days=1)

    eligible = _eligible_slots(slots, open_min, close_min, today, veterinarian_id, branch)
    relaxed = False
    if not eligible and branch:
        # Branch restriction eliminated everything — retry without it and
        # flag that the constraint was relaxed rather than silently
        # dropping the owner's request.
        eligible = _eligible_slots(slots, open_min, close_min, today, veterinarian_id, None)
        relaxed = True

    def emit(window: List[_Slot], used_pref_time: bool, used_pref_date: bool,
             fallback: str) -> SlotSelection:
        first, last = window[0], window[-1]
        reason = (
            "Requested time was available."
            if used_pref_time
            else "Preferred time was unavailable; the nearest valid "
                 "consecutive slots on the preferred date were selected."
            if used_pref_date
            else "No valid window on the preferred date; the nearest "
                 "future date with consecutive availability was selected."
        )
        if relaxed:
            reason += " Preferred branch had no availability; another branch's slots were used."
        return SlotSelection(
            found=True,
            reason_code="OK",
            reason=reason,
            date=first.day.isoformat(),
            start_time=first.start.strftime("%H:%M"),
            end_time=last.end.strftime("%H:%M"),
            veterinarian_id=first.vet,
            branch=first.branch or None,
            slot_ids=[s.id for s in window],
            slot_count=len(window),
            required_slots=required_slots,
            estimated_duration_minutes=required_slots * 60,
            requested_date=sel.requested_date,
            requested_time=sel.requested_time,
            used_preferred_time=used_pref_time,
            used_preferred_date=used_pref_date,
            fallback_used=not used_pref_time,
            fallback_type=fallback,
            searched_dates=list(sel.searched_dates),
        )

    def pick_nearest(windows: List[List[_Slot]]) -> Optional[List[_Slot]]:
        if not windows:
            return None
        if pref_min is None:
            return windows[0]
        return min(windows, key=lambda w: (abs(_minutes(w[0].start) - pref_min), _minutes(w[0].start)))

    # ---- Priority 1 + 2: the preferred DATE is exhausted first ---------
    sel.searched_dates.append(pref_day.isoformat())
    day_windows = _day_windows(eligible, pref_day, required_slots)
    sel.candidate_windows += len(day_windows)

    if day_windows:
        if pref_min is not None:
            exact = [w for w in day_windows if _minutes(w[0].start) == pref_min]
            if exact:
                return emit(exact[0], True, True, "none")
        chosen = pick_nearest(day_windows)
        if chosen:
            return emit(chosen, False, True, "same_day_nearest_time")

    # ---- Priority 3: nearest permitted future date ---------------------
    limit_day = pref_day + timedelta(days=max_days)
    future_days = sorted({s.day for s in eligible if pref_day < s.day <= limit_day})
    for day in future_days:
        sel.searched_dates.append(day.isoformat())
        day_windows = _day_windows(eligible, day, required_slots)
        sel.candidate_windows += len(day_windows)
        if day_windows:
            chosen = pick_nearest(day_windows)
            if chosen:
                return emit(chosen, False, False, "next_available_date")

    sel.reason = (
        "No suitable consecutive available slots were found within the "
        f"permitted search range ({max_days} days)."
    )
    return sel
