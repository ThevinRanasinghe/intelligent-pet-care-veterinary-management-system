"""Allow-list tool registry with input validation and call tracing.

Every backend data-access tool used by the specialist agents is registered
here via the ``@registered_tool(agent, input_model)`` decorator. The
decorator:

- Adds the function name to ``TOOL_ALLOW_LIST`` under its owning agent.
- Validates the call arguments against a Pydantic model before executing
  (``auth_token`` is excluded — it is plumbing, not data).
- Enforces agent isolation inside a ``workflow_tool_context`` — a tool
  belonging to agent X raises ``ToolNotAllowedError`` when invoked while
  the active workflow context is agent Y. Outside any context (the
  existing direct-agent HTTP endpoints) there is no restriction.
- Appends a ``ToolCallRecord`` to the contextvar trace when one is active,
  so the supervisor can report exactly which tools each step used.
"""
import inspect
import logging
import time
from contextlib import contextmanager
from contextvars import ContextVar
from dataclasses import asdict, dataclass, field
from datetime import datetime
from functools import wraps
from typing import Annotated, Any, Dict, List, Optional, Type

from pydantic import AfterValidator, BaseModel, StringConstraints

logger = logging.getLogger("ToolRegistry")


class ToolNotAllowedError(PermissionError):
    """A registered tool was invoked under a workflow context belonging
    to a different agent."""


class ToolValidationError(ValueError):
    """Tool arguments failed the registered Pydantic input model."""


# --- Input field types ---------------------------------------------------

_IdStr = Annotated[
    str, StringConstraints(strip_whitespace=True, min_length=1, max_length=64)
]


def _check_date(value: str) -> str:
    datetime.strptime(value, "%Y-%m-%d")
    return value


def _check_time(value: str) -> str:
    datetime.strptime(value, "%H:%M")
    return value


IdStr = _IdStr
"""Non-empty identifier string, <= 64 chars."""

DateStr = Annotated[_IdStr, AfterValidator(_check_date)]
"""Strict YYYY-MM-DD date string."""

TimeStr = Annotated[_IdStr, AfterValidator(_check_time)]
"""Strict HH:MM time string."""


# --- Records & trace -----------------------------------------------------


@dataclass
class ToolCallRecord:
    tool: str
    agent: str
    ok: bool
    duration_ms: int
    error_kind: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


_agent_var: ContextVar[Optional[str]] = ContextVar("tool_agent", default=None)
_trace_var: ContextVar[Optional[List[ToolCallRecord]]] = ContextVar(
    "tool_trace", default=None
)


@contextmanager
def workflow_tool_context(agent: str):
    """Marks tool calls as belonging to ``agent`` and collects a trace.

    Yields the trace list (list[ToolCallRecord]) populated during the
    context. Nested calls overwrite the active trace for the duration.
    """
    token_agent = _agent_var.set(agent)
    token_trace = _trace_var.set([])
    try:
        yield _trace_var.get()
    finally:
        _trace_var.reset(token_trace)
        _agent_var.reset(token_agent)


def get_tool_trace() -> List[ToolCallRecord]:
    """Current trace list (empty outside a workflow_tool_context)."""
    return _trace_var.get() or []


# --- Registry ------------------------------------------------------------

# agent name -> set of registered tool function names
TOOL_ALLOW_LIST: Dict[str, set] = {}

_AUTH_PARAM = "auth_token"


def _error_kind(exc: BaseException) -> str:
    if isinstance(exc, ToolNotAllowedError):
        return "not_allowed"
    if isinstance(exc, ToolValidationError):
        return "invalid_input"
    kind = getattr(exc, "kind", None)
    return kind or type(exc).__name__


def registered_tool(agent: str, input_model: Optional[Type[BaseModel]] = None):
    """Decorator registering an async (or sync) tool under ``agent``.

    ``input_model`` is a Pydantic model whose fields match the tool's
    data parameters (everything except ``auth_token``).
    """

    def decorator(fn):
        TOOL_ALLOW_LIST.setdefault(agent, set()).add(fn.__name__)
        signature = inspect.signature(fn)

        def _guard(args, kwargs):
            bound = signature.bind(*args, **kwargs)
            bound.apply_defaults()
            data_args = {
                key: value
                for key, value in bound.arguments.items()
                if key != _AUTH_PARAM
            }
            active_agent = _agent_var.get()
            if active_agent is not None and active_agent != agent:
                raise ToolNotAllowedError(
                    f"Tool '{fn.__name__}' belongs to '{agent}' and cannot "
                    f"be used inside a '{active_agent}' workflow context."
                )
            if input_model is not None:
                try:
                    input_model(**data_args)
                except Exception as exc:
                    raise ToolValidationError(
                        f"Invalid input for tool '{fn.__name__}': {exc}"
                    ) from exc

        def _record(started: float, error: Optional[BaseException]) -> None:
            trace = _trace_var.get()
            if trace is not None:
                trace.append(
                    ToolCallRecord(
                        tool=fn.__name__,
                        agent=agent,
                        ok=error is None,
                        duration_ms=int((time.perf_counter() - started) * 1000),
                        error_kind=_error_kind(error) if error else None,
                    )
                )

        if inspect.iscoroutinefunction(fn):

            @wraps(fn)
            async def async_wrapper(*args, **kwargs):
                started = time.perf_counter()
                error: Optional[BaseException] = None
                try:
                    _guard(args, kwargs)
                    return await fn(*args, **kwargs)
                except Exception as exc:  # noqa: BLE001 — record then re-raise
                    error = exc
                    raise
                finally:
                    _record(started, error)

            return async_wrapper

        @wraps(fn)
        def sync_wrapper(*args, **kwargs):
            started = time.perf_counter()
            error: Optional[BaseException] = None
            try:
                _guard(args, kwargs)
                return fn(*args, **kwargs)
            except Exception as exc:  # noqa: BLE001 — record then re-raise
                error = exc
                raise
            finally:
                _record(started, error)

        return sync_wrapper

    return decorator
