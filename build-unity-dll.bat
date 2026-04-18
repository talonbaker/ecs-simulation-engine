@echo off
:: ============================================================
:: build-unity-dll.bat
:: Publishes APIFramework and copies the DLLs Unity needs into
:: Assets/Plugins/.
::
:: APIFramework targets netstandard2.1 so Unity's Mono can load it natively.
:: Newtonsoft.Json is provided by com.unity.nuget.newtonsoft-json (manifest.json)
:: and must NOT be copied to Assets/Plugins — a duplicate causes a Mono conflict.
:: PluginImportFixer.cs (Assets/Editor/) forces validateReferences=false on
:: APIFramework.dll after each reimport.
::
:: Run from the repo root:
::   build-unity-dll.bat
:: ============================================================

setlocal

set PROJECT=APIFramework
set OUTPUT=UnityVisualizer\Assets\Plugins

echo.
echo [build-unity-dll] Publishing %PROJECT%...
echo.

dotnet publish %PROJECT% -c Release -o .build\%PROJECT% --no-self-contained
if errorlevel 1 (
    echo.
    echo [build-unity-dll] ERROR: dotnet publish failed.
    pause
    exit /b 1
)

echo.
echo [build-unity-dll] Copying DLLs to %OUTPUT%...

if not exist "%OUTPUT%" mkdir "%OUTPUT%"

:: Copy APIFramework.dll + its PDB
copy /Y ".build\%PROJECT%\APIFramework.dll" "%OUTPUT%\APIFramework.dll"
copy /Y ".build\%PROJECT%\APIFramework.pdb" "%OUTPUT%\APIFramework.pdb" 2>nul

:: Newtonsoft.Json is provided by com.unity.nuget.newtonsoft-json in manifest.json.
:: Do NOT copy it here — a duplicate in Assets/Plugins causes a Mono assembly conflict.

echo.
echo [build-unity-dll] Done.
echo [build-unity-dll] Files in %OUTPUT%:
dir /b "%OUTPUT%\*.dll"
echo.
endlocal
pause
