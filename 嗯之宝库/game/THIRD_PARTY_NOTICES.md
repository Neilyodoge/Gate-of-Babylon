# Third-party components

## Raylib-cs 7.0.2

C# bindings and bundled native raylib binaries are obtained from the official NuGet package:
https://www.nuget.org/packages/Raylib-cs/7.0.2

Source: https://github.com/raylib-cs/raylib-cs

Raylib-cs uses the zlib license. The native raylib library also uses the zlib/libpng license.
This prototype links to the original package; it does not alter those libraries.

## Microsoft .NET

The default build is framework dependent and uses the separately installed .NET 9 runtime.
https://github.com/dotnet/runtime

## System font

The prototype reads SimHei from the Windows Fonts directory at runtime.
No system font file is included in the project or redistributed with the build.
