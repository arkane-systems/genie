#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the release zip
#

all:
	make -C genie

#
# debian: build the deb installation package 
#

#
# fedora: build the fedora installation package
#

#
# install: build for /usr/local and install locally
#

#
# clean: delete the package interim files and products
#

clean:
	make -C genie clean

