#
# Debian Makefile.
#

#
# Default target: package.
#

package:
	# Packaging for Arch.
	updpkgsums
	makepkg
	mkdir -p ../../out/arch
	mv genie* ../../out/arch

clean:
	rm -rf src
	rm -rf pkg
