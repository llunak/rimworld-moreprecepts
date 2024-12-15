#! /bin/bash
name=$(ls *.csproj | sed 's/.csproj//')
rm -f ../Assemblies/$name.dll 2>/dev/null
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build $name.csproj /property:Configuration=Release
