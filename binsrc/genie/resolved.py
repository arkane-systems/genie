# Resolver symlink function module

import os


def configure(verbose):
    """Replace resolv.conf with the required stub symlink for systemd-resolved."""
    # We cannot check if the target (/run/systemd/resolve/stub-resolv.conf) exists,
    # since it will not be created until after systemd-resolved starts up. So we're
    # going to have to live with that uncertainty.

    # Check if source file (/etc/resolv.conf) exists
    if os.path.lexists('/etc/resolv.conf'):
        # If so, move it to the backup file (/etc/resolv.conf.wsl)
        if verbose:
            print("genie: backing up /etc/resolv.conf to /etc/resolv.conf.wsl")

        if os.path.exists('/etc/resolv.conf.wsl'):
            os.remove('/etc/resolv.conf.wsl')

        os.rename('/etc/resolv.conf', '/etc/resolv.conf.wsl')

    # Create symlink from /etc/resolv.conf to /run/systemd/resolve/stub-resolv.conf
    if verbose:
        print("genie: creating symlink /etc/resolv.conf -> /run/systemd/resolve/stub-resolv.conf")

    os.symlink('/run/systemd/resolve/stub-resolv.conf', '/etc/resolv.conf')


def unconfigure(verbose):
    """Restore original resolv.conf."""
    # Check if /etc/resolv.conf exists, and if so, if it is a symlink
    if os.path.exists('/etc/resolv.conf'):
        if os.path.islink('/etc/resolv.conf'):
            # If so, remove it
            if verbose:
                print("genie: removing symlink /etc/resolv.conf")

            os.remove('/etc/resolv.conf')

            # Check if /etc/resolv.conf.wsl exists
            if os.path.exists('/etc/resolv.conf.wsl'):
                # If so, move it to /etc/resolv.conf
                if verbose:
                    print("genie: restoring /etc/resolv.conf from /etc/resolv.conf.wsl")

                os.rename('/etc/resolv.conf.wsl', '/etc/resolv.conf')
            else:
                # If not, warn the user
                print(
                    "genie: WARNING: /etc/resolv.conf.wsl does not exist; please restore /etc/resolv.conf manually")

        else:
            print("genie: WARNING: /etc/resolv.conf is not a symlink")

    else:
        print("genie: WARNING: /etc/resolv.conf does not exist")
