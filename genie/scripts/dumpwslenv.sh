#! /bin/sh
# Set inside-genie flag.
echo INSIDE_GENIE=true > /run/genie.env

# Dump relevant environment variable.
echo WSL_DISTRO_NAME=$WSL_DISTRO_NAME >> /run/genie.env
echo WSL_INTEROP=$WSL_INTEROP >> /run/genie.env
echo WSLENV=$WSLENV >> /run/genie.env

