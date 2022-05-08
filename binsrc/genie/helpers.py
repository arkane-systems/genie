# Helper functions module

import os


def get_login_session_user():
    """Get the user logged into the current session, pre-setuid."""
    # return os.environ["LOGNAME"]
    return os.getlogin()


def get_wsl_distro_name():
    """Get the WSL distribution name."""
    return os.environ['WSL_DISTRO_NAME']
