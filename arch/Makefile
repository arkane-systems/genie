#
# This Makefile packages genie for Arch
#

# Package the Arch Linux package

arch-pkg:
	make -C genie
	makepkg -s

# clean: clean up after a build/package

clean:
	makepkg -c
	make -C genie clean
	rm *.pkg.xz

