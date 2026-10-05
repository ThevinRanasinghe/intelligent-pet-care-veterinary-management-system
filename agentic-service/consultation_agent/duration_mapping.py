"""Deterministic mapping from the consultation assessment to a bounded
slot requirement.

The LLM assesses clinical complexity; this module — pure code — decides
how many consecutive one-hour appointment slots that complexity can
justify. The LLM's ``requiredSlots`` / ``estimatedDurationMinutes``
fields are treated as untrusted suggestions and are always normalized:

- the system uses fixed 60-minute slots (``BookingRules``), so
  ``estimatedDurationMinutes`` is always ``requiredSlots * 60``;
- ``requiredSlots`` is clamped to ``1..SCHEDULING_MAX_SLOTS`` AND to a
  per-complexity ceiling, so "simple" symptoms can never book half a
  day no matter what the model emitted;
- a missing ``complexity`` is treated conservatively (the "moderate"
  ceiling of 2) — extended visits require the model to actually assert
  complexity;
- low-confidence output cannot inflate the booking: below the
  confidence floor the count falls back to the complexity default.

Owner-entered text is data, not instructions: prompt-injection asking
for more slots lands here as an out-of-range number and is clamped.
"""
import logging
import os
from dataclasses import dataclass, field
from typing import Any, List, Optional

logger = logging.getLogger("ConsultationDurationMapping")

SLOT_MINUTES = 60  # mirrors backend BookingRules.SlotDurationMinutes


def _env_int(name: str, default: int) -> int:
    try:
        value = int(os.getenv(name, str(default)))
        return value if value > 0 else default
    except ValueError:
        return default


def scheduling_max_slots() -> int:
    """Upper bound on consecutive slots one appointment may occupy."""
    return _env_int("SCHEDULING_MAX_SLOTS", 4)


# Per-complexity ceilings — the LLM chooses the tier, code enforces the
# bound. "simple" can never take more than one slot.
_COMPLEXITY_CAP = {"simple": 1, "moderate": 2, "complex": 4}
# Conservative defaults when the LLM omits requiredSlots entirely.
_COMPLEXITY_DEFAULT = {"simple": 1, "moderate": 1, "complex": 2}
# Missing/unknown complexity is capped like "moderate": at most a
# doubled visit, never 3–4 slots without an explicit assessment.
_UNKNOWN_CAP = _COMPLEXITY_CAP["moderate"]

# Below this confidence the suggestion cannot escalate beyond the
# complexity default — uncertainty prefers the smaller booking.
_CONFIDENCE_FLOOR = 0.5


@dataclass
class SlotNeed:
    required_slots: int
    estimated_minutes: int
    complexity: str  # normalized: simple | moderate | complex
    notes: List[str] = field(default_factory=list)


def _as_int(raw: Any) -> Optional[int]:
    if isinstance(raw, bool):  # bool is an int subclass — exclude it
        return None
    if isinstance(raw, int):
        return raw
    if isinstance(raw, float) and raw.is_integer():
        return int(raw)
    return None


def _as_confidence(raw: Any) -> Optional[float]:
    if isinstance(raw, bool):
        return None
    if isinstance(raw, (int, float)):
        return float(raw)
    return None


def normalize_scheduling_need(
    complexity: Any,
    required_slots: Any,
    estimated_minutes: Any,
    confidence: Any = None,
) -> SlotNeed:
    """Map LLM scheduling hints to a bounded slot count.

    Returns a ``SlotNeed`` whose ``required_slots`` always satisfies
    ``1 <= n <= scheduling_max_slots()`` and whose ``estimated_minutes``
    is always ``required_slots * SLOT_MINUTES``. ``notes`` explains every
    adjustment for the audit trail.
    """
    tier = str(complexity).strip().lower() if isinstance(complexity, str) else ""
    notes: List[str] = []
    if tier in _COMPLEXITY_CAP:
        cap = _COMPLEXITY_CAP[tier]
        default = _COMPLEXITY_DEFAULT[tier]
    else:
        # Missing or unrecognized complexity: moderate ceiling, single-slot
        # default — an extended visit always requires an explicit assessment.
        if tier:
            notes.append(
                f"unrecognized complexity '{complexity}'; capped at the 'moderate' ceiling"
            )
        tier = "moderate"
        cap = _UNKNOWN_CAP
        default = 1

    hard_cap = min(cap, scheduling_max_slots())

    slots = _as_int(required_slots)
    minutes = _as_int(estimated_minutes)

    if slots is not None and slots < 1:
        notes.append(f"requiredSlots={required_slots} is not positive; ignored")
        slots = None

    if slots is None and minutes is not None:
        if minutes > 0:
            slots = -(-minutes // SLOT_MINUTES)  # ceil
            if minutes % SLOT_MINUTES:
                notes.append(
                    f"estimatedDurationMinutes={minutes} is not slot-aligned; "
                    f"rounded up to {slots} slot(s)"
                )
        else:
            notes.append(
                f"estimatedDurationMinutes={minutes} is not positive; ignored"
            )

    if slots is None:
        slots = default
        notes.append(f"no usable slot estimate; defaulted to {default} for '{tier}' complexity")

    if slots > hard_cap:
        notes.append(
            f"requiredSlots={slots} exceeds the {hard_cap}-slot ceiling for "
            f"'{tier}' complexity; clamped"
        )
        slots = hard_cap

    conf = _as_confidence(confidence)
    if conf is not None and conf < _CONFIDENCE_FLOOR and slots > default:
        notes.append(
            f"confidence={conf:.2f} below {_CONFIDENCE_FLOOR}; "
            f"reduced to {default} slot(s)"
        )
        slots = default

    return SlotNeed(
        required_slots=slots,
        estimated_minutes=slots * SLOT_MINUTES,
        complexity=tier,
        notes=notes,
    )
