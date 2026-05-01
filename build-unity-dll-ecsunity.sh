#!/usr/bin/env bash
# ============================================================
# build-unity-dll-ecsunity.sh
# Publishes APIFramework, Warden.Contracts, and Warden.Telemetry
# then copies the DLLs into ECSUnity/Assets/Plugins/.
#
# Run from the repo root:
#   chmod +x build-unity-dll-ecsunity.sh && ./build-unity-dll-ecsunity.sh
# ============================================================

set -e

OUTPUT="ECSUnity/Assets/Plugins"

echo ""
echo "[build-ecsunity] Publishing APIFramework..."
dotnet publish APIFramework -c Release -o .build/APIFramework --no-self-contained

echo ""
echo "[build-ecsunity] Publishing Warden.Contracts..."
dotnet publish Warden.Contracts -c Release -o .build/Warden.Contracts --no-self-contained

echo ""
echo "[build-ecsunity] Publishing Warden.Telemetry..."
dotnet publish Warden.Telemetry -c Release -o .build/Warden.Telemetry --no-self-contained

echo ""
echo "[build-ecsunity] Copying DLLs to $OUTPUT..."
mkdir -p "$OUTPUT"

cp -f .build/APIFramework/APIFramework.dll         "$OUTPUT/APIFramework.dll"
cp -f .build/APIFramework/APIFramework.pdb         "$OUTPUT/APIFramework.pdb" 2>/dev/null || true

cp -f .build/Warden.Contracts/Warden.Contracts.dll "$OUTPUT/Warden.Contracts.dll"
cp -f .build/Warden.Telemetry/Warden.Telemetry.dll "$OUTPUT/Warden.Telemetry.dll"

# System.Text.Json — used by Warden.Telemetry.TelemetrySerializer (active runtime
# call) and as [JsonStringEnumConverter] attribute on WorldStateDto. Unity's Mono
# runtime does not ship these; supply them from the publish output.
# DO NOT copy System.Memory, System.Buffers, System.Numerics.Vectors — those live
# in Unity's Mono and would conflict if duplicated.
cp -f .build/APIFramework/System.Text.Json.dll                     "$OUTPUT/System.Text.Json.dll"
cp -f .build/APIFramework/System.Text.Encodings.Web.dll            "$OUTPUT/System.Text.Encodings.Web.dll"
cp -f .build/APIFramework/System.Runtime.CompilerServices.Unsafe.dll "$OUTPUT/System.Runtime.CompilerServices.Unsafe.dll"
cp -f .build/APIFramework/Microsoft.Bcl.AsyncInterfaces.dll        "$OUTPUT/Microsoft.Bcl.AsyncInterfaces.dll"

# DO NOT copy Newtonsoft.Json.dll — provided by com.unity.nuget.newtonsoft-json

echo ""
echo "[build-ecsunity] Done. Files in $OUTPUT:"
ls -1 "$OUTPUT/"*.dll

echo ""
echo "[build-ecsunity] Next steps:"
echo "  1. Open ECSUnity/ in Unity 6000.4.3f1"
echo "  2. Wait for asset reimport"
echo "  3. Open Scenes/MainScene.unity and press Play"
echo "  4. Assign Assets/Settings/DefaultSimConfig.asset to EngineHost in the Inspector"
