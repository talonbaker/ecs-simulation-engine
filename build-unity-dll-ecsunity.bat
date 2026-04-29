@echo off
:: ============================================================
:: build-unity-dll-ecsunity.bat
:: Publishes APIFramework, Warden.Contracts, and Warden.Telemetry
:: then copies the DLLs into ECSUnity/Assets/Plugins/.
::
:: APIFramework targets netstandard2.1 so Unity's Mono/IL2CPP
:: can load it natively without additional compat shims.
::
:: Newtonsoft.Json is supplied by com.unity.nuget.newtonsoft-json
:: in ECSUnity/Packages/manifest.json — do NOT copy it here.
:: A duplicate causes a Mono assembly conflict.
::
:: Run from the repo root:
::   build-unity-dll-ecsunity.bat
:: ============================================================

setlocal

set OUTPUT=ECSUnity\Assets\Plugins

echo.
echo [build-ecsunity] Publishing APIFramework...
dotnet publish APIFramework -c Release -o .build\APIFramework --no-self-contained
if errorlevel 1 (
    echo [build-ecsunity] ERROR: APIFramework publish failed.
    pause & exit /b 1
)

echo.
echo [build-ecsunity] Publishing Warden.Contracts...
dotnet publish Warden.Contracts -c Release -o .build\Warden.Contracts --no-self-contained
if errorlevel 1 (
    echo [build-ecsunity] ERROR: Warden.Contracts publish failed.
    pause & exit /b 1
)

echo.
echo [build-ecsunity] Publishing Warden.Telemetry...
dotnet publish Warden.Telemetry -c Release -o .build\Warden.Telemetry --no-self-contained
if errorlevel 1 (
    echo [build-ecsunity] ERROR: Warden.Telemetry publish failed.
    pause & exit /b 1
)

echo.
echo [build-ecsunity] Copying DLLs to %OUTPUT%...
if not exist "%OUTPUT%" mkdir "%OUTPUT%"

:: APIFramework
copy /Y ".build\APIFramework\APIFramework.dll"             "%OUTPUT%\APIFramework.dll"
copy /Y ".build\APIFramework\APIFramework.pdb"             "%OUTPUT%\APIFramework.pdb" 2>nul

:: Warden.Contracts — contains WorldStateDto and all contract types
copy /Y ".build\Warden.Contracts\Warden.Contracts.dll"    "%OUTPUT%\Warden.Contracts.dll"

:: Warden.Telemetry — WARDEN builds only; remove for RETAIL distribution builds
copy /Y ".build\Warden.Telemetry\Warden.Telemetry.dll"    "%OUTPUT%\Warden.Telemetry.dll"

:: DO NOT copy Newtonsoft.Json.dll — provided by com.unity.nuget.newtonsoft-json in manifest.json

echo.
echo [build-ecsunity] Done. Files in %OUTPUT%:
dir /b "%OUTPUT%\*.dll"
echo.
echo [build-ecsunity] Next steps:
echo   1. Open ECSUnity/ in Unity 6000.4.3f1
echo   2. Wait for asset reimport (first time is slow)
echo   3. Open Scenes/MainScene.unity and press Play
echo   4. Assign Assets/Settings/DefaultSimConfig.asset to EngineHost in Inspector
endlocal
pause
