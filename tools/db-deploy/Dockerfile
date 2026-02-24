# db-deploy Dockerfile now assumes the dacpac is pre-built and placed under src/database/out

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS deploy

# Install prerequisites, mssql tools, and sqlpackage
RUN apt-get update \
	&& apt-get install -y curl apt-transport-https gnupg unzip ca-certificates unixodbc-dev \
	&& curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - \
	&& curl https://packages.microsoft.com/config/ubuntu/22.04/prod.list > /etc/apt/sources.list.d/mssql-release.list \
	&& apt-get update \
	&& ACCEPT_EULA=Y apt-get install -y msodbcsql18 mssql-tools18 \
	&& curl -L https://aka.ms/sqlpackage-linux -o /tmp/sqlpackage.zip \
	&& unzip /tmp/sqlpackage.zip -d /opt/sqlpackage \
	&& chmod +x /opt/sqlpackage/sqlpackage \
	&& rm /tmp/sqlpackage.zip \
	&& apt-get clean && rm -rf /var/lib/apt/lists/*

# Copy the pre-built dacpac into the deploy container
COPY src/database/out/*.dacpac /deploy/

# include the deployment helper script
COPY tools/db-deploy/deploy.sh /deploy/deploy.sh
RUN chmod +x /deploy/deploy.sh

ENTRYPOINT ["/deploy/deploy.sh"]
