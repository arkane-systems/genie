# Hostname and hosts file functions module

import os
import subprocess
import sys

from python_hosts import Hosts, HostsEntry

import configuration


def update(verbose):
    """Update the hostname and mount over previous hostname."""
    if verbose:
        print("genie: generating new hostname")

    external_hostname = os.uname().nodename

    if verbose:
        print(f"genie: external hostname is {external_hostname}")

    internal_hostname = external_hostname + configuration.update_hostname_suffix()

    # Create the hostname file
    try:
        with open('/run/genie.hostname', 'w') as hostfile:
            print(internal_hostname, file=hostfile)
            hostfile.close()
    except UnicodeEncodeError:
        print("genie: it appears that your hostname contains characters not permitted by Linux")
        print("       (per RFC 952/RFC 1123); this is probably because Windows permits Unicode")
        print("       hostnames and WSL inherits them. Please see here for details and workaround:")
        print("       https://github.com/arkane-systems/genie/issues/268\n")
        sys.exit("genie: cannot continue with illegal hostname")

    os.chmod('/run/genie.hostname', 0o644)

    # Mount the hostname file
    if verbose:
        print(f"genie: setting new hostname to {internal_hostname}")

    sp = subprocess.run(
        ['mount', '--bind', '/run/genie.hostname', '/etc/hostname'])

    if sp.returncode != 0:
        print("genie: failed to bind hostname file; attempting to continue")
        return

    if verbose:
        print("genie: updating hosts file")

    # Update hosts file (remove old hostname, add new hostname)
    _modify_hosts_file_entries(external_hostname, internal_hostname)


def restore(verbose):
    """Restore the hostname."""
    internal_hostname = os.uname().nodename

    if verbose:
        print("genie: dropping in-bottle hostname")

    # Drop the in-bottle hostname mount
    sp = subprocess.run(['umount', '/etc/hostname'])

    if sp.returncode != 0:
        print("genie: failed to unmount hostname file; attempting to continue")
        return

    # Remove the hostname file
    os.remove('/run/genie.hostname')

    # Reset hostname
    subprocess.run(['hostname', '-F', '/etc/hostname'])

    external_hostname = os.uname().nodename

    # Update hosts file (remove new hostname, add old hostname)
    _modify_hosts_file_entries(internal_hostname, external_hostname)


# Internal functions
def _modify_hosts_file_entries(old_name, new_name):
    """Modify the hosts file to replace old_name with new_name."""
    try:
        hosts = Hosts()

        entries = hosts.find_all_matching(name=old_name)

        # Iterate through all relevant entries
        for e in entries:
            # Iterate through all names
            new_names = []
            for n in e.names:
                # Modify name
                new_names.append(n.replace(old_name, new_name))
                new_entry = HostsEntry(
                    entry_type=e.entry_type, address=e.address, names=new_names, comment=e.comment)

                # Replace old entry
                hosts.add([new_entry], force=True)

        hosts.write()

    except:  # noqa
        print(f"genie: error occurred modifying hosts file ({sys.exc_info()[0]}); check format")
        print("genie: attempting to continue anyway...")
