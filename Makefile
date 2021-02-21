#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

#
# Default target: clean, build all, package all.
#

default: clean make-output-directory build-binaries
	make -C package/tar package
	make -C package/debian package
	make -C package/arch package
	make -C package/fedora package

#
# Targets: individual end-product build.
#

clean:
	make -C binsrc clean
	make -C package/local clean
	make -C package/debian clean
	make -C package/arch clean
	make -C package/fedora clean
	make -C package/tar clean
	rm -rf out

package-local: build-local-binaries
	make -C package/local package

package-tar: make-output-directory build-binaries
	make -C package/tar package

package-debian: make-output-directory build-binaries
	make -C package/debian package

package-arch: make-output-directory build-binaries
	make -C package/arch package

package-fedora: make-output-directory build-binaries
	make -C package/fedora package

#
# Helpers: intermediate build stages.
#

build-binaries:
	make -C binsrc build

build-local-binaries:
	make -C binsrc build-local

make-output-directory:
	mkdir out

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

