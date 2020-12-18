#
# This Makefile packages genie for Arch
#

# Package the Arch Linux package

arch-pkg:
	lxc launch images:archlinux genie-packer
	sleep 3
	lxc exec genie-packer -- pacman -Syu --noconfirm
	lxc exec genie-packer -- pacman -S --noconfirm base-devel dotnet-runtime-3.1 dotnet-sdk-3.1
	lxc exec genie-packer -- useradd -m bob -u 1000
	lxc file push -r genie genie-packer/home/bob
	lxc file push -r ../genie/conf genie-packer/home/bob
	lxc file push -r ../genie/docs genie-packer/home/bob
	lxc file push -r ../genie/scripts genie-packer/home/bob
	lxc file push archmake.sh genie-packer/home/bob/genie/archmake.sh
	lxc exec genie-packer --user 1000 --cwd /home/bob/genie -- ./archmake.sh
	lxc file push PKGBUILD genie-packer/home/bob

# clean: clean up after a build/package

clean:
	lxc delete genie-archive
