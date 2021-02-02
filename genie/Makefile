#
# This Makefile produces and packages the genie program.
#

# Installation locations for 'make install'
BIN = $(DESTDIR)/usr/bin
ETC = $(DESTDIR)/etc
LIBEXEC = $(DESTDIR)/usr/libexec/genie
ENVGEN = $(DESTDIR)/usr/lib/systemd/system-environment-generators
USRENVGEN = $(DESTDIR)/usr/lib/systemd/user-environment-generators
MAN8 = $(DESTDIR)/usr/share/man/man8
DOCS = $(DESTDIR)/usr/share/doc/genie

#
# default target: build the whole thing, but do not package
#
build:
	make -C genie
	make -C runinwsl

#
# (INTERNAL) install: install the package
#
install:
	install -Dm 4755 -o root "genie/bin/Release/net5.0/linux-x64/publish/genie" -t "$(LIBEXEC)"
	install -Dm 0755 -o root "runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "$(LIBEXEC)"
	install -Dm 0755 -o root "scripts/10-genie-envar.sh" -t "$(LIBEXEC)"
	mkdir -p "$(BIN)"
	install -Dm 0644 -o root "conf/genie.ini" -t "$(ETC)"

#
# debian-pkg: build debian package
#
debian-pkg:
	debuild -d
	
#
# debian-pkg-ci: build debian package (continuous integration)
#

debian-pkg-ci:
	echo $KEY | gpg --import --yes
	echo "force key unlock" | gpg -s -a --passphrase "$PP" --yes
	debuild -d -k0x428ad1a0ba078abb

#
# (INTERNAL) install-local: install a built genie package locally
#
build-local:
	make -C genie local
	make -C runinwsl

install-local:
	# Local install of the above
	install -Dm 4755 -o root "genie/bin/Release/net5.0/linux-x64/publish/genie" -t "/usr/local/libexec/genie"
	install -Dm 0755 -o root "runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl" -t "/usr/local/libexec/genie"
	# Environment generator has to go in the same place
	install -Dm 0755 -o root "scripts/10-genie-envar.sh" -t "/usr/local/libexec/genie"
	mkdir -p "$(BIN)"
	# Create symlink manually
	ln -s /usr/local/libexec/genie/genie /usr/local/bin/genie
	ln -s /usr/local/libexec/genie/10-genie-envar.sh $(ENVGEN)/10-genie-envar.sh
	ln -s /usr/local/libexec/genie/10-genie-envar.sh $(ENVGEN)/10-genie-envar.sh
	# Config file also to the same place
	install -Dm 0644 -o root "conf/genie.ini" -t "$(ETC)"
	# These are handled elsewhere by debuild, so we need to do them manually.
	cp "docs/genie.8" /tmp/genie.8
	gzip -9 "/tmp/genie.8"
	install -Dm 0644 -o root "/tmp/genie.8.gz" -t "$(MAN8)"

#
# local: build and install locally
#
local: distclean build-local install-local

#
# clean: clean up after a build/package
#
distclean:
	make -C genie clean
	make -C runinwsl clean

clean:
	debuild -- clean
