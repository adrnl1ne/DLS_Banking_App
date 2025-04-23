FROM python:3.9-slim

WORKDIR /app

COPY requirements.txt .

RUN pip install --no-cache-dir -r requirements.txt
RUN pip install watchdog uvicorn

COPY . .

EXPOSE 8000 8001

# Make sure the entrypoint script is executable if you add one
RUN chmod +x *.py || echo "No scripts to make executable"

CMD ["python", "main.py"]