import importlib
import subprocess
import sys

def run_pip_install(package, extra_args):
    process = subprocess.Popen([sys.executable, "-m", "pip", "install", package, "--user"] + extra_args,
                               stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE,
                               universal_newlines=True)
    stdout, stderr = process.communicate()
    return process.returncode, stdout, stderr

def echo(stdout, stderr):
    if stdout:
        sys.stdout.write(stdout)
    if stderr:
        sys.stderr.write(stderr)

def install_package_using_pip(package):
    printverbose("Did not find dependency, attempting to install {} for the current user using pip".format(package))
    exitcode, stdout, stderr = run_pip_install(package, [])

    # Distributions that implement PEP 668 (Debian 12, Ubuntu 23.04+, Fedora 38+) mark the
    # system interpreter as externally managed, and pip then refuses to install anything
    # against it - including a --user install, which only ever writes to the user site
    # directory and never touches distro-managed files. --break-system-packages opts out of
    # that check. It was added in pip 23.0, so only retry when pip has told us that this is
    # why the install failed, rather than sending an unknown option to an older pip.
    if exitcode != 0 and "externally-managed-environment" in (stdout + stderr):
        printverbose("Python reported an externally managed environment, retrying the install with --break-system-packages")
        retry = run_pip_install(package, ["--break-system-packages"])
        # Only report the retry if it worked, so a successful install does not also print the
        # error from the first attempt. If it failed too, the first attempt's externally
        # managed message is the one worth showing - the retry just adds noise on top of it.
        if retry[0] == 0:
            exitcode, stdout, stderr = retry

    echo(stdout, stderr)

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

install_missing_dependency('Crypto.Cipher.AES', 'pycryptodome')