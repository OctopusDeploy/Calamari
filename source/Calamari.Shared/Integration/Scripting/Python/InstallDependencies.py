import importlib
import subprocess
import sys

def install_missing_dependency(module, package):
    try:
        printverbose("Checking for dependency {}".format(package))
        importlib.import_module(module)
        printverbose("{} was found".format(package))
    except:
        printverbose("Did not find dependency, attempting to install {} for the current user using pip".format(package))
        subprocess.call([sys.executable, "-m", "pip", "install", package, "--user", "--disable-pip-version-check"])

def printverbose(message):
    print("##octopus[stdout-verbose]")
    print(message)
    print("##octopus[stdout-default]")

install_missing_dependency('Crypto.Cipher.AES', 'pycryptodome')