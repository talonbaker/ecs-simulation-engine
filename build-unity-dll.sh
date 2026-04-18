#!/usr/bin/env bash
# ============================================================
# build-unity-dll.sh
# Builds APIFramework as a .NET DLL and copies it into the
# Unity project's Plugins folder.
#
# Run from the repo root:
#   ./build-unity-dll.sh
# ============================================================

set -e

PROJECT="APIFramework"
OUTPUT="UnityVisualizer/Assets/Plugins"

echo
echo "[build-unity-dll] Building $PROJECT..."
echo

dotnet build "$PROJECT" -c Release -o ".build/$PROJECT"

echo
echo "[build-unity-dll] Copying DLL to $OUTPUT..."

mkdir -p "$OUTPUT"

cp -f ".build/$PROJECT/APIFramework.dll" "$OUTPUT/APIFramework.dll"
cp -f ".build/$PROJECT/APIFramework.pdb" "$OUTPUT/APIFramework.pdb" 2>/dev/null || true

echo
echo "[build-unity-dll] Done.  DLL is at $OUTPUT/APIFramework.dll"
echo "[build-unity-dll] Refresh the Unity Editor to pick up the new DLL."
echo
