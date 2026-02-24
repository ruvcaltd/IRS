from flask import Flask
from flask_cors import CORS
from flask_smorest import Api

def create_app():
    # we maintain a single Flask instance defined in yahoo_app.py so that
    # both `gunicorn yahoo_app:app` and `flask run` (which uses create_app)
    # operate on the same application and share the same routes.
    from .. import yahoo_app
    app = yahoo_app.app

    # load additional config if needed
    app.config.from_object('app.config.Config')

    # ensure CORS is configured (may already have been configured in yahoo_app)
    CORS(app, origins=["http://localhost:4200","http://dotnet-api:5000"], supports_credentials=True)

    # flask-smorest Api wrapper (used by existing blueprints)
    Api(app)  # this will not re‑register blueprints; they are already on app

    return app
