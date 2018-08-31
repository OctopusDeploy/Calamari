import base64
from runpy import run_path

def encode( value ):
    return base64.b64encode(value.encode('utf-8')).decode('utf-8');

def decode( value ):
    return base64.b64decode(value).decode('utf-8');
    
def get_octopusvariable( key ):
    return octopusvariables[encode(key)]; 

{{VariableDeclarations}}

run_path("{{TargetScriptFile}}")