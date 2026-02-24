import os

class Config:
    DB_CONNECTION_STRING = os.environ.get('DB_CONNECTION_STRING')
    JWT_SECRET_KEY = os.environ.get('JWT_SECRET_KEY')
    JWT_ISSUER = os.environ.get('JWT_ISSUER', 'MyApp')
    JWT_AUDIENCE = os.environ.get('JWT_AUDIENCE', 'MyApp')
    DOTNET_API_BASE_URL = os.environ.get('DOTNET_API_BASE_URL', 'http://dotnet-api:8080')

    # flask-smorest settings
    API_TITLE = "Flask API"
    API_VERSION = "v1"
    OPENAPI_VERSION = "3.0.3"
    OPENAPI_URL_PREFIX = "/"
    OPENAPI_SWAGGER_UI_PATH = "/swagger"
    OPENAPI_SWAGGER_UI_URL = "https://cdn.jsdelivr.net/npm/swagger-ui-dist/"
