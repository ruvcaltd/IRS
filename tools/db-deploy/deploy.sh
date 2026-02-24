#!/bin/bash
set -e

# Wait for SQL Server to be ready
until /opt/mssql-tools18/bin/sqlcmd -S "$TARGET_SERVER" -U sa -P "$SA_PASSWORD" -C -Q "SELECT 1" &>/dev/null; do
    echo "SQL Server not ready yet, retrying in 5s..."
    sleep 5
done

echo "SQL Server is ready. Publishing dacpac..."
DACPAC_FILE=$(ls /deploy/*.dacpac 2>/dev/null | head -n 1)
if [ -z "$DACPAC_FILE" ]; then
    echo "*** Could not find any .dacpac file in /deploy"
    exit 1
fi

/opt/sqlpackage/sqlpackage \
        /Action:Publish \
        /SourceFile:"$DACPAC_FILE" \
    /TargetConnectionString:"Server=$TARGET_SERVER,1433;Database=$DB_NAME;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True;Encrypt=False" \
    /p:BlockOnPossibleDataLoss=True

# NOTE: using TargetConnectionString allows explicit trust/encrypt settings
