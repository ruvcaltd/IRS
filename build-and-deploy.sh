#!/bin/bash
set -e

echo "========================================="
echo "Building and Deploying Docker Workflows"
echo "========================================="
echo ""

# Check if Docker is running
echo "Checking Docker status..."
if ! docker ps &>/dev/null; then
    echo "ERROR: Docker is not running!"
    echo "Please start Docker and try again."
    exit 1
fi
echo "Docker is running!"

# Check if .env file exists
if [ ! -f ".env" ]; then
    echo "ERROR: .env file not found!"
    echo "Please copy .env.template to .env and fill in the values."
    exit 1
fi

echo ""
echo "Stopping any existing containers..."
docker compose down

echo ""
echo "Building Docker images..."
docker compose build --no-cache

echo ""
echo "Starting containers..."
docker compose up -d

echo ""
echo "========================================="
echo "Deployment Complete!"
echo "========================================="
echo ""
echo "Services are starting up. Access them at:"
echo "  - Angular UI:     http://localhost:4203"
echo "  - .NET API:       http://localhost:5000"
echo "  - .NET Swagger:   http://localhost:5000/swagger"
echo "  - Flask API:      http://localhost:5001"
echo "  - Flask Swagger:  http://localhost:5001/swagger"
echo ""
echo "To view logs, run: docker compose logs -f"
echo "To stop services, run: docker compose down"
