"""Unit tests for the deterministic appointment-window search.

Covers the required scheduling rules: exact preferred time first, the
preferred DATE exhausted before any other date, nearest same-day
windows in either direction, consecutive-only multi-slot windows, the
bounded future-date fallback, and defensive filtering (status, past,
veterinarian, opening hours).
"""
from datetime import date

import pytest

from scheduling_agent.slot_search import (
    find_appointment_window,
    parse_preferred,
    resolve_required_slots,
)

TODAY = date(2026, 10, 1)
DAY = "2026-10-10"
NEXT = "2026-10-11"


def slot(sid, day=DAY, start="09:00", vet="vet-1", branch="Main",
         status="Available", end=None):
    """One-hour slot fixture; end defaults to start + 1h."""
    hour, minute = (int(part) for part in start.split(":"))
    end = end or f"{hour + 1:02d}:{minute:02d}"
    return {
        "id": sid,
        "veterinarianId": vet,
        "date": day,
        "startTime": start,
        "endTime": end,
        "branch": branch,
        "status": status,
    }


def find(slots, **kwargs):
    kwargs.setdefault("today", TODAY)
    kwargs.setdefault("required_slots", 1)
    kwargs.setdefault("preferred_date", DAY)
    return find_appointment_window(slots=slots, **kwargs)


# ---- Priority 1: exact preferred time --------------------------------


def test_exact_preferred_time_selected():
    sel = find([slot("s1", start="10:00")], preferred_time="10:00")
    assert sel.found
    assert sel.date == DAY and sel.start_time == "10:00"
    assert sel.used_preferred_time and sel.used_preferred_date
    assert sel.fallback_type == "none" and not sel.fallback_used
    assert sel.slot_ids == ["s1"]


# ---- Priority 2: same-day nearest window -----------------------------


def test_same_day_fallback_stays_on_preferred_date():
    """Example A: 10:00 blocked for a 2-slot window; 12:00 works."""
    slots = [
        slot("a", start="10:00"),
        slot("b", start="11:00", status="Booked"),
        slot("c", start="12:00"),
        slot("d", start="13:00"),
    ]
    sel = find(slots, preferred_time="10:00", required_slots=2)
    assert sel.found
    assert sel.date == DAY and sel.start_time == "12:00" and sel.end_time == "14:00"
    assert sel.slot_ids == ["c", "d"]
    assert not sel.used_preferred_time and sel.used_preferred_date
    assert sel.fallback_type == "same_day_nearest_time"


def test_earlier_same_day_window_selected():
    """Example 2: pref 16:00 can't fit 2 slots; 15:00-17:00 works."""
    slots = [
        slot("a", start="15:00"),
        slot("b", start="16:00"),
        slot("c", start="17:00", status="Booked"),
    ]
    sel = find(slots, preferred_time="16:00", required_slots=2)
    assert sel.found
    assert sel.start_time == "15:00" and sel.end_time == "17:00"
    assert sel.fallback_type == "same_day_nearest_time"


def test_later_same_day_window_selected():
    slots = [
        slot("a", start="09:00", status="Completed"),
        slot("b", start="11:00"),
    ]
    sel = find(slots, preferred_time="10:00")
    assert sel.found and sel.start_time == "11:00"


def test_single_slot_nearest_same_day():
    """Example 4: requiredSlots=1, 10:00 unavailable, 11:00 proposed."""
    sel = find(
        [slot("a", start="10:00", status="Booked"), slot("b", start="11:00")],
        preferred_time="10:00",
    )
    assert sel.found and sel.start_time == "11:00" and sel.date == DAY


def test_nearest_prefers_min_distance():
    """09:00 and 12:00 are both free; 10:30 preference picks the closer."""
    slots = [slot("a", start="09:00"), slot("b", start="12:00")]
    sel = find(slots, preferred_time="11:00")
    assert sel.found and sel.start_time == "12:00"


# ---- Multi-slot consecutiveness --------------------------------------


def test_three_consecutive_slots():
    slots = [
        slot("a", start="09:00"),
        slot("b", start="10:00"),
        slot("c", start="11:00"),
        slot("d", start="12:00", status="Booked"),
    ]
    sel = find(slots, preferred_time="10:00", required_slots=3)
    assert sel.found
    assert sel.slot_ids == ["a", "b", "c"]
    assert sel.start_time == "09:00" and sel.end_time == "12:00"


def test_non_consecutive_slots_never_combine():
    """Slots at 10:00 and 12:00 are individually Available but not
    consecutive — a 2-slot window must not bridge the gap."""
    slots = [
        slot("a", start="10:00"),
        slot("b", start="11:00", status="Booked"),
        slot("c", start="12:00"),
    ]
    sel = find(slots, preferred_time="10:00", required_slots=2)
    assert not sel.found
    assert sel.reason_code == "NO_VALID_SLOT"


def test_slots_from_different_vets_never_combine():
    """Consecutive times across different vets are not a window."""
    slots = [
        slot("a", start="10:00", vet="vet-1"),
        slot("b", start="11:00", vet="vet-2"),
    ]
    sel = find(slots, preferred_time="10:00", required_slots=2)
    assert not sel.found


# ---- Priority 3: future-date fallback --------------------------------


def test_next_day_searched_only_after_preferred_day_exhausted():
    """Example 3: no 2-slot window anywhere on the preferred day."""
    slots = [
        slot("a", start="09:00"),
        slot("b", start="10:00", status="Booked"),
        slot("c", start="11:00", status="Booked"),
        slot("d", start="12:00"),
        slot("e", start="13:00", status="Booked"),
        slot("f", start="14:00"),
        slot("g", start="15:00", status="Booked"),
        slot("h", start="16:00"),
        slot("i", start="17:00", status="Booked"),
        # Next day: valid window exists.
        slot("j", day=NEXT, start="09:00"),
        slot("k", day=NEXT, start="10:00"),
    ]
    sel = find(slots, preferred_time="10:00", required_slots=2)
    assert sel.found
    assert sel.date == NEXT and sel.start_time == "09:00"
    assert not sel.used_preferred_date
    assert sel.fallback_type == "next_available_date"
    assert DAY in sel.searched_dates and NEXT in sel.searched_dates


def test_future_day_searches_entire_opening_window():
    """The fallback day is searched across all opening hours, not just
    around the preferred time."""
    slots = [
        slot("a", day=NEXT, start="16:00"),
        slot("b", day=NEXT, start="17:00"),
    ]
    sel = find(slots, preferred_time="09:00", required_slots=2)
    assert sel.found and sel.date == NEXT and sel.start_time == "16:00"


def test_search_range_limited_by_max_future_days():
    slots = [slot("a", day="2026-10-20", start="09:00")]
    sel = find(slots, preferred_time="10:00", max_future_days=5)
    assert not sel.found
    assert sel.reason_code == "NO_VALID_SLOT"
    assert "2026-10-20" not in sel.searched_dates


def test_nearest_future_date_wins():
    """Two future dates have windows; the nearer date is chosen."""
    slots = [
        slot("a", day="2026-10-12", start="09:00"),
        slot("b", day="2026-10-15", start="09:00"),
    ]
    sel = find(slots, preferred_time="10:00")
    assert sel.found and sel.date == "2026-10-12"


def test_no_slots_anywhere_safe_failure():
    sel = find([], preferred_time="10:00")
    assert not sel.found
    assert sel.reason_code == "NO_VALID_SLOT"
    assert sel.reason


# ---- Filtering --------------------------------------------------------


@pytest.mark.parametrize("status", ["Completed", "Booked", "Cancelled", "Reserved"])
def test_non_available_statuses_ignored(status):
    sel = find([slot("a", start="10:00", status=status)], preferred_time="10:00")
    assert not sel.found


def test_past_slots_ignored():
    sel = find(
        [slot("a", day="2026-09-01", start="10:00")],
        preferred_date="2026-09-01",
        preferred_time="10:00",
    )
    assert not sel.found


def test_requested_veterinarian_restriction_respected():
    slots = [
        slot("a", start="10:00", vet="vet-2"),
        slot("b", start="11:00", vet="vet-1"),
    ]
    sel = find(slots, preferred_time="10:00", veterinarian_id="vet-1")
    assert sel.found and sel.start_time == "11:00" and sel.veterinarian_id == "vet-1"


def test_slots_outside_opening_hours_ignored():
    slots = [
        slot("a", start="07:00", end="08:00"),
        slot("b", start="17:00", end="19:00"),
    ]
    sel = find(slots, preferred_time="10:00", open_hour=9, close_hour=18)
    assert not sel.found


def test_branch_filter_relaxed_when_it_eliminates_everything():
    """A preferred branch with zero usable slots falls back to other
    branches rather than failing — recorded in the reason."""
    slots = [slot("a", start="10:00", branch="North")]
    sel = find(slots, preferred_time="10:00", branch="Main")
    assert sel.found and sel.branch == "North"
    assert "branch" in sel.reason.lower()


def test_branch_filter_applied_when_alternatives_exist():
    """Preferred-branch slots win over other branches when both exist."""
    slots = [
        slot("a", start="10:00", branch="North"),
        slot("b", start="11:00", branch="Main"),
    ]
    sel = find(slots, preferred_time="11:00", branch="Main")
    assert sel.found and sel.branch == "Main" and sel.start_time == "11:00"


# ---- requiredSlots resolution ----------------------------------------


def test_resolve_required_slots_from_assessment():
    assert resolve_required_slots({"requiredSlots": 2}) == (2, 120, "assessment")


def test_resolve_required_slots_from_duration():
    assert resolve_required_slots({"estimatedDurationMinutes": 90}) == (2, 90, "duration")


def test_resolve_required_slots_default():
    assert resolve_required_slots(None) == (1, 60, "default")
    assert resolve_required_slots({}) == (1, 60, "default")


@pytest.mark.parametrize(
    "bad,expected_source",
    [
        (0, "invalid"),
        (-1, "invalid"),
        (99, "exceeds_max"),
        ("two", "default"),
        (True, "default"),
        (2.5, "default"),
    ],
)
def test_resolve_required_slots_invalid(bad, expected_source):
    _, _, source = resolve_required_slots({"requiredSlots": bad})
    assert source == expected_source


def test_find_rejects_out_of_range_required_slots():
    sel = find([slot("a")], preferred_time="10:00", required_slots=99)
    assert not sel.found
    assert sel.reason_code == "INVALID_DURATION"


# ---- Preferred input parsing ------------------------------------------


def test_parse_preferred_iso_datetime():
    day, time_str = parse_preferred("2026-10-31T09:00:00")
    assert day == date(2026, 10, 31) and time_str == "09:00"


def test_parse_preferred_date_only():
    day, time_str = parse_preferred("2026-10-31")
    assert day == date(2026, 10, 31) and time_str is None


def test_parse_preferred_none():
    assert parse_preferred(None) == (None, None)
