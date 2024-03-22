param(
    [string]$Version = '2.0'
)

# copy ffmpeg binaries, needed for flyleaf media player
xcopy Totoro.Installer\FFmpeg Totoro.WinUI\bin\publish\FFmpeg /s /e /h

# remove some localization files, (for some reason installer builder is getting failed becaues of these)
Remove-Item Totoro.WinUI\bin\publish\gd-gb\*, Totoro.WinUI\bin\publish\mi-Nz\*, Totoro.WinUI\bin\publish\ug-CN\*

# build installer
msbuild Totoro.Installer\Totoro.Installer.wixproj /property:Configuration=Release /property:BuildVersion=$Version /property:BasePath=Totoro.WinUI\bin\publish