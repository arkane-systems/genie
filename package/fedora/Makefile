#
# Fedora Makefile.
#

#
# Default target: package.
#

genie_version:=$(shell rpmspec -q --qf %{Version} --srpm genie.spec)

package:
	# Packaging for Fedora.
	rpmdev-setuptree
	tar zcvf ~/rpmbuild/SOURCES/genie-${genie_version}.tar.gz * --dereference --transform='s/^/genie-${genie_version}\//'
	rpmbuild -ba -v genie.spec
	mkdir -p ../../out/fedora
	mv ~/rpmbuild/RPMS/x86_64/genie* ../../out/fedora

clean:
	# Clean up temporary tree.
	rm -rf ~/rpmbuild
