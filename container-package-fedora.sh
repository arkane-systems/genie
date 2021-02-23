#! /bin/sh
lxc exec package-fedora --user 1000 --cwd /pkg --env HOME=/home/avatar -- make package-fedora
