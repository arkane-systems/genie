# Binary formats function module

import os
import subprocess


def mount(verbose):
    """Mount the binfmt_misc filesystem, if it is not mounted."""

    # Having unmounted the binfmts fs before starting systemd, we remount it as
    # a courtesy. But remember, genie is not guaranteed to be idempotent, so don't
    # rely on this, for the love of Thompson and Ritchie!

    if not os.path.exists('/proc/sys/fs/binfmt_misc'):

        if verbose:
            print("genie: remounting binfmt_misc filesystem as a courtesy")

        sp = subprocess.run(['mount', '-t', 'binfmt_misc',
                             'binfmt_misc', '/proc/sys/fs/binfmt_misc'])

        if sp.returncode != 0:
            print(
                "genie: failed to remount binfmt_misc filesystem; attempting to continue")


def umount(verbose):
    """Unmount the binfmts filesystem, if it is mounted."""
    if os.path.exists('/proc/sys/fs/binfmt_misc'):

        if verbose:
            print("genie: unmounting binfmt_misc filesystem before proceeding")

        sp = subprocess.run(['umount', '/proc/sys/fs/binfmt_misc'])

        if sp.returncode != 0:
            print(
                "genie: failed to unmount binfmt_misc filesystem; attempting to continue")

    else:
        if verbose:
            print("no binfmt_misc filesystem present")


def check_flags(verbose):
    """Check the flags for the current binfmt filesystem."""
    if os.path.exists('/proc/sys/fs/binfmt_misc/WSLInterop'):
        with open('/proc/sys/fs/binfmt_misc/WSLInterop', 'rt') as wif:
            for wl in wif:
                if wl.startswith('flags: '):
                    flags = wl.rstrip()[7:]

                    if verbose:
                        print(f'genie: WSL interop flags detected: {flags}')

                    return flags

            if verbose:
                print("genie: could not find WSLInterop flags")
                    
            return None

    else:
        if verbose:
            print("genie: no WSLInterop configuration present")

        return None


def write_interop_file(verbose, flags):
    """Write out a new WSL interop file with the specified flags."""
    if os.path.exists('/usr/lib/binfmt.d'):

        if flags is None:
            if verbose:
                print ('genie: no WSLInterop configuration available; assuming PF')
            flags = 'PF'

        with open('/usr/lib/binfmt.d/WSLInterop.conf', 'w') as f:
            f.write(f':WSLInterop:M::MZ::/init:{flags}')

        if verbose:
            print ('genie: written new WSLInterop config')

    else:
        print ('genie: systemd binfmt.d is not available')
