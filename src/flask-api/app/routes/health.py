from flask import Blueprint
from flask_smorest import Blueprint as SmorestBlueprint

health_bp = SmorestBlueprint('health', __name__, url_prefix='/health', description='Health check endpoints')

@health_bp.route('', methods=['GET'])
@health_bp.response(200, description='Health check successful')
def health():
    """Health check endpoint."""
    return {'status': 'healthy'}, 200
