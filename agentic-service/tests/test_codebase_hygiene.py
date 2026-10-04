"""Hygiene checks for Phase-1 cleanup guarantees."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def test_no_nested_duplicate_scheduling_agent():
    assert not (ROOT / "inventory_agent" / "scheduling_agent").exists()


def test_no_hardcoded_localhost_backend_in_agents():
    """localhost:5019 may appear only as the dev default in shared/config.py."""
    offenders = []
    for py in ROOT.rglob("*.py"):
        if ".venv" in py.parts or "tests" in py.parts:
            continue
        if "localhost:5019" in py.read_text(encoding="utf-8") and py.name != "config.py":
            offenders.append(str(py.relative_to(ROOT)))
    assert offenders == []


def test_no_aiohttp_dependency():
    """All HTTP calls standardise on httpx — aiohttp must be gone."""
    offenders = []
    for py in ROOT.rglob("*.py"):
        if ".venv" in py.parts or "tests" in py.parts:
            continue
        if re.search(r"\bimport aiohttp\b|\bfrom aiohttp\b", py.read_text(encoding="utf-8")):
            offenders.append(str(py.relative_to(ROOT)))
    assert offenders == []


def test_no_dummy_api_key_fallback():
    """B9: the silent dummy-key boot workaround must not return."""
    offenders = []
    for py in ROOT.rglob("*.py"):
        if ".venv" in py.parts or "tests" in py.parts:
            continue
        if "dummy_key" in py.read_text(encoding="utf-8"):
            offenders.append(str(py.relative_to(ROOT)))
    assert offenders == []


def test_no_raw_llm_output_logging():
    """B12: raw LLM payloads are never logged."""
    offenders = []
    for py in ROOT.rglob("*.py"):
        if ".venv" in py.parts or "tests" in py.parts:
            continue
        if "Raw LLM Output" in py.read_text(encoding="utf-8"):
            offenders.append(str(py.relative_to(ROOT)))
    assert offenders == []


def test_requirements_match_imports():
    reqs = (ROOT / "requirements.txt").read_text(encoding="utf-8")
    for dep in ("fastapi", "uvicorn", "pydantic", "langchain", "langgraph",
                "langchain-google-genai", "httpx", "python-dotenv"):
        assert dep in reqs
    assert "aiohttp" not in reqs
