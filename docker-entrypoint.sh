#!/bin/sh
set -eu

case "${ENABLE_HTTPS:-false}" in
    true)
        : "${HTTPS_CERT_PASSWORD:?HTTPS_CERT_PASSWORD is required when ENABLE_HTTPS=true}"
        export ASPNETCORE_URLS="http://+:8080;https://+:8443"
        export ASPNETCORE_HTTPS_PORT=8443
        export ASPNETCORE_Kestrel__Certificates__Default__Path=/https/LGUTreasury.pfx
        export ASPNETCORE_Kestrel__Certificates__Default__Password="$HTTPS_CERT_PASSWORD"
        export DisableHttpsRedirection=false
        unset HTTPS_CERT_PASSWORD
        ;;
    false)
        export ASPNETCORE_URLS="http://+:8080"
        export DisableHttpsRedirection=true
        unset ASPNETCORE_HTTPS_PORT
        unset ASPNETCORE_Kestrel__Certificates__Default__Path
        unset ASPNETCORE_Kestrel__Certificates__Default__Password
        unset HTTPS_CERT_PASSWORD
        ;;
    *)
        echo "ENABLE_HTTPS must be true or false."
        exit 1
        ;;
esac

exec dotnet LGUTreasury.dll
