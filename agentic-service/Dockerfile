# PetCare Agentic Service — container build for cloud deployment.
# Build context must be the REPOSITORY ROOT:
#   docker build -f agentic-service/Dockerfile .

FROM python:3.12-slim
WORKDIR /app

COPY agentic-service/requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt

COPY agentic-service/ .

ENV PYTHONUNBUFFERED=1

# main.py runs uvicorn on 0.0.0.0:$PORT (default 8000); Render injects PORT.
CMD ["python", "main.py"]
