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
