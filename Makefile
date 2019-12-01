#
# This Makefile produces and packages the genie-systemd program.
#

#
# default target: build the release zip
#
basic:
	make -C genie
	# Merge in place
	mkdir -p systemd-genie/usr/bin
	# Copy config file to etc
	cp -r etc lib systemd-genie/
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/genie systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/*.dll systemd-genie/usr/bin/
	cp genie/bin/Release/netcoreapp3.0/linux-x64/publish/genie.runtimeconfig.json systemd-genie/usr/bin/
	chmod u+s systemd-genie/usr/bin/genie
	chmod a+rx systemd-genie/usr/bin/genie


all: basic
	# Set owner to root
	sudo chown root:root systemd-genie/usr/bin/*
	# Copy lib to systemd-genie
	cp -r lib systemd-genie/
	# Make the distro zip.
	sudo tar zcvf genie.tar.gz systemd-genie/*

#
# debian: build the deb installation package
#

pkg-deb:
	# Make debian subfolder
	mkdir -p systemd-genie/DEBIAN
	# Set in-package permissions
	sudo chmod -R 0755 systemd-genie/DEBIAN
	# Copy control file
	cp debian/control systemd-genie/DEBIAN/control
	# Compute md5 sums
	sudo md5sum systemd-genie/usr/bin/* > systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/lib/genie/* >> systemd-genie/DEBIAN/md5sums
	sudo md5sum systemd-genie/lib/systemd/system-environment-generators/* >> systemd-genie/DEBIAN/md5sums
	# Make .deb package
	sudo dpkg-deb --build systemd-genie

debian: clean all pkg-deb

#
# arch: build the arch installation package (files only)
#

arch: clean basic
	# No need to chown because the package is made in a fakeroot enviroment
	# Move lib to /usr/lib (location in Arch Linux)
	cp -r lib systemd-genie/usr/

#
# install: build for /usr/local and install locally
#

install:
	cp -r lib systemd-genie/
	make -C genie local
	echo "If you have not yet, you must install the dependent packages - daemonize, hostess, and all systemd deps."
	sudo install -Dm 4755 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/genie" -t "/usr/local/bin"
	sudo install -Dm 644 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/genie.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/Linux.ProcessManager.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/System.CommandLine.dll" -t "/usr/local/bin"
	sudo install -Dm 744 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/Tmds.LibC.dll" -t "/usr/local/bin"
	sudo install -Dm 644 -o root "genie/bin/ReleaseLocal/netcoreapp3.0/linux-x64/publish/genie.runtimeconfig.json" -t "/usr/local/bin"
	sudo install -Dm 755 -o root "systemd-genie/lib/genie/dumpwslenv.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/lib/genie/readwslenv.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/lib/genie/runinwsl.sh" -t "/usr/local/lib/genie/"
	sudo install -Dm 755 -o root "systemd-genie/lib/systemd/system-environment-generators/10-genie-envar.sh" -t "/usr/local/lib/systemd/system-environment-generators"

#
# clean: delete the package interim files and products
#

clean:
	make -C genie clean
	rm -rf systemd-genie
	rm -f genie.tar.gz systemd-genie.deb