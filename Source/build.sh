#! /bin/sh
FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.7.2-api/ dotnet build MorePrecepts.csproj /property:Configuration=Release
if test $? -eq 0; then
    # no idea why these get created, but they break game loading
    rm ../1.4/Assemblies/System.*.dll
fi
