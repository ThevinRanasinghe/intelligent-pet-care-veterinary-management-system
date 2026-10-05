"""Deterministic supervisor-side validation of scheduling proposals.

The scheduling agent already validates its own output; the supervisor
re-checks the pieces that gate a high-impact action:

- ``validate_proposal`` runs before the approval gate: required fields,
  strict date/time formats, multi-slot consistency, and a fresh backend
  conflict check over the whole proposed window via the registered
  ``check_conflict`` tool. A missing recommendedAppointment (the
  specialist's safe fallback) yields no Proposal — the workflow fails
  with reason ``no_valid_slot`` and the manager handles it manually.
- ``final_validation`` runs after approval: re-checks the conflict AND
  re-fetches the slot inventory so a slot claimed between proposal and
  approval fails with ``slot_no_longer_available`` and NO approvedAction
  is emitted.
"""
import logging
from datetime import datetime
from typing import Any, Dict, List, Optional, Tuple

from shared.backend import BackendApiError
from shared.tool_registry import workflow_tool_context

from scheduling_agent.tools import check_conflict, fetch_available_slots

from .models import Proposal, ProposalAppointment

logger = logging.getLogger("SupervisorValidation")

_DATE_FMT = "%Y-%m-%d"
_TIME_FMT = "%H:%M"


def _bad_formats(app: Dict[str, Any]) -> Optional[str]:
    try:
        datetime.strptime(str(app.get("date", "")), _DATE_FMT)
        datetime.strptime(str(app.get("startTime", "")), _TIME_FMT)
        datetime.strptime(str(app.get("endTime", "")), _TIME_FMT)
    except (ValueError, TypeError):
        return "invalid_time_format"
    for field_name in ("veterinarianId", "appointmentSlotId"):
        value = app.get(field_name)
        if not isinstance(value, str) or not value.strip() or len(value) > 64:
            return f"invalid_{field_name}"

    # Multi-slot consistency: slotIds must cover the whole window.
    slot_ids = app.get("slotIds")
    if slot_ids is not None:
        if not isinstance(slot_ids, list) or not slot_ids:
            return "invalid_slotIds"
        for sid in slot_ids:
            if not isinstance(sid, str) or not sid.strip() or len(sid) > 64:
                return "invalid_slotIds"
        slot_count = app.get("slotCount")
        if not isinstance(slot_count, int) or slot_count != len(slot_ids):
            return "invalid_slotCount"
        if slot_ids[0] != app.get("appointmentSlotId"):
            return "slot_order_mismatch"
    return None


async def _conflict_check(app: Dict[str, Any], auth_token: Optional[str]) -> bool:
    with workflow_tool_context("scheduling_agent"):
        return await check_conflict(
            app["veterinarianId"],
            app["date"],
            app["startTime"],
            app["endTime"],
            auth_token,
        )


async def validate_proposal(
    assessment: Optional[Dict[str, Any]],
    auth_token: Optional[str],
) -> Tuple[Optional[Proposal], Optional[str]]:
    """Builds a Proposal from a scheduling assessment.

    Returns (proposal, failure_reason): proposal is None when the
    assessment carried no recommendedAppointment; failure_reason is set
    when deterministic validation rejected the recommendation.
    """
    if not assessment:
        return None, "no_assessment"

    rec = assessment.get("recommendedAppointment")
    if not rec:
        return None, None  # safe fallback — NoProposal, handled by caller

    fmt_error = _bad_formats(rec)
    if fmt_error:
        return None, fmt_error

    try:
        conflict = await _conflict_check(rec, auth_token)
    except BackendApiError as exc:
        logger.warning("proposal conflict check failed (%s)", exc.kind)
        return None, f"conflict_check_failed:{exc.kind}"
    if conflict:
        return None, "conflict_detected"

    diagnostics = assessment.get("searchDiagnostics") or {}
    proposal = Proposal(
        appointment=ProposalAppointment(
            veterinarianId=rec["veterinarianId"],
            date=rec["date"],
            startTime=rec["startTime"],
            endTime=rec["endTime"],
            appointmentSlotId=rec["appointmentSlotId"],
            slotIds=list(rec.get("slotIds") or [rec["appointmentSlotId"]]),
            slotCount=int(rec.get("slotCount") or 1),
            requiredSlots=int(rec.get("requiredSlots") or 1),
            estimatedDurationMinutes=int(rec.get("estimatedDurationMinutes") or 60),
            usedPreferredTime=bool(rec.get("usedPreferredTime", True)),
            usedPreferredDate=bool(rec.get("usedPreferredDate", True)),
            fallbackType=rec.get("fallbackType", "none"),
            requestedDate=diagnostics.get("requestedDate"),
            requestedTime=diagnostics.get("requestedTime"),
        ),
        quotation=assessment.get("quotationProposal"),
        validationSummary=dict(assessment.get("validationSummary") or {}),
        confidence=assessment.get("confidence", "Low"),
        planningNotes=assessment.get("planningNotes", ""),
        disclaimer=assessment.get("disclaimer", ""),
    )
    return proposal, None


async def _slots_still_available(
    slot_ids: List[str], veterinarian_id: str, date: str, auth_token: Optional[str]
) -> bool:
    """Re-fetches availability: every proposed slot must still be Available."""
    with workflow_tool_context("scheduling_agent"):
        slots = await fetch_available_slots(
            auth_token, veterinarian_id=veterinarian_id, date=date
        )
    available_ids = {str(s.get("id")) for s in (slots or [])}
    return all(sid in available_ids for sid in slot_ids)


async def final_validation(
    proposal: Dict[str, Any],
    auth_token: Optional[str],
) -> Optional[str]:
    """Re-checks the approved window. Returns a failure reason or None."""
    appointment = (proposal or {}).get("appointment") or {}
    if not appointment or not appointment.get("veterinarianId") or not appointment.get("date"):
        return "missing_proposal"

    # The slot set must still be Available — availability may have
    # changed between proposal and approval.
    slot_ids = appointment.get("slotIds") or [appointment.get("appointmentSlotId")]
    try:
        if not await _slots_still_available(
            slot_ids, appointment["veterinarianId"], appointment["date"], auth_token
        ):
            return "slot_no_longer_available"
        conflict = await _conflict_check(appointment, auth_token)
    except BackendApiError as exc:
        logger.warning("final validation backend check failed (%s)", exc.kind)
        return f"conflict_check_failed:{exc.kind}"
    if conflict:
        return "slot_no_longer_available"
    return None
