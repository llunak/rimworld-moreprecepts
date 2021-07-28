#! /bin/sh
FrameworkPathOverride=$(dirname $(which mono))/../lib/mono/4.7.2-api/ dotnet build MorePrecepts.csproj /property:Configuration=Release
