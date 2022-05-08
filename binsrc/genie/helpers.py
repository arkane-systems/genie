# Helper functions module

import os


def get_wsl_distro_name():
    """Get the WSL distribution name."""
    return os.environ['WSL_DISTRO_NAME']
