#
# This Makefile package and alienizes the genie program.
#

#
# default target: build the whole thing, but do not package
#
package:
	make -C genie debian-pkg
	fpm -s deb -t tar `ls *.deb`
	gzip `ls *.tar`

#
# clean: clean up after a build/package
#
clean:
	make -C genie clean
	sudo rm *.deb *.build *.buildinfo *.changes *.dsc *.tar.gz
