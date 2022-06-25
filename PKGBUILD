# Maintainer: Ong Yong Xin <ongyongxin2020+github AT gmail DOT com>
# Contributor: Xuanrui Qi <me@xuanruiqi.com>
# Contributor: Rayfalling <Rayfalling@outlook.com>
# Contributor: facekapow, rayfalling, Ducksoft
_pkgname=genie
pkgname=${_pkgname}-systemd
pkgver=2.5
pkgrel=1
pkgdesc="A quick way into a systemd \"bottle\" for WSL"
arch=('x86_64')
url="https://github.com/arkane-systems/genie"
license=('Unlicense')
depends=('daemonize' 'python>=3.7' 'python-psutil' 'systemd>=232.25' 'inetutils')
makedepends=('git' 'python-pip')
options=(!strip)
source=("git+https://github.com/arkane-systems/genie.git")
sha256sums=('SKIP')
backup=('etc/genie.ini')

# pkgver() {
#    git describe --long --tags | sed 's/\([^-]*-g\)/r\1/;s/-/./g;s/^v//g'
# }

build() {
    cd genie
    make build-binaries
}

package() {
    cd genie
    make DESTDIR=${pkgdir} internal-package
    make DESTDIR=${pkgdir} internal-supplement
}
