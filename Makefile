#
# This Makefile builds and packages genie by invoking relevant sub-makefiles.
#

#
# Default target: list options
#

default:
	# Build options include:
        #
	# make install-local
	#
	# make package-tar
	# make package-debian (requires Debian build environment)
	# make package-arch (requires Arch build environment)
	# make package-fedora (requires Fedora build environment)
	#
	# make clean
	# make clean-local
	# make clean-tar
	# make clean-debian
	# make clean-arch
	# make clean-fedora
	#
	# make binaries-only

#
# Targets: individual end-product build.
#

clean:
	make -C binsrc clean
	rm -rf out

install-local:
	make -C package/local package

clean-local:
	make -C package/local clean

package-tar: make-output-directory
	make -C package/tar package

clean-tar:
	make -C package/tar clean

package-debian: make-output-directory
	make -C package/debian package

clean-debian:
	make -C package/debian clean

package-arch: make-output-directory
	make -C package/arch package

clean-arch:
	make -C package/arch clean

package-fedora: make-output-directory # build-binaries
	make -C package/fedora package

clean-fedora:
	make -C package/fedora clean

#
# Helpers: intermediate build stages.
#

make-output-directory:
	mkdir -p out

binaries-only:
	mkdir -C binsrc

