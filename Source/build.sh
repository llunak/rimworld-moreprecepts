#! /bin/sh
FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.7.2-api/ dotnet build *.csproj /property:Configuration=Release
if test $? -eq 0; then
    # no idea why these get created, but they break game loading
    rm -f ../Assemblies/System*.dll ../Assemblies/mscorlib*.dll
fi
