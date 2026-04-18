@echo off
:: ============================================================
:: build-unity-dll.bat
:: Builds APIFramework as a .NET DLL and copies it into the
:: Unity project's Plugins folder so the visualiser can load it.
::
:: Run from the repo root:
::   build-unity-dll.bat
:: ============================================================

setlocal

set PROJECT=APIFramework
set OUTPUT=UnityVisualizer\Assets\Plugins

echo.
echo [build-unity-dll] Building %PROJECT%...
echo.

dotnet build %PROJECT% -c Release -o .build\%PROJECT%
if errorlevel 1 (
    echo.
    echo [build-unity-dll] ERROR: dotnet build failed.
    exit /b 1
)

echo.
echo [build-unity-dll] Copying DLL to %OUTPUT%...

if not exist "%OUTPUT%" mkdir "%OUTPUT%"

copy /Y ".build\%PROJECT%\APIFramework.dll"  "%OUTPUT%\APIFramework.dll"
copy /Y ".build\%PROJECT%\APIFramework.pdb"  "%OUTPUT%\APIFramework.pdb"  2>nul

echo.
echo [build-unity-dll] Done.  DLL is at %OUTPUT%\APIFramework.dll
echo [build-unity-dll] Refresh the Unity Editor (Ctrl+R) to pick up the new DLL.
echo.
endlocal
