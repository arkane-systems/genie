# Helper functions module

import os
import shutil
import subprocess

import nsenter
import psutil


def find_systemd():
    """Find the running systemd process and return its pid."""
    for proc in psutil.process_iter():
        if "systemd" in proc.name():
            return proc.pid

    return 0


def get_login_session_user():
    """Get the user logged into the current session, pre-setuid."""
    # return os.environ["LOGNAME"]
    return os.getlogin()


def get_systemd_state(sdp):
    """Get the systemd state, whether we are within or without the bottle."""

    if sdp == 0:
        return "offline"

    with nsenter.Namespace(sdp, 'pid'):
        sc = subprocess.run(["systemctl", "is-system-running"],
                            capture_output=True, text=True)
        return sc.stdout.rstrip()


def get_systemd_target():
    """Get the default systemd target."""
    return os.path.basename(os.path.realpath('/etc/systemd/system/default.target'))


def get_unshare_path():
    """Find the path to the unshare utility."""
    return shutil.which('unshare')


def get_wsl_distro_name():
    """Get the WSL distribution name."""
    return os.environ['WSL_DISTRO_NAME']
