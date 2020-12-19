#
# This Makefile package and alienizes the genie program.
#

#
# default target: build the whole thing, and package for debian
#
package:
	make -C genie debian-pkg
	fpm -s deb -t tar `ls *.deb`
	gzip `ls *.tar`

debian: package

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean
	sudo rm *.deb *.build *.buildinfo *.changes *.dsc *.tar.xz

#
# Package and clean up the Arch Linux package
#
arch:
	make -C arch arch-pkg

arch-clean:
	make -C arch clean
