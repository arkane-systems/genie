#
# Local installation Makefile.
#

# Installation locations.
INSTALLDIR = /usr/local/libexec/genie
BINDIR = /usr/local/bin
ENVGENDIR = /usr/lib/systemd/system-environment-generators
USRENVGENDIR = /usr/lib/systemd/user-environment-generators
ETCDIR = /etc
MAN8DIR = /usr/local/share/man/man8

#
# Default target: package.
#

package:
	# Installing locally.
	make -C binsrc build-local

	# Just put the bits in the right places.
	mkdir -p "$(BINDIR)"

	# Binaries.
	install -Dm 4755 -o root "binsrc/genie/bin/ReleaseLocal/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "othersrc/scripts/bindxsocket.sh" -t "$(INSTALLDIR)"
	ln -s $(INSTALLDIR)/genie $(BINDIR)/genie

	# Environment generator.
	install -Dm 0755 -o root "othersrc/scripts/10-genie-envar.sh" -t "$(INSTALLDIR)"
	ln -s $(INSTALLDIR)/10-genie-envar.sh $(ENVGENDIR)/10-genie-envar.sh
	ln -s $(INSTALLDIR)/10-genie-envar.sh $(USRENVGENDIR)/10-genie-envar.sh

	# Configuration file.
	install -Dm 0644 -o root "othersrc/etc/genie.ini" -t "$(ETCDIR)"

	# Man page.
	# Make sure directory exists.
	mkdir -p "$(MAN8DIR)"

	# this bit would ordinarily be handed by debuild, etc.
	cp "othersrc/docs/genie.8" /tmp/genie.8
	gzip -9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8DIR)"

clean:
	# Nothing to clean in a local installation.
