# Agentic Service Setup Guide

The `agentic-service` is a Python-based microservice that powers the AI agents (Consultation, Diagnosis, Inventory, and Scheduling) using FastAPI and LangGraph. It communicates with the C# backend and the frontend.

## Prerequisites

1. **Python 3.10+** must be installed on your system.
2. **Pip** (Python package installer).

## Setup Instructions

### 1. Navigate to the Directory
Open your terminal and navigate to the `agentic-service` folder:
```bash
cd agentic-service
```

### 2. Create a Virtual Environment
It is highly recommended to use a virtual environment to manage dependencies.
```bash
python -m venv .venv
```

Activate the virtual environment:
- **Windows (Command Prompt):** `.venv\Scripts\activate.bat`
- **Windows (PowerShell):** `.venv\Scripts\Activate.ps1`
- **Mac/Linux:** `source .venv/bin/activate`

### 3. Install Dependencies
Install the required packages using the provided `requirements.txt` file:
```bash
pip install -r requirements.txt
```
*(This will install FastAPI, Uvicorn, LangGraph, LangChain, Pydantic, and other required libraries).*

### 4. Configure Environment Variables
You need a Google Gemini API Key for the agents to function.
1. Create a file named `.env` in the `agentic-service` directory (if it doesn't already exist).
2. Add your API key to the file:
```env
GEMINI_API_KEY="your_actual_api_key_here"
```

### 5. Run the Service
Start the FastAPI server using Uvicorn. The service must run on **port 8000** because the C# backend and frontend are hardcoded to communicate with `http://localhost:8000`.

```bash
python main.py
```
*(Alternatively, you can run `uvicorn main:app --host 0.0.0.0 --port 8000 --reload` for auto-reloading during development).*

You should see output indicating that the server is running on `http://0.0.0.0:8000`.

## Architecture Note
- **C# Backend (`localhost:5019`)**: Provides deterministic data (database records, inventory counts) to the Python agents.
- **Python Agentic Service (`localhost:8000`)**: Runs LangGraph workflows. It accepts requests, fetches required context from the C# backend, analyzes it using the LLM, and returns structured JSON responses. 
- **Frontend (`localhost:5173`)**: Communicates with the C# backend (which proxies some AI requests) or directly with the Python service depending on the component.
