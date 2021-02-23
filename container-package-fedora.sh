#! /bin/sh
# This works on my machine with my local packaging container. It will not work for you.
lxc exec package-fedora --user 1000 --cwd /pkg --env HOME=/home/avatar -- make package-fedora
