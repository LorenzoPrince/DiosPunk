using System;
using System.IO;
using DiosPunk.Flow;
using UnityEditor;
using UnityEngine;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Placeholders procedurales de los NPC saboteadores, el proyectil, el escudo Punk, el marco dentado del
    /// debuff, el retrato de diálogo y el clic del metrónomo de calibración, más los DialogueData de los 5 niveles.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string Npc1PngPath = ArtDir + "/Npc1_Placeholder.png";
        private const string Npc2PngPath = ArtDir + "/Npc2_Placeholder.png";
        private const string ProjectilePngPath = ArtDir + "/Projectile_Placeholder.png";
        private const string ShieldPngPath = ArtDir + "/PunkShield_Placeholder.png";
        private const string FramePngPath = ArtDir + "/DebuffFrame_Placeholder.png";
        private const string PortraitPngPath = ArtDir + "/Portrait_Placeholder.png";
        private const string CalibrationClickPath = AudioDir + "/Calibration_Click.wav";
        private const string DialogueDir = "Assets/_Project/Data/Dialogues";

        internal sealed class FxAssets
        {
            public Sprite Npc1, Npc2, Projectile, Shield, Frame, Portrait;
            public AudioClip Click;
        }

        private static FxAssets BuildFxAssets()
        {
            Directory.CreateDirectory(DialogueDir);

            SaveMaskedSprite(Npc1PngPath, 128, 256, (x, y) => NpcMask(x, y, false), new Color(0.07f, 0.07f, 0.07f), Color.white, 3);
            SaveMaskedSprite(Npc2PngPath, 128, 256, (x, y) => NpcMask(x, y, true), new Color(0.18f, 0.18f, 0.18f), Color.white, 3);
            SaveMaskedSprite(ProjectilePngPath, 96, 96, StarMask, Color.white, Color.black, 3);
            SaveMaskedSprite(PortraitPngPath, 192, 192, PortraitMask, new Color(0.2f, 0.2f, 0.2f), Color.white, 4, true);

            SavePixels(ShieldPngPath, 160, 160, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(80f, 80f));
                if (d >= 62f && d <= 72f) return Color.white;
                return d < 62f ? new Color(1f, 1f, 1f, 0.18f) : Color.clear;
            });

            // Dientes hacia arriba: blanco con borde negro en la línea del diente (se tiñe con el color del proyectil).
            SavePixels(FramePngPath, 512, 96, (x, y) =>
            {
                float tri = Mathf.Abs((x % 32) / 16f - 1f);       // 0..1
                float h = 36f + 44f * (1f - tri);
                if (y > h) return Color.clear;
                return y >= h - 5f ? Color.black : Color.white;
            });

            WriteClickTrackWav(CalibrationClickPath, 600f, 0.0, 1, 0f);
            AssetDatabase.ImportAsset(CalibrationClickPath, ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(CalibrationClickPath);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            return ReloadFx();
        }

        /// <summary>Vuelve a cargar los assets de fx (abrir o crear una escena descarga los objetos sin referencias).</summary>
        private static FxAssets ReloadFx()
        {
            return new FxAssets
            {
                Npc1 = AssetDatabase.LoadAssetAtPath<Sprite>(Npc1PngPath),
                Npc2 = AssetDatabase.LoadAssetAtPath<Sprite>(Npc2PngPath),
                Projectile = AssetDatabase.LoadAssetAtPath<Sprite>(ProjectilePngPath),
                Shield = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldPngPath),
                Frame = AssetDatabase.LoadAssetAtPath<Sprite>(FramePngPath),
                Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPngPath),
                Click = AssetDatabase.LoadAssetAtPath<AudioClip>(CalibrationClickPath)
            };
        }

        // ---------------- Formas ----------------

        private static bool NpcMask(int x, int y, bool mohawk)
        {
            // Silueta tipo recorte: cabeza, cuello, torso trapezoidal, brazos y piernas.
            if (Vector2.Distance(new Vector2(x, y), new Vector2(64f, 205f)) <= 26f) return true;
            if (x >= 56 && x <= 72 && y >= 170 && y <= 184) return true;
            if (y >= 90 && y <= 180)
            {
                float half = Mathf.Lerp(30f, 38f, (y - 90f) / 90f);
                if (Mathf.Abs(x - 64f) <= half) return true;
            }
            if (y >= 100 && y <= 175 && ((x >= 16 && x <= 32) || (x >= 96 && x <= 112))) return true;
            if (y >= 4 && y <= 92 && ((x >= 40 && x <= 60) || (x >= 68 && x <= 88))) return true;

            if (mohawk && y >= 226)
            {
                // Tres crestas puntiagudas sobre la cabeza.
                for (int s = -1; s <= 1; s++)
                {
                    float sx = 64f + s * 14f;
                    float height = 232f + 22f - Mathf.Abs(x - sx) * 3.2f;
                    if (Mathf.Abs(x - sx) < 8f && y <= height) return true;
                }
            }
            return false;
        }

        private static bool StarMask(int x, int y)
        {
            Vector2 d = new Vector2(x - 48f, y - 48f);
            float angle = Mathf.Atan2(d.y, d.x) + Mathf.PI;                  // 0..2π
            float sector = angle / (2f * Mathf.PI / 8f);
            float tri = Mathf.Abs((sector - Mathf.Floor(sector)) * 2f - 1f);  // 0 en el valle, 1 en la punta
            float limit = Mathf.Lerp(20f, 44f, tri);
            return d.magnitude <= limit;
        }

        private static bool PortraitMask(int x, int y)
        {
            if (Vector2.Distance(new Vector2(x, y), new Vector2(96f, 118f)) <= 46f) return true;       // cabeza
            float ex = (x - 96f) / 86f;
            float ey = (y - 0f) / 76f;
            return y <= 76 && ex * ex + ey * ey <= 1f;                                                  // hombros
        }

        // ---------------- Generación ----------------

        private static void SaveMaskedSprite(string path, int w, int h, Func<int, int, bool> mask, Color fill, Color outline,
            int outlineWidth, bool halftone = false)
        {
            var inside = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    inside[y * w + x] = mask(x, y);

            SavePixels(path, w, h, (x, y) =>
            {
                if (inside[y * w + x])
                {
                    if (!halftone) return fill;
                    // Trama de fotocopia: puntos alternados.
                    bool dot = ((x / 3) + (y / 3)) % 2 == 0;
                    return dot ? fill : Color.Lerp(fill, Color.white, 0.45f);
                }

                for (int oy = -outlineWidth; oy <= outlineWidth; oy++)
                {
                    for (int ox = -outlineWidth; ox <= outlineWidth; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (inside[ny * w + nx]) return outline;
                    }
                }
                return Color.clear;
            });
        }

        private static void SavePixels(string path, int w, int h, Func<int, int, Color> pixel)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[y * w + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // ---------------- Diálogos ----------------

        private static void SetDialogueLine(SerializedProperty array, int index, string speaker, string text, Sprite portrait)
        {
            SerializedProperty line = array.GetArrayElementAtIndex(index);
            line.FindPropertyRelative("speaker").stringValue = speaker;
            line.FindPropertyRelative("text").stringValue = text;
            line.FindPropertyRelative("portrait").objectReferenceValue = portrait;
        }

        private static void SetChoice(SerializedProperty array, int index, string label, ChoiceVariable variable, int value)
        {
            SerializedProperty choice = array.GetArrayElementAtIndex(index);
            choice.FindPropertyRelative("label").stringValue = label;
            choice.FindPropertyRelative("variable").intValue = (int)variable;
            choice.FindPropertyRelative("value").intValue = value;
        }
    }
}
