#
# This Makefile packages genie for Arch
#

# Package the Arch Linux package

arch-pkg:
	make -C genie
	mkdir -p /tmp/genie-makepkg
	cp PKGBUILD /tmp/genie-makepkg
	HERE=$(pwd)
	cd /tmp/genie-makepkg
	makepkg -s
	cp *.pkg.tar.gz $HERE
	cd $HERE

# clean: clean up after a build/package

clean:
	make -C genie clean
	rm -rf /tmp/genie-makepkg
	rm *.pkg.tar.xz
