#! /bin/sh
echo INSIDE_GENIE=true > /run/genie.env
echo WSL_DISTRO_NAME=$WSL_DISTRO_NAME >> /run/genie.env
echo WSL_INTEROP=$WSL_INTEROP >> /run/genie.env
echo WSLENV=$WSLENV >> /run/genie.env
