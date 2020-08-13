#
# This Makefile package and alienizes the genie program.
#

#
# default target: build the whole thing, but do not package
#
package:
	make -C genie debian-pkg
	sudo alien --to-rpm `ls *.deb`
	sudo alien --to-lsb `ls *.deb`
	sudo alien --to-tgz `ls *.deb`

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean
	sudo rm *.deb *.rpm *.build *.buildinfo *.changes *.dsc *.tar.xz *.tgz
