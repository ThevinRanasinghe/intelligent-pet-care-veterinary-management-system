"""Centralised environment configuration for the Agentic Service.

All environment-dependent values are resolved here so that no module
hardcodes deployment URLs, model identifiers, or secrets.
"""
import os

from dotenv import load_dotenv

load_dotenv()


class ConfigurationError(RuntimeError):
    """Raised when a required piece of service configuration is missing."""


# Default Gemini model identifier. Override with the GEMINI_MODEL
# environment variable if the project is provisioned for a different model.
# gemini-2.5-flash is retired for new API keys; 3.5-flash is the current
# fast/default tier.
DEFAULT_GEMINI_MODEL = "gemini-3.5-flash"


def get_api_base_url() -> str:
    """Base URL of the PetCare ASP.NET Core API (including /api)."""
    return os.getenv("API_BASE_URL", "http://localhost:5019/api").rstrip("/")


def get_backend_timeout_seconds() -> float:
    """Timeout applied to every Agentic Service -> backend HTTP call."""
    raw = os.getenv("BACKEND_TIMEOUT_SECONDS", "10")
    try:
        value = float(raw)
    except ValueError:
        return 10.0
    return value if value > 0 else 10.0


def get_gemini_api_key() -> str:
    """Gemini API key. Raises ConfigurationError when missing.

    The service intentionally fails instead of pretending an unconfigured
    key can produce real analysis.
    """
    key = os.getenv("GEMINI_API_KEY") or os.getenv("GOOGLE_API_KEY")
    if not key or not key.strip():
        raise ConfigurationError(
            "GEMINI_API_KEY (or GOOGLE_API_KEY) is not configured. "
            "Set it in the environment or agentic-service/.env."
        )
    return key.strip()


def get_gemini_model() -> str:
    """Gemini model identifier, configurable via GEMINI_MODEL."""
    return os.getenv("GEMINI_MODEL", DEFAULT_GEMINI_MODEL).strip() or DEFAULT_GEMINI_MODEL


def get_internal_api_key() -> str | None:
    """Shared secret expected in the X-Internal-Key request header.

    This authenticates service-to-service calls from the ASP.NET Core API.
    Returns None when unconfigured so callers can report misconfiguration.
    """
    key = os.getenv("AGENTIC_INTERNAL_KEY")
    return key.strip() if key and key.strip() else None


def get_service_port() -> int:
    raw = os.getenv("PORT", "8000")
    try:
        return int(raw)
    except ValueError:
        return 8000
