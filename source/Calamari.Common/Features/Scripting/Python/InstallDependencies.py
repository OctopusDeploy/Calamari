import importlib
import subprocess
import sys

def install_package_using_pip(package):
    printverbose("Did not find dependency, attempting to install {} for the current user using pip".format(package))
    exitcode = subprocess.call([sys.executable, "-m", "pip", "install", package, "--user"])
    if exitcode != 0:
        print("Unable to install package {} using pip.".format(package), file=sys.stderr)
        print("If you do not have pip you can install {} using your favorite python package manager.".format(package), file=sys.stderr)
        exit(exitcode)

def install_missing_dependency(module, package):
    try:
        printverbose("Checking for dependency {}".format(package))
        importlib.import_module(module)
        printverbose("{} was found".format(package))
    except:
        install_package_using_pip(package)

def printverbose(message):
    print("##octopus[stdout-verbose]")
    print(message)
    print("##octopus[stdout-default]")

install_missing_dependency('Crypto.Cipher.AES', 'pycryptodome==3.19.1')