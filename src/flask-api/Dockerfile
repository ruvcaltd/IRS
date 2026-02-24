FROM python:3.12.1-slim

# Install ODBC Driver 18 for SQL Server with security flags
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl gnupg2 apt-transport-https ca-certificates wget \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && curl -fsSL https://packages.microsoft.com/config/debian/12/prod.list | tee /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 unixodbc-dev \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Add non-root user for security
RUN groupadd -r flask && useradd -r -g flask flask

COPY . .

# Set permissions
RUN chown -R flask:flask /app

# Run as non-root user
USER flask

EXPOSE 5001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=30s \
    CMD python -c "import requests; requests.get('http://localhost:5001/health')" || exit 1

CMD ["gunicorn", "--config", "gunicorn.conf.py", "yahoo_app:app"]
