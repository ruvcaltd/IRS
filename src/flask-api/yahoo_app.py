from flask import Flask, jsonify
from flask_swagger_ui import get_swaggerui_blueprint
from flask_swagger import swagger
import os

# import blueprints containing all the route handlers
from app.routes import pi_bp, health_bp, finance_bp

app = Flask(__name__)
app.secret_key = os.environ.get('SECRET_KEY', 'mysecret')

# register blueprints at application startup
app.register_blueprint(pi_bp)
app.register_blueprint(health_bp)
app.register_blueprint(finance_bp)

SWAGGER_URL = '/swagger'
API_URL = '/swagger.json'

swaggerui_blueprint = get_swaggerui_blueprint(
    SWAGGER_URL,
    API_URL,
    config={'app_name': "Yahoo Finance API"}
)
app.register_blueprint(swaggerui_blueprint, url_prefix=SWAGGER_URL)

@app.route(API_URL)
def swagger_spec():
    """Swagger API definition"""
    swag = swagger(app)
    swag['info']['title'] = "Yahoo Finance API"
    swag['info']['version'] = "1.0"

    swag.setdefault('securityDefinitions', {})['Bearer'] = {
        'type': 'apiKey',
        'name': 'Authorization',
        'in': 'header',
        'description': "JWT access token in the form 'Bearer <token>'"
    }

    for path_item in swag.get('paths', {}).values():
        for op in path_item.values():
            op.setdefault('security', []).append({'Bearer': []})
    return jsonify(swag)

if __name__ == "__main__":
    app.run(debug=True)
