#! /bin/sh
lxc exec package-arch --user 1000 --cwd /pkg --env HOME=/home/avatar -- make package-arch
