#! /bin/sh
# This works on my machine with my local packaging container. It will not work for you.
lxc exec package-arch --user 0 -- resolvectl dns eth0 172.16.0.128
lxc exec package-fedora --user 1000 --cwd /pkg --env HOME=/home/avatar -- make package-fedora
