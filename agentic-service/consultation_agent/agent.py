import logging
import os
import json
from langgraph.graph import StateGraph, END
from langchain_core.messages import HumanMessage, SystemMessage
from langchain_google_genai import ChatGoogleGenerativeAI
from pydantic import ValidationError

from .models import AgentState, ConsultationAssessment, get_safe_fallback
from .tools import fetch_consultation_details, fetch_previous_history

logger = logging.getLogger("ConsultationAgent")
logging.basicConfig(level=logging.INFO)

from dotenv import load_dotenv
load_dotenv()

api_key = os.getenv("GEMINI_API_KEY") or os.getenv("GOOGLE_API_KEY")
if not api_key:
    # Provide a dummy key temporarily if it's not set just so the app doesn't crash on boot,
    # though actual analysis will fail until they set it correctly.
    api_key = "dummy_key_to_prevent_crash_on_boot"

# Make sure GEMINI_API_KEY environment variable is set
# Using Gemini 3.8 Flash via langchain-google-genai
llm = ChatGoogleGenerativeAI(
    model="gemini-3.8-flash", 
    temperature=0.0,
    api_key=api_key
)

async def retrieve_context_node(state: AgentState) -> AgentState:
    """Gathers required read-only data from the backend."""
    req_id = state["consultation_request_id"]
    logger.info(f"[{req_id}] Node: retrieve_context")
    
    try:
        # 1. Read existing consultation
        auth_token = state.get("auth_token")
        consultation = await fetch_consultation_details(req_id, auth_token)
        
        # 2. Read previous history
        pet_id = consultation.get("petId")
        history = await fetch_previous_history(pet_id, auth_token) if pet_id else []
        
        state["raw_input_data"] = {
            "consultation": consultation,
            "history": history
        }
    except Exception as e:
        logger.error(f"[{req_id}] Failed to retrieve context: {e}")
        state["error"] = str(e)
        
    return state

def analyze_request_node(state: AgentState) -> AgentState:
    """Prompts the LLM to generate the assessment."""
    req_id = state["consultation_request_id"]
    logger.info(f"[{req_id}] Node: analyze_request (Retry: {state['retry_count']})")
    
    sys_prompt = """You are a Consultation Request Analysis Agent. 
    Your ONLY job is to analyze the provided consultation request, pet details, and history.
    Provide a preliminary assessment JSON. 
    DO NOT provide final diagnoses, prescribe treatments, or recommend billing/scheduling operations.
    
    IMPORTANT: You must return ONLY a raw JSON object. Do not include markdown code blocks like ```json. Do not include any conversational text before or after the JSON.
    
    Output EXACTLY this JSON structure:
    {
        "consultationRequestId": "string",
        "priority": "Low" | "Moderate" | "High" | "Emergency",
        "consultationType": "Routine" | "Urgent" | "Emergency",
        "keyConcerns": [{"concern": "string", "reason": "string"}],
        "recommendedChecks": ["string"],
        "suggestedNextStep": "string",
        "disclaimer": "Preliminary AI consultation assessment — requires veterinary review and confirmation."
    }"""
    
    if state["error"] and state["retry_count"] > 0:
        sys_prompt += f"\n\nPREVIOUS ERROR (Fix this in your JSON output): {state['error']}"

    human_msg = f"Consultation Data to Analyze: {json.dumps(state['raw_input_data'])}"
    
    try:
        response = llm.invoke([
            SystemMessage(content=sys_prompt),
            HumanMessage(content=human_msg)
        ])
        
        content = response.content
        
        # If the response is a list of blocks, extract the text
        if isinstance(content, list):
            text_parts = []
            for block in content:
                if isinstance(block, dict) and block.get("type") == "text":
                    text_parts.append(block.get("text", ""))
                elif isinstance(block, str):
                    text_parts.append(block)
            content = "".join(text_parts)
            
        logger.info(f"[{req_id}] Raw LLM Output: {content}")
        
        # Robustly extract JSON using regex in case the model outputs extra conversational text
        import re
        json_match = re.search(r'\{.*\}', content, re.DOTALL)
        if json_match:
            content = json_match.group(0)
            
        state["llm_response"] = content
        state["error"] = None
    except Exception as e:
        logger.error(f"[{req_id}] LLM Call failed: {e}")
        state["error"] = "LLM invocation error: " + str(e)
        
    return state

def validate_response_node(state: AgentState) -> AgentState:
    """Validates the LLM JSON output against the Pydantic schema."""
    req_id = state["consultation_request_id"]
    logger.info(f"[{req_id}] Node: validate_response")
    
    if state["error"]:
        return state
        
    try:
        raw_json = json.loads(state["llm_response"])
        raw_json["consultationRequestId"] = req_id # Enforce ID correctness
        
        # This implicitly runs all our Pydantic validators (types, enums, disclaimer)
        parsed = ConsultationAssessment(**raw_json)
        state["assessment"] = parsed
        logger.info(f"[{req_id}] Validation Passed.")
    except (json.JSONDecodeError, ValidationError) as e:
        logger.warning(f"[{req_id}] Validation Failed: {e}")
        state["error"] = str(e)
        state["retry_count"] += 1
        
    return state

def fallback_node(state: AgentState) -> AgentState:
    """Provides a safe fallback if validation repeatedly fails."""
    req_id = state["consultation_request_id"]
    logger.error(f"[{req_id}] Node: fallback_node - Max retries exceeded.")
    state["assessment"] = get_safe_fallback(req_id)
    return state

# Routing logic
def route_after_validation(state: AgentState) -> str:
    if state.get("assessment") is not None:
        return "end" # Success
    if state["retry_count"] < 2:
        return "retry" # Back to LLM
    return "fallback" # Max retries hit, go to fallback

def route_after_context(state: AgentState) -> str:
    if state.get("error"):
        return "fallback" # If API is down, just fallback immediately
    return "analyze"

# Build LangGraph
graph_builder = StateGraph(AgentState)
graph_builder.add_node("retrieve_context", retrieve_context_node)
graph_builder.add_node("analyze_request", analyze_request_node)
graph_builder.add_node("validate_response", validate_response_node)
graph_builder.add_node("fallback", fallback_node)

graph_builder.set_entry_point("retrieve_context")
graph_builder.add_conditional_edges("retrieve_context", route_after_context, {"analyze": "analyze_request", "fallback": "fallback"})
graph_builder.add_edge("analyze_request", "validate_response")
graph_builder.add_conditional_edges("validate_response", route_after_validation, {
    "end": END,
    "retry": "analyze_request",
    "fallback": "fallback"
})
graph_builder.add_edge("fallback", END)

consultation_agent_app = graph_builder.compile()
