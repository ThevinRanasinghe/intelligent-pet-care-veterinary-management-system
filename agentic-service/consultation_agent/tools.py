import httpx
import logging
import os

logger = logging.getLogger(__name__)
# Assuming the backend is running on port 5019 based on the previous fixes
API_BASE = os.getenv("API_BASE_URL", "http://localhost:5019/api")

async def fetch_consultation_details(consultation_id: str, auth_token: str = None) -> dict:
    """Reads the consultation request and pet details from the ASP.NET Core backend."""
    logger.info(f"Retrieving consultation details for: {consultation_id} from {API_BASE}")
    headers = {"Authorization": auth_token} if auth_token else {}
    async with httpx.AsyncClient() as client:
        try:
            response = await client.get(f"{API_BASE}/consultations/{consultation_id}", headers=headers)
            response.raise_for_status()
            data = response.json()
            logger.info(f"Successfully retrieved consultation details: {data.get('id')}")
            return data
        except httpx.HTTPError as e:
            logger.error(f"Error fetching consultation details: {e}")
            # If the backend is unreachable or returning 404, we will raise
            raise

async def fetch_previous_history(pet_id: str, auth_token: str = None) -> list:
    """Reads the pet's previous consultation history."""
    logger.info(f"Retrieving history for pet: {pet_id}")
    headers = {"Authorization": auth_token} if auth_token else {}
    async with httpx.AsyncClient() as client:
        try:
            # Adjust the route based on the actual backend implementation if needed
            response = await client.get(f"{API_BASE}/examinations/pet/{pet_id}", headers=headers)
            if response.status_code == 404:
                return []
            response.raise_for_status()
            return response.json()
        except httpx.HTTPError as e:
            logger.warning(f"Failed to fetch previous history, or route doesn't exist: {e}")
            return []
