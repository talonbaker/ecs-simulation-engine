#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: generates placeholder chibi-cue sprite PNG files for WP-4.0.E.
///
/// USAGE
/// ──────
/// Open Assets → ECS → Generate Chibi Cue Placeholder Sprites.
/// Sprites are written to ECSUnity/Assets/Sprites/Silhouettes/ as simple colored
/// squares. Replace with final hand-drawn art in WP-4.1.2.
///
/// COLOR CODING (helps distinguish cues at a glance in Editor)
/// ─────────────────────────────────────────────────────────────
/// anger-lines     → red-orange   (#ff4422)
/// sweat-drop      → sky blue     (#44aaff)
/// sleep-z         → lavender     (#aaaaff)
/// red-face-flush  → crimson      (#ff2244)
/// green-face-nausea → lime       (#44ff88)
/// heart           → hot pink     (#ff44aa)
/// sparkles        → yellow       (#ffee44)
/// exclamation     → white        (#ffffff)
/// question-mark   → cyan         (#44ffee)
/// stink           → olive green  (#88aa44)
/// </summary>
public static class GenerateChibiCuePlaceholderSprites
{
    private static readonly (string filename, Color color)[] Cues =
    {
        ("cue_anger_lines.png",      new Color(1.0f, 0.27f, 0.13f)),
        ("cue_sweat_drop.png",       new Color(0.27f, 0.67f, 1.0f)),
        ("cue_sleep_z.png",          new Color(0.67f, 0.67f, 1.0f)),
        ("cue_red_face_flush.png",   new Color(1.0f, 0.13f, 0.27f)),
        ("cue_green_face_nausea.png", new Color(0.27f, 1.0f, 0.53f)),
        ("cue_heart.png",            new Color(1.0f, 0.27f, 0.67f)),
        ("cue_sparkles.png",         new Color(1.0f, 0.93f, 0.27f)),
        ("cue_exclamation.png",      new Color(1.0f, 1.0f, 1.0f)),
        ("cue_question_mark.png",    new Color(0.27f, 1.0f, 0.93f)),
        ("cue_stink_lines.png",      new Color(0.53f, 0.67f, 0.27f)),
    };

    [MenuItem("Assets/ECS/Generate Chibi Cue Placeholder Sprites")]
    public static void Generate()
    {
        const string outputFolder = "Assets/Sprites/Silhouettes";
        const int    SpriteSize   = 32;

        int generated = 0;
        int skipped   = 0;

        foreach (var (filename, color) in Cues)
        {
            var fullPath = Path.Combine(Application.dataPath.Replace("Assets", outputFolder), filename);
            var assetPath = Path.Combine(outputFolder, filename);

            if (File.Exists(fullPath))
            {
                Debug.Log($"[GenerateChibiCuePlaceholderSprites] Skipped (exists): {filename}");
                skipped++;
                continue;
            }

            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var pixels = new Color[SpriteSize * SpriteSize];

            // Fill with the cue color; draw a 1-px transparent border so sprites
            // have a visible silhouette edge in the pixel-art shader.
            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    bool isBorder = x == 0 || x == SpriteSize - 1 || y == 0 || y == SpriteSize - 1;
                    pixels[y * SpriteSize + x] = isBorder ? Color.clear : color;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            generated++;
        }

        AssetDatabase.Refresh();

        // Set import settings for each newly-created sprite.
        foreach (var (filename, _) in Cues)
        {
            var assetPath = Path.Combine(outputFolder, filename).Replace('\\', '/');
            var importer  = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) continue;

            importer.textureType       = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode        = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Debug.Log($"[GenerateChibiCuePlaceholderSprites] Done. Generated: {generated}, Skipped (existing): {skipped}.");
        EditorUtility.DisplayDialog(
            "Chibi Cue Sprites",
            $"Generated {generated} placeholder sprite(s).\nSkipped {skipped} that already exist.\n\n" +
            $"Sprites are in {outputFolder}. Assign them to SilhouetteAssetCatalog._chibiCueSprites " +
            $"in the Inspector, or wire them into each NpcSilhouetteInstance's EmotionSlot._iconSprites array.",
            "OK");
    }
}
#endif
