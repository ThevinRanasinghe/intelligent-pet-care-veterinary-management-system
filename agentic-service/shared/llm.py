"""Shared Gemini LLM access for all agents.

The model is created lazily so that importing an agent never requires
configuration; the first actual analysis fails clearly if GEMINI_API_KEY
is missing (ConfigurationError) rather than silently using a dummy key.
"""
import logging
import re
from typing import Optional

from langchain_google_genai import ChatGoogleGenerativeAI

from .config import get_gemini_api_key, get_gemini_model

logger = logging.getLogger("LLM")

_llm: Optional[ChatGoogleGenerativeAI] = None


def get_llm() -> ChatGoogleGenerativeAI:
    """Returns the shared Gemini chat model, building it on first use."""
    global _llm
    if _llm is None:
        # Raises ConfigurationError with a clear message when unset.
        api_key = get_gemini_api_key()
        _llm = ChatGoogleGenerativeAI(
            model=get_gemini_model(),
            temperature=0.0,
            api_key=api_key,
            # Keep a couple of in-client retries for transient 5xx/network
            # blips, but don't burn ~35s of exponential backoff on quota
            # errors that cannot recover inside a single request window.
            max_retries=2,
        )
    return _llm


def reset_llm() -> None:
    """Test hook: clears the cached model instance."""
    global _llm
    _llm = None


def content_to_text(content) -> str:
    """Normalises a LangChain message content value to plain text."""
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        parts = []
        for block in content:
            if isinstance(block, dict) and block.get("type") == "text":
                parts.append(block.get("text", ""))
            elif isinstance(block, str):
                parts.append(block)
        return "".join(parts)
    return str(content)


_JSON_OBJECT_PATTERN = re.compile(r"\{.*\}", re.DOTALL)

# Errors that can never succeed on an immediate in-graph retry: a
# rate-limited/quota-exhausted key stays limited for the whole request
# window, and a missing key fails on every call. Retrying these just
# burns the shared Gemini quota and delays the safe fallback.
_NON_RETRYABLE_MARKERS = (
    "ratelimit",
    "rate_limit",
    "resourceexhausted",
    "quota",
    "429",
    "not_configured",
)


def is_non_retryable_error(error: Optional[str]) -> bool:
    """True for errors where an immediate retry cannot help."""
    if not error:
        return False
    lowered = error.lower()
    return any(marker in lowered for marker in _NON_RETRYABLE_MARKERS)


def extract_json_object(text: str) -> Optional[str]:
    """Extracts the first JSON object substring from model output."""
    match = _JSON_OBJECT_PATTERN.search(text or "")
    return match.group(0) if match else None
