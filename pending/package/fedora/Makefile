#
# Fedora Makefile.
#

#
# Default target: package.
#

genie_version:=$(shell rpmspec -q --qf %{Version} --srpm genie.spec)

sourcedir=$(shell rpm --eval '%{_sourcedir}')
rpmdir=$(shell rpm --eval '%{_rpmdir}')

package:
	# Packaging for Fedora.
	rpmdev-setuptree
	tar zcvf $(sourcedir)/genie-${genie_version}.tar.gz * --dereference --transform='s/^/genie-${genie_version}\//'
	rpmbuild -ba -v genie.spec
	mkdir -p ../../out/fedora
	mv $(rpmdir)/x86_64/genie* ../../out/fedora

clean:
	# Clean up temporary tree.
	rpmdev-wipetree
