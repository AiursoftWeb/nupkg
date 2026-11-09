#!/bin/sh
set -e
mkdir -p /var/lib/aiursoft-nupkg
chown www-data:www-data /var/lib/aiursoft-nupkg

# Bootstrap config if missing (matching Docker entrypoint pattern)
if [ ! -f /etc/aiursoft-nupkg/appsettings.json ]; then
    cp /usr/share/aiursoft-nupkg/appsettings.json /etc/aiursoft-nupkg/appsettings.json
fi
# Replace /usr/share copy with symlink to /etc
rm -f /usr/share/aiursoft-nupkg/appsettings.json
ln -sf /etc/aiursoft-nupkg/appsettings.json /usr/share/aiursoft-nupkg/appsettings.json
