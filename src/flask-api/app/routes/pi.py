from flask import Blueprint
from flask_smorest import Blueprint as SmorestBlueprint
import math

pi_bp = SmorestBlueprint('pi', __name__, url_prefix='/api/pi', description='Pi calculation endpoints')

@pi_bp.route('/hello', methods=['GET'])
@pi_bp.response(200, description='Returns hello world message with Pi value')
def hello_pi():
    """
    Hello World Pi endpoint.
    Calculates and returns Pi value with a greeting message.
    """
    pi_value = math.pi
    return {
        'message': 'Hello World from Flask API!',
        'pi': pi_value,
        'pi_formatted': f'{pi_value:.10f}'
    }, 200
