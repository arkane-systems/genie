%global project https://github.com/arkane-systems/genie/
%global version 1.34

# debuginfo is 'not supported' for .NET binaries
%global debug_package %{nil}
%global _enable_debug_package 0
%global __os_install_post /usr/lib/rpm/brp-compress %{nil}

Name:          genie
Version:       %{version}
Release:       1%{?dist}
Summary:       A quick way into systemd "bottle" for WSL

License:       Unlicense
URL:           %{project}
Source0:       %{project}archive/%{version}.tar.gz
Requires:      systemd-container
Requires:      daemonize
Requires:      dotnet-runtme-5.0
BuildRequires: dotnet-sdk-5.0
BuildRequires: make

ExclusiveArch: x86_64

%description
A quick way into systemd "bottle" for WSL

%prep
%setup -q -n %{name}-%{version}

%build
cd genie
make

%install
install -d -p %{buildroot}%{_libexecdir}/%{name}
install -d -p %{buildroot}%{_sysconfdir}
install -m 4755 -vp genie/genie/bin/Release/net5.0/linux-x64/publish/genie %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp genie/runinwsl/bin/Release/net5.0/linux-x64/publish/runinwsl %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp genie/scripts/10-genie-envar.sh %{buildroot}%{_libexecdir}/%{name}
install -m 0755 -vp genie/conf/genie.ini %{buildroot}%{_sysconfdir}/

%post
ln -s %{_libexecdir}/%{name}/%{name} %{_bindir}/%{name}
ln -s %{_libexecdir}/%{name}/10-genie-envar.sh %{_exec_prefix}/lib/systemd/system-environment-generators/

%postun
rm -rf %{_libexecdir}/%{name}
rm -f %{_bindir}/%{name}
rm -f %{_exec_prefix}/lib/systemd/system-environment-generators/10-envar.sh

%clean
rm -rf %{buildroot}

%files
%{_libexecdir}/%{name}/*
%config %{_sysconfdir}/genie.ini

%changelog
* Wed Feb 17 2021 Gerard Braad <me@gbraad.nl> 1.34-1
- Initial version for Fedora
