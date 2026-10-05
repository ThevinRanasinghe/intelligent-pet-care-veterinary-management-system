"""Configuration behaviour: env-driven values, clear failure on missing key."""
import pytest

from shared import config
from shared.config import ConfigurationError


def test_api_base_url_default_localhost_dev():
    # A localhost default is intentional for local dev; it lives in exactly
    # one place (shared.config) instead of being scattered across tools.
    assert config.get_api_base_url().endswith("/api")


def test_api_base_url_from_environment(monkeypatch):
    monkeypatch.setenv("API_BASE_URL", "https://api.example.com/api/")
    assert config.get_api_base_url() == "https://api.example.com/api"


def test_missing_gemini_key_fails_clearly(monkeypatch):
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.delenv("GOOGLE_API_KEY", raising=False)
    with pytest.raises(ConfigurationError, match="GEMINI_API_KEY"):
        config.get_gemini_api_key()


def test_dummy_key_behaviour_removed(monkeypatch):
    # B9: no silent dummy key — get_llm must raise, not construct a client
    # that fails cryptically later.
    monkeypatch.delenv("GEMINI_API_KEY", raising=False)
    monkeypatch.delenv("GOOGLE_API_KEY", raising=False)
    from shared import llm

    with pytest.raises(ConfigurationError):
        llm.get_llm()


def test_gemini_model_configurable(monkeypatch):
    monkeypatch.setenv("GEMINI_MODEL", "gemini-test-model")
    assert config.get_gemini_model() == "gemini-test-model"


def test_gemini_model_default():
    assert config.get_gemini_model() == config.DEFAULT_GEMINI_MODEL


def test_internal_key(monkeypatch):
    monkeypatch.delenv("AGENTIC_INTERNAL_KEY", raising=False)
    assert config.get_internal_api_key() is None
    monkeypatch.setenv("AGENTIC_INTERNAL_KEY", "secret-123")
    assert config.get_internal_api_key() == "secret-123"


def test_backend_timeout_configurable(monkeypatch):
    monkeypatch.setenv("BACKEND_TIMEOUT_SECONDS", "5")
    assert config.get_backend_timeout_seconds() == 5.0
    monkeypatch.setenv("BACKEND_TIMEOUT_SECONDS", "not-a-number")
    assert config.get_backend_timeout_seconds() == 10.0
