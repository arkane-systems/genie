#
# Fedora Makefile.
#

#
# Default target: package.
#

package:
	# Packaging for Fedora.
	rpmdev-setuptree
	export VERSION=`rpmspec  -q --qf %{Version} --srpm genie.spec`
	cd ../.. ; tar zcvf ~/rpmbuild/SOURCES/genie-${VERSION}.tar.gz * --transform='s/^/genie-${VERSION}\//'
	rpmbuild -ba -v genie.spec
	mkdir -p ../../out/fedora
	mv ~/rpmbuild/RPMS/x86_64/genie* ../../out/fedora

clean:
	# Clean up temporary tree.
	rm -rf ~/rpmbuild
