#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the release zip
#

all:
	make -C genie
	# Merge in place
	mkdir -p systemd-genie/usr/bin
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/genie systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/*.dll systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/genie.runtimeconfig.json systemd-genie/usr/bin/
	# Set in-package permissions
	sudo chmod -R 0755 systemd-genie/DEBIAN
	sudo chown root:root systemd-genie/usr/bin/*
	sudo chmod u+s systemd-genie/usr/bin/genie
	sudo chmod a+rx systemd-genie/usr/bin/genie
	# Compute md5 sums
	sudo md5sum systemd-genie/usr/bin/* > systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/lib/genie/* >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/lib/systemd/system-environment-generators/* >> systemd-genie/DEBIAN/md5sums
	# Make the distro zip.
	sudo tar zcvf genie.tar.gz systemd-genie/*

#
# debian: build the deb installation package 
#

pkg-deb:
	# Make .deb package
	sudo dpkg-deb --build systemd-genie

debian: clean all pkg-deb

#
# install: build for /usr/local and install locally
#

install:
	make -C genie local
	echo "Local installation not yet supported."

#
# clean: delete the package interim files and products
#

clean:
	make -C genie clean
	rm -rf systemd-genie/usr/bin/*
	rm -f genie.tar.gz systemd-genie.deb
