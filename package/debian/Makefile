#
# Debian Makefile.
#

# Installation locations.

DESTDIR=debian/systemd-genie

INSTALLDIR = $(DESTDIR)/usr/libexec/genie
BINDIR = $(DESTDIR)/usr/bin
ETCDIR = $(DESTDIR)/etc

#
# Default target: package.
#

package:
	# Packaging for Debian.
	debuild
	mkdir -p ../../out/debian
	mv ../systemd-genie* ../../out/debian

package-inner:
	# Innermost iteration; invoked by debuild.

	# Binaries.
	mkdir -p "$(BINDIR)"
	install -Dm 4755 -o root "../../binsrc/genie/bin/Release/net5.0/linux-x64/publish/genie" -t "$(INSTALLDIR)"
	install -Dm 0755 -o root "../../binsrc/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(INSTALLDIR)"

	# Environment generator.
	install -Dm 0755 -o root "../../othersrc/scripts/10-genie-envar.sh" -t "$(INSTALLDIR)"

	# Configuration file.
	install -Dm 0644 -o root "../../othersrc/etc/genie.ini" -t "$(ETCDIR)"

clean:
	debuild -- clean

clean-inner:
	# Innermost iteration; invoked by debuild.
	# Nothing to do.
