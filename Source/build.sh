#! /bin/bash
name=MorePrecepts
rm -f ../Assemblies/$name.dll 2>/dev/null
FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.7.2-api/ DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build $name.csproj /property:Configuration=Release
if test $? -eq 0; then
    # no idea why these get created, but they break game loading
    shopt -s extglob
    rm -f $(ls -1 ../Assemblies/*.dll | grep -v $name)
fi
