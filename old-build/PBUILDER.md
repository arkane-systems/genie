# Considerations applying to building the Debian Source Package with pbuilder(1)

## Firstly

Obviously, I need a pbuilder(1) base.tgz that includes the .NET Core SDK. To achieve this, I use the following hook script, as `/var/cache/pbuilder/hook.d/E10add-apr-core`:

```
#!/bin/sh
apt-get install -y wget ca-certificates
wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt-get update
apt-get install -y dotnet-sdk-3.1

```

## Secondly

It is necessary for the build process to have access to the network, as the .NET Core build process pulls in NuGet packages on an as-needed basis. Therefore, pbuilder(1) should be invoked with the command line:

```
sudo pbuilder build --use-network yes <dsc_package>
```
