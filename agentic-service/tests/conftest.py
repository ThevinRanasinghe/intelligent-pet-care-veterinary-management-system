import os
import sys

import pytest

# Ensure the service root is importable (agent packages + shared module).
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Deterministic configuration for tests; real keys are never needed.
os.environ.setdefault("API_BASE_URL", "http://petcare.test/api")
os.environ.setdefault("AGENTIC_INTERNAL_KEY", "test-internal-key")
os.environ.setdefault("BACKEND_TIMEOUT_SECONDS", "2")


@pytest.fixture(autouse=True)
def _reset_llm_cache():
    """Each test starts with no cached LLM instance."""
    from shared import llm

    llm.reset_llm()
    yield
    llm.reset_llm()
