# noname
{noname} - a new engine that mix sokol &amp; SDL2

# How to build the sample
```
dotnet publish -c Release -r win-x64 -o ../../bin/dist -p:AOT=true -p:ExtraDefineConstants=SOKOL_D3D11
```
