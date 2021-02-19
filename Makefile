#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

#
# default target: build the whole thing, and package for debian
#
#package:
#	make -C genie debian-pkg
#	fpm -s deb -t tar `ls *.deb`
#	gzip `ls *.tar`
#
#debian: package

#
# clean: clean up after a build/package
#
#clean:
#	make -C genie clean
#	sudo rm *.deb *.build *.buildinfo *.changes *.dsc *.tar.xz *.tar.gz

#
# Package and clean up the Arch Linux package
#
#arch:
#	make -C arch
#
#arch-clean:
#	make -C arch clean

#
# Build and install locally
#

#local:
#	make -C genie local
#
#local-clean:
#	make -C genie distclean

