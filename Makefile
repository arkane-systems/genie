#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

#
# Default target: list options
#

default:
	# Build options include:
	#
	# make build-binaries
	# make install-local
	#
	# Run "make build-binaries" before any of the package commands.
	#
	# make package-tar (NOT YET IMPLEMENTED)
	# make package-debian (requires Debian build environment)
	# make package-arch (requires Arch build environment; NOT YET TESTED)
	# make package-fedora (requires Fedora build environment; NOT YET TESTED)
	#
	# make clean
	# make clean-debian
	# make clean-arch
	# make clean-fedora

#
# Targets: individual end-product build.
#

clean:
	make -C binsrc clean
#	make -C package/local clean
#	make -C package/debian clean
#	make -C package/arch clean
#	make -C package/fedora clean
#	make -C package/tar clean
	rm -rf out

install-local: build-local-binaries
	make -C package/local package

package-tar: make-output-directory # build-binaries
	make -C package/tar package

package-debian: make-output-directory # build-binaries
	make -C package/debian package

clean-debian: clean
	make -C package/debian clean

package-arch: make-output-directory # build-binaries
	make -C package/arch package

clean-arch: clean
	make -C package/arch clean

package-fedora: make-output-directory # build-binaries
	make -C package/fedora package

clean-fedora: clean
	make -C package/fedora clean

#
# Helpers: intermediate build stages.
#

build-binaries:
	make -C binsrc build

build-local-binaries:
	make -C binsrc build-local

make-output-directory:
	mkdir -p out
