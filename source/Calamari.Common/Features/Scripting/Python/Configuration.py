import base64
import os.path
import sys
import binascii
from Crypto.Cipher import AES 

unpad = lambda s: s[:-s[-1]]

def encode(value):
    return base64.b64encode(value.encode('utf-8')).decode('utf-8')

def decode(value):
    return base64.b64decode(value).decode('utf-8')

def decrypt(encrypted, iv):
    key = sys.argv[len(sys.argv) - 1]
    key = binascii.unhexlify(key)
    iv = binascii.unhexlify(iv)
    cipher = AES.new(key, AES.MODE_CBC, iv)
    decrypted = unpad(cipher.decrypt(base64.b64decode(encrypted)))
    return decrypted.decode('utf-8')

def get_octopusvariable(key):
    return octopusvariables.get(key, "")

def set_octopusvariable(name, value, sensitive=False):
    octopusvariables[name] = value
    name = encode(name)
    value = encode(value)

    if sensitive:
        print("##octopus[setVariable name='{0}' value='{1}' sensitive='{2}']".format(name, value, encode("True")))
    else:
        print("##octopus[setVariable name='{0}' value='{1}']".format(name, value))

def createartifact(path, fileName = None):
    if fileName is None:
        fileName = os.path.basename(path)

    serviceFileName = encode(fileName)

    length = str(os.stat(path).st_size) if os.path.isfile(path) else "0"
    length = encode(length)

    path = os.path.abspath(path)
    servicepath = encode(path)

    print("##octopus[stdout-verbose]");
    print("Artifact {0} will be collected from {1} after this step completes".format(fileName, path))
    print("##octopus[stdout-default]");
    print("##octopus[createArtifact path='{0}' name='{1}' length='{2}']".format(servicepath, serviceFileName, length))

def updateprogress(progress, message=None):
    encodedProgress = encode(str(progress))
    encodedMessage = encode(message)

    print("##octopus[progress percentage='{0}' message='{1}']".format(encodedProgress, encodedMessage))

def failstep(message=None):
    if message is not None:
        encodedMessage = encode(message)
        print("##octopus[resultMessage message='{}']".format(encodedMessage))
    exit(-1)

def printverbose(message):
    print("##octopus[stdout-verbose]")
    print(message)
    print("##octopus[stdout-default]")

def printhighlight(message):
    print("##octopus[stdout-highlight]")
    print(message)
    print("##octopus[stdout-default]")

def printwait(message):
    print("##octopus[stdout-wait]")
    print(message)
    print("##octopus[stdout-default]")

def printwarning(message):
    print("##octopus[stdout-warning]")
    print(message)
    print("##octopus[stdout-default]")

printverbose(sys.version)

{{VariableDeclarations}}