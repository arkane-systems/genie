# AppArmor control module

import os
import subprocess

import helpers


def configure(verbose):
    """Configure an AppArmor namespace for the genie bottle."""

    # If the AppArmor filesystem is not mounted, mount it.
    if not os.path.exists('/sys/kernel/security/apparmor'):
        if verbose:
            print("genie: mounting AppArmor filesystem")

        sp = subprocess.run(['mount', '-t', 'securityfs',
                             'securityfs', '/sys/kernel/security'])
        if sp.returncode != 0:
            print(
                "genie: failed to mount AppArmor filesystem; attempting to continue without AppArmor")
            return None

    # Create AppArmor namespace for genie bottle.
    nsName = 'genie-' + helpers.get_wsl_distro_name()

    if verbose:
        print(f"genie: creating AppArmor namespace '{nsName}'")

    if not os.path.exists('/sys/kernel/security/apparmor/policy/namespaces'):
        print("genie: could not find AppArmor filesystem; attempting to continue without AppArmor")
        return None

    os.mkdir('/sys/kernel/security/apparmor/policy/namespaces/' + nsName)

    return nsName


def unconfigure(verbose):
    """Clean up the AppArmor namespace when the bottle is stopped."""

    nsName = 'genie-' + helpers.get_wsl_distro_name()

    if verbose:
        print(f"genie: deleting AppArmor namespace '{nsName}'")

    if os.path.exists('/sys/kernel/security/apparmor/policy/namespaces/' + nsName):
        try:
            os.rmdir('/sys/kernel/security/apparmor/policy/namespaces/' + nsName)
        except OSError as e:
            print(
                f"genie: failed to delete AppArmor namespace; attempting to continue; {e.strerror}" + e.strerror)
    else:
        if verbose:
            print("genie: no AppArmor namespace to delete")
