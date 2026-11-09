using System;
using System.IO;
using DiosPunk.Flow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Infraestructura de audio del GDD: SFX sintéticos procedurales (placeholders de UI, juicios, proyectiles,
    /// escudo y Modo Punk), la SfxLibrary que los expone al área de sonido, los stems "punk" (segunda mezcla con
    /// guitarra distorsionada, sincronizada con la pista base) y el prefab de la cortinilla de carga fanzine.
    /// Todo es determinista (semillas fijas): regenerar no cambia los bytes. Los SFX solo se crean si faltan, y la
    /// biblioteca solo se llena donde no haya clips definitivos asignados.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string SfxDir = AudioDir + "/Sfx";
        private const string SfxLibraryDir = "Assets/_Project/Resources/Audio";
        private const string SfxLibraryPath = SfxLibraryDir + "/SfxLibrary.asset";
        private const string TransitionDir = "Assets/_Project/Resources/UI";
        private const string TransitionPrefabPath = TransitionDir + "/SceneTransition.prefab";
        private const string GrainPngPath = ArtDir + "/PaperGrain_Placeholder.png";
        private const int SfxRate = 44100;

        private static readonly Color PaperRed = new Color(0.85f, 0.1f, 0.1f, 1f);

        [MenuItem("DiosPunk/Audio/Regenerate SFX placeholders")]
        public static void RegenerateSfxPlaceholders()
        {
            BuildSfxAssets(true);
            Debug.Log("[Audio] SFX placeholders regenerados en " + SfxDir + ".");
        }

        // ============================================================
        // SFX + biblioteca
        // ============================================================

        private struct SfxSpec
        {
            public string file;
            public Func<float[]> synth;
            public SfxId id;
            public float volume, pitchMin, pitchMax;
        }

        private static SfxSpec[] SfxSpecs() => new[]
        {
            new SfxSpec { file = "Sfx_UiClick", synth = SynthUiClick, id = SfxId.UiClick, volume = 0.8f, pitchMin = 0.95f, pitchMax = 1.05f },
            new SfxSpec { file = "Sfx_UiHover", synth = SynthUiHover, id = SfxId.UiHover, volume = 0.35f, pitchMin = 0.97f, pitchMax = 1.03f },
            new SfxSpec { file = "Sfx_HitPerfect", synth = SynthHitPerfect, id = SfxId.HitPerfect, volume = 0.7f, pitchMin = 1f, pitchMax = 1.08f },
            new SfxSpec { file = "Sfx_HitGood", synth = SynthHitGood, id = SfxId.HitGood, volume = 0.6f, pitchMin = 0.95f, pitchMax = 1.02f },
            new SfxSpec { file = "Sfx_Miss", synth = SynthMiss, id = SfxId.Miss, volume = 0.7f, pitchMin = 1f, pitchMax = 1f },
            new SfxSpec { file = "Sfx_NpcThrow", synth = SynthNpcThrow, id = SfxId.NpcThrow, volume = 0.8f, pitchMin = 0.95f, pitchMax = 1.05f },
            new SfxSpec { file = "Sfx_TomatoImpact", synth = SynthTomatoImpact, id = SfxId.TomatoImpact, volume = 0.9f, pitchMin = 0.92f, pitchMax = 1.06f },
            new SfxSpec { file = "Sfx_ShieldBreak", synth = SynthShieldBreak, id = SfxId.ShieldBreak, volume = 0.85f, pitchMin = 1f, pitchMax = 1f },
            new SfxSpec { file = "Sfx_PunkActivate", synth = SynthPunkActivate, id = SfxId.PunkActivate, volume = 0.9f, pitchMin = 1f, pitchMax = 1f },
            new SfxSpec { file = "Sfx_PunkShout_01", synth = SynthShoutA, id = SfxId.PunkShout, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new SfxSpec { file = "Sfx_PunkShout_02", synth = SynthShoutE, id = SfxId.PunkShout, volume = 1f, pitchMin = 1f, pitchMax = 1f }
        };

        private static void BuildSfxAssets(bool force)
        {
            Directory.CreateDirectory(SfxDir);
            Directory.CreateDirectory(SfxLibraryDir);
            AssetDatabase.Refresh();

            SfxSpec[] specs = SfxSpecs();
            for (int i = 0; i < specs.Length; i++)
            {
                string path = SfxDir + "/" + specs[i].file + ".wav";
                if (!force && File.Exists(path)) continue;
                WriteWavFloat(path, specs[i].synth());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ImportPcmMono(path);
            }

            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(library, SfxLibraryPath);
            }

            var so = new SerializedObject(library);
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                SerializedProperty entry = so.FindProperty(LibraryField(id));
                if (entry == null) throw new MissingFieldException(nameof(SfxLibrary), LibraryField(id));

                SerializedProperty clips = entry.FindPropertyRelative("clips");
                if (!AssignableWithPlaceholders(clips)) continue; // ya hay clips definitivos: no se pisan

                var assigned = new System.Collections.Generic.List<AudioClip>();
                float volume = 1f, pMin = 1f, pMax = 1f;
                for (int i = 0; i < specs.Length; i++)
                {
                    if (specs[i].id != id) continue;
                    assigned.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + specs[i].file + ".wav"));
                    volume = specs[i].volume;
                    pMin = specs[i].pitchMin;
                    pMax = specs[i].pitchMax;
                }

                clips.arraySize = assigned.Count;
                for (int i = 0; i < assigned.Count; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = assigned[i];
                entry.FindPropertyRelative("volume").floatValue = volume;
                entry.FindPropertyRelative("pitchMin").floatValue = pMin;
                entry.FindPropertyRelative("pitchMax").floatValue = pMax;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
        }

        private static string LibraryField(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiClick: return "_uiClick";
                case SfxId.UiHover: return "_uiHover";
                case SfxId.HitPerfect: return "_hitPerfect";
                case SfxId.HitGood: return "_hitGood";
                case SfxId.Miss: return "_miss";
                case SfxId.NpcThrow: return "_npcThrow";
                case SfxId.TomatoImpact: return "_tomatoImpact";
                case SfxId.ShieldBreak: return "_shieldBreak";
                case SfxId.PunkActivate: return "_punkActivate";
                default: return "_punkShout";
            }
        }

        /// <summary>true si el campo está vacío o solo contiene placeholders generados (no hay audio definitivo que proteger).</summary>
        private static bool AssignableWithPlaceholders(SerializedProperty clips)
        {
            for (int i = 0; i < clips.arraySize; i++)
            {
                UnityEngine.Object clip = clips.GetArrayElementAtIndex(i).objectReferenceValue;
                if (clip == null) continue;
                if (!AssetDatabase.GetAssetPath(clip).StartsWith(SfxDir, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static void ImportPcmMono(string path)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ---------------- Síntesis (mono 44.1 kHz, semillas fijas) ----------------

        private static float[] Synth(float seconds, Func<float, float> sample)
        {
            int n = Mathf.CeilToInt(seconds * SfxRate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = sample(i / (float)SfxRate);
            return Finish(data);
        }

        /// <summary>Normaliza a pico 0.9 y aplica un fade-out de 5 ms para evitar clics al final.</summary>
        private static float[] Finish(float[] data)
        {
            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.9f / peak;

            int fade = Mathf.Min(data.Length, SfxRate / 200);
            for (int i = 0; i < data.Length; i++)
            {
                float g = gain;
                int fromEnd = data.Length - 1 - i;
                if (fromEnd < fade) g *= fromEnd / (float)fade;
                data[i] *= g;
            }
            return data;
        }

        private sealed class Noise
        {
            private readonly System.Random _rng;
            private float _prev;
            private float _low;
            public Noise(int seed) { _rng = new System.Random(seed); }
            public float White() => (float)(_rng.NextDouble() * 2.0 - 1.0);
            /// <summary>Ruido pasa-altos (diferencia con la muestra previa): papel / arena.</summary>
            public float High() { float w = White(); float h = w - _prev; _prev = w; return h * 0.5f; }
            /// <summary>Ruido pasa-bajos de un polo (a = coeficiente, 0..1).</summary>
            public float Low(float a) { _low += a * (White() - _low); return _low; }
        }

        private static float SynthPhase(ref float phase, float hz)
        {
            phase += hz / SfxRate;
            if (phase > 1f) phase -= 1f;
            return phase;
        }

        // Papel / fanzine: golpecito seco con un "thump" grave.
        private static float[] SynthUiClick()
        {
            var noise = new Noise(11);
            float ph = 0f;
            return Synth(0.09f, t =>
            {
                float thump = Mathf.Sin(2f * Mathf.PI * SynthPhase(ref ph, 150f * Mathf.Exp(-t * 30f) + 60f)) * Mathf.Exp(-t * 45f) * 0.6f;
                return noise.High() * Mathf.Exp(-t * 70f) * 1.1f + thump;
            });
        }

        // Roce de papel muy suave.
        private static float[] SynthUiHover()
        {
            var noise = new Noise(12);
            return Synth(0.06f, t => noise.High() * Mathf.Sin(Mathf.PI * t / 0.06f));
        }

        // Impacto brillante tipo caja/rim.
        private static float[] SynthHitPerfect()
        {
            var noise = new Noise(13);
            return Synth(0.12f, t =>
                noise.High() * Mathf.Exp(-t * 35f) * 0.9f
                + Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 60f) * 0.45f
                + Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 80f) * 0.2f);
        }

        // Impacto más opaco: misma familia, menos brillo.
        private static float[] SynthHitGood()
        {
            var noise = new Noise(14);
            return Synth(0.1f, t =>
                noise.Low(0.5f) * Mathf.Exp(-t * 45f) * 0.7f
                + Mathf.Sin(2f * Mathf.PI * 700f * t) * Mathf.Exp(-t * 55f) * 0.5f);
        }

        // Error / corte: zumbido cuadrado que cae y se apaga de golpe.
        private static float[] SynthMiss()
        {
            var noise = new Noise(15);
            float ph = 0f;
            float prev = 0f;
            const float dur = 0.22f;
            return Synth(dur, t =>
            {
                float sq = SynthPhase(ref ph, 240f * Mathf.Exp(-t * 6f) + 70f) < 0.5f ? 1f : -1f;
                float raw = sq * Mathf.Exp(-t * 9f) * 0.5f + noise.White() * Mathf.Exp(-t * 14f) * 0.25f;
                float gate = t > dur * 0.75f ? 1f - (t - dur * 0.75f) / (dur * 0.25f) : 1f; // corte final
                float s = (raw + prev) * 0.5f * gate;
                prev = raw;
                return s;
            });
        }

        // Whoosh de lanzamiento: ruido filtrado cuyo brillo sube y baja.
        private static float[] SynthNpcThrow()
        {
            var noise = new Noise(16);
            float lp = 0f, slow = 0f;
            const float dur = 0.4f;
            return Synth(dur, t =>
            {
                float k = t / dur;
                float a = 0.05f + 0.4f * k;
                lp += a * (noise.White() - lp);
                slow += 0.01f * (lp - slow);
                return (lp - slow) * Mathf.Pow(Mathf.Sin(Mathf.PI * k), 1.5f);
            });
        }

        // Splat: golpe grave + ruido mojado + pequeñas burbujas.
        private static float[] SynthTomatoImpact()
        {
            var noise = new Noise(17);
            float ph = 0f;
            return Synth(0.45f, t =>
            {
                float thump = Mathf.Sin(2f * Mathf.PI * SynthPhase(ref ph, 140f * Mathf.Exp(-t * 14f) + 40f)) * Mathf.Exp(-t * 10f) * 0.8f;
                float splash = noise.Low(0.25f) * Mathf.Exp(-t * 9f) * 1.6f;
                float pops = 0f;
                for (int p = 0; p < 2; p++)
                {
                    float t0 = 0.12f + p * 0.09f;
                    if (t > t0) pops += Mathf.Sin(2f * Mathf.PI * (280f + p * 90f) * (t - t0)) * Mathf.Exp(-(t - t0) * 60f) * 0.25f;
                }
                return thump + splash + pops;
            });
        }

        // Metal que se rompe: parciales inarmónicos con decaimiento rápido y un crujido inicial.
        private static float[] SynthShieldBreak()
        {
            var noise = new Noise(18);
            float[] freqs = { 620f, 1480f, 2210f, 3470f, 5120f };
            float[] amps = { 0.35f, 0.3f, 0.25f, 0.18f, 0.12f };
            float[] decay = { 7f, 9f, 11f, 14f, 18f };
            return Synth(0.55f, t =>
            {
                float s = 0f;
                for (int i = 0; i < freqs.Length; i++) s += Mathf.Sin(2f * Mathf.PI * freqs[i] * t) * amps[i] * Mathf.Exp(-t * decay[i]);
                float crack = noise.High() * Mathf.Exp(-t * 40f) * 0.9f;
                float shatter = t > 0.06f ? noise.High() * Mathf.Exp(-(t - 0.06f) * 28f) * 0.4f : 0f;
                return s + crack + shatter;
            });
        }

        // Acorde de potencia distorsionado + platillo.
        private static float[] SynthPunkActivate()
        {
            var noise = new Noise(19);
            float p1 = 0f, p2 = 0f, p3 = 0f;
            return Synth(0.8f, t =>
            {
                float env = Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 3.5f);
                float chord = (Saw(SynthPhase(ref p1, 82.41f)) + Saw(SynthPhase(ref p2, 123.47f)) + Saw(SynthPhase(ref p3, 164.81f))) * 0.4f;
                float dist = (float)Math.Tanh(chord * 4f * env) * 0.8f;
                float crash = noise.High() * Mathf.Exp(-t * 5f) * 0.35f;
                return dist + crash;
            });
        }

        private static float Saw(float phase) => phase * 2f - 1f;

        // Placeholders vocales: fuente glotal (sierra con vibrato) filtrada por formantes de "a" y "e".
        private static float[] SynthShoutA() => SynthVowel(0.55f, 20, 170f, 230f, 190f, new[] { 800f, 1200f, 2600f }, new[] { 110f, 130f, 200f });

        private static float[] SynthShoutE() => SynthVowel(0.5f, 21, 210f, 265f, 200f, new[] { 420f, 2000f, 2800f }, new[] { 90f, 150f, 200f });

        private static float[] SynthVowel(float dur, int seed, float f0Start, float f0Peak, float f0End, float[] formants, float[] bandwidths)
        {
            var noise = new Noise(seed);
            float ph = 0f;
            var y1 = new float[formants.Length];
            var y2 = new float[formants.Length];
            var a1 = new float[formants.Length];
            var a2 = new float[formants.Length];
            var gain = new float[formants.Length];
            for (int i = 0; i < formants.Length; i++)
            {
                float r = Mathf.Exp(-Mathf.PI * bandwidths[i] / SfxRate);
                a1[i] = 2f * r * Mathf.Cos(2f * Mathf.PI * formants[i] / SfxRate);
                a2[i] = -r * r;
                gain[i] = (1f - r) * (i == 0 ? 1f : 0.7f);
            }

            return Synth(dur, t =>
            {
                float k = t / dur;
                float f0 = (k < 0.3f ? Mathf.Lerp(f0Start, f0Peak, k / 0.3f) : Mathf.Lerp(f0Peak, f0End, (k - 0.3f) / 0.7f))
                           * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                float src = Saw(SynthPhase(ref ph, f0)) + noise.White() * 0.08f; // soplido leve
                float sum = 0f;
                for (int i = 0; i < formants.Length; i++)
                {
                    float y = gain[i] * src + a1[i] * y1[i] + a2[i] * y2[i];
                    y2[i] = y1[i];
                    y1[i] = y;
                    sum += y;
                }
                float env = Mathf.Min(1f, t / 0.04f) * Mathf.Clamp01((dur - t) / 0.12f);
                return (float)Math.Tanh(sum * 2.5f) * env; // saturación suave: "arenga" rasposa
            });
        }

        private static void WriteWavFloat(string path, float[] samples)
        {
            int dataLen = samples.Length * 2;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataLen);
                bw.Write(new[] { 'W', 'A', 'V', 'E' });
                bw.Write(new[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(SfxRate);
                bw.Write(SfxRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(new[] { 'd', 'a', 't', 'a' });
                bw.Write(dataLen);
                for (int i = 0; i < samples.Length; i++)
                {
                    bw.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                }
            }
        }

        // ============================================================
        // Stem Punk (segunda mezcla sincronizada)
        // ============================================================

        /// <summary>
        /// Segunda mezcla del nivel: misma duración, frecuencia y grilla de beats que el metrónomo base, pero con un
        /// golpe de acorde de potencia distorsionado en cada beat y platillo en cada compás. Al ser exactamente del
        /// mismo largo (misma fórmula de muestras) queda alineada sample a sample con la pista base.
        /// </summary>
        private static void WritePunkStemWav(string path, float bpm, double firstBeat, int beats, float tailSeconds, float rootHz)
        {
            const int rate = 44100;
            double beat = 60.0 / bpm;
            int total = (int)((firstBeat + beats * beat + tailSeconds) * rate); // misma fórmula que WriteClickTrackWav
            var mix = new float[total];
            var noise = new Noise(30);

            int stabLen = (int)(Math.Min(0.28, beat * 0.9) * rate);
            for (int b = 0; b < beats; b++)
            {
                int start = (int)Math.Round((firstBeat + b * beat) * rate);
                bool downbeat = b % 4 == 0;
                float amp = downbeat ? 0.9f : 0.65f;
                float p1 = 0f, p2 = 0f, p3 = 0f;

                for (int n = 0; n < stabLen && start + n < total; n++)
                {
                    float t = n / (float)rate;
                    float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 11f);
                    float chord = (Saw(SynthPhase(ref p1, rootHz)) + Saw(SynthPhase(ref p2, rootHz * 1.498f)) + Saw(SynthPhase(ref p3, rootHz * 2f))) * 0.4f;
                    mix[start + n] += (float)Math.Tanh(chord * 5f * env) * amp * 0.55f;
                }

                if (downbeat)
                {
                    int crashLen = Math.Min((int)(0.35 * rate), total - start);
                    for (int n = 0; n < crashLen; n++)
                    {
                        float t = n / (float)rate;
                        mix[start + n] += noise.High() * Mathf.Exp(-t * 9f) * 0.3f;
                    }
                }
            }

            var shorts = new short[total];
            for (int i = 0; i < total; i++) shorts[i] = (short)(Mathf.Clamp(mix[i], -1f, 1f) * short.MaxValue * 0.9f);

            int dataLen = total * 2;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataLen);
                bw.Write(new[] { 'W', 'A', 'V', 'E' });
                bw.Write(new[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(rate);
                bw.Write(rate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(new[] { 'd', 'a', 't', 'a' });
                bw.Write(dataLen);
                for (int i = 0; i < shorts.Length; i++) bw.Write(shorts[i]);
            }
        }

        /// <summary>Genera, importa y devuelve el stem punk de una canción (mono PCM, igual que la base).</summary>
        private static AudioClip CreatePunkStemClip(string path, float bpm, double firstBeat, int beats, float rootHz)
        {
            WritePunkStemWav(path, bpm, firstBeat, beats, 1.5f, rootHz);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ImportPcmMono(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ============================================================
        // Cortinilla de carga (prefab)
        // ============================================================

        private static void BuildTransitionOverlayPrefab(BuiltAssets a)
        {
            Directory.CreateDirectory(TransitionDir);
            AssetDatabase.Refresh();

            // Grano de fotocopia: manchas grises dispersas y alguna raya vertical. Seamless (ruido por pixel).
            SavePixels(GrainPngPath, 256, 256, (x, y) =>
            {
                float h = Hash01(x, y, 1);
                float streak = Hash01(x, 0, 7) > 0.965f ? 0.18f : 0f;
                float alpha = h > 0.55f ? (h - 0.55f) * 0.9f : 0f;
                float v = 0.55f + Hash01(x, y, 3) * 0.45f;
                return new Color(v, v, v, Mathf.Clamp01(alpha + streak));
            });
            var importer = (TextureImporter)AssetImporter.GetAtPath(GrainPngPath);
            var ts = new TextureImporterSettings();
            importer.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect; // requisito de Image.Type.Tiled
            importer.SetTextureSettings(ts);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            var grain = AssetDatabase.LoadAssetAtPath<Sprite>(GrainPngPath);

            Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPngPath);
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath);

            var root = new GameObject("SceneTransition", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // por encima de cualquier canvas del juego
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var group = root.AddComponent<CanvasGroup>();

            Image bg = FindOrCreateImage(root.transform, "Background", white, new Color(0.06f, 0.06f, 0.06f, 1f),
                Center, Center, Vector2.zero, Vector2.zero, Image.Type.Simple);
            Stretch(bg.rectTransform);
            bg.raycastTarget = true; // absorbe los clics mientras la cortinilla está visible

            Image grainImage = FindOrCreateImage(root.transform, "Grain", grain, new Color(1f, 1f, 1f, 0.3f),
                Center, Center, Vector2.zero, Vector2.zero, Image.Type.Tiled);
            Stretch(grainImage.rectTransform);

            // Tira de papel pegada, ligeramente torcida, con el rótulo.
            Image paper = FindOrCreateImage(root.transform, "Paper", panel, Color.white,
                Center, Center, new Vector2(0f, 40f), new Vector2(980f, 230f), Image.Type.Sliced);
            paper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            TMP_Text label = FindOrCreateText(paper.rectTransform, "Label", "CARGANDO...", 104f, TextAlignmentOptions.Center,
                Center, Center, Vector2.zero, new Vector2(940f, 190f), Color.black);
            label.fontStyle = FontStyles.Bold;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            // Cinta de progreso (relleno rojo sobre papel).
            Image tape = FindOrCreateImage(root.transform, "ProgressTape", panel, Color.white,
                Center, Center, new Vector2(0f, -140f), new Vector2(980f, 52f), Image.Type.Sliced);
            tape.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1f);
            Image fill = FindOrCreateImage(tape.rectTransform, "Fill", white, PaperRed,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(960f, 28f), Image.Type.Filled);
            ConfigureFill(fill, 0f);

            TMP_Text footer = FindOrCreateText(root.transform, "Footer", "DIOS PUNK  -  UNR GAMELAB", 34f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -300f), new Vector2(1200f, 60f), new Color(0.7f, 0.7f, 0.7f, 1f));
            footer.fontStyle = FontStyles.Bold;

            var overlay = root.AddComponent<SceneTransitionOverlay>();
            SetRefs(overlay, ("_canvas", canvas), ("_group", group), ("_paper", paper.rectTransform),
                ("_label", label), ("_progressFill", fill));

            PrefabUtility.SaveAsPrefabAsset(root, TransitionPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Hash determinista 0..1 por pixel (grano reproducible).</summary>
        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
