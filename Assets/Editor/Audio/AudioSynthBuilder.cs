using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Gera TODOS os sons e músicas do jogo por síntese (sem baixar nada): WAVs em Assets/Gameplay/Audio
    /// e o FeedbackBank que liga cada som a um evento. Tools > Furia Pinguim > Áudio.
    /// </summary>
    public static class AudioSynthBuilder
    {
        private const int Rate = 22050;
        private const string Dir = "Assets/Gameplay/Audio";
        public const string BankPath = Dir + "/FeedbackBank.asset";
        private const string Demo = "Assets/Dypsloom/DypThePenguin/Demo/";

        [MenuItem("Tools/Furia Pinguim/Áudio/Gerar sons e músicas")]
        public static void BuildMenu()
        {
            var bank = BuildAll();
            EditorUtility.DisplayDialog("Áudio", bank != null ? "Sons e músicas gerados em " + Dir : "Falhou", "OK");
        }

        public static FeedbackBank BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Gameplay")) { AssetDatabase.CreateFolder("Assets", "Gameplay"); }
            if (!AssetDatabase.IsValidFolder(Dir)) { AssetDatabase.CreateFolder("Assets/Gameplay", "Audio"); }

            var bank = AssetDatabase.LoadAssetAtPath<FeedbackBank>(BankPath);
            if (bank == null) { bank = ScriptableObject.CreateInstance<FeedbackBank>(); AssetDatabase.CreateAsset(bank, BankPath); }

            bank.jump = Save("sfx_pulo", Jump(), gain: 0.55f);
            bank.land = Save("sfx_aterrissar", Land());
            bank.slide = Save("sfx_deslizar", Slide());
            bank.throwBall = Save("sfx_bola_de_neve", Throw());
            bank.stomp = Save("sfx_pisao", Stomp(), gain: 0.75f);
            bank.playerHurt = Save("sfx_dano", Hurt(), gain: 0.5f);
            bank.playerDie = Save("sfx_morte", Die(), gain: 0.7f);
            bank.enemyHit = Save("sfx_acerto", Hit());
            bank.enemyDie = Save("sfx_inimigo_derrotado", Pop());
            bank.monsterWindup = Save("sfx_monstro_aviso", Windup(), gain: 0.7f);
            bank.fish = Save("sfx_peixe", Coin());
            bank.rareFish = Save("sfx_peixe_dourado", RareCoin());
            bank.checkpoint = Save("sfx_checkpoint", CheckpointSfx());
            bank.mission = Save("sfx_missao", Fanfare(), gain: 0.7f);
            bank.splash = Save("sfx_agua", Splash());
            bank.iceCrack = Save("sfx_gelo_quebrando", Crack());
            bank.area = Save("sfx_novo_lugar", Chime());
            bank.dialogBlip = Save("sfx_dialogo", Blip(), gain: 0.25f);
            bank.bossRoar = Save("sfx_bsod_rugido", Roar());
            bank.bossSlam = Save("sfx_bsod_pisao", Boom());
            bank.bossFreeze = Save("sfx_bsod_travou", Glitch(), gain: 0.75f);
            bank.bossCoreHit = Save("sfx_bsod_nucleo", CoreHit());
            bank.victory = Save("jingle_vitoria", Victory());
            bank.gameOver = Save("jingle_fim_de_jogo", GameOverJingle());
            bank.musicLevel = Save("musica_montanha", MusicLevel(), music: true);
            bank.musicBoss = Save("musica_bsod", MusicBoss(), music: true);
            bank.hitEffect = AssetDatabase.LoadAssetAtPath<GameObject>(Demo + "Effects/Puff Effect Enemy.prefab");
            bank.dustEffect = AssetDatabase.LoadAssetAtPath<GameObject>(Demo + "Effects/Puff Effect.prefab");
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            return bank;
        }

        public static void BuildBatch()
        {
            var bank = BuildAll();
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            ApplyMenuMusic(bank);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorApplication.Exit(bank != null ? 0 : 1);
        }

        /// <summary>Coloca a música da montanha no menu principal também.</summary>
        public static void ApplyMenuMusic(FeedbackBank bank)
        {
            if (bank == null) { return; }
            var am = UnityEngine.Object.FindAnyObjectByType<UI.AudioManager>();
            if (am == null) { am = new GameObject("AudioManager").AddComponent<UI.AudioManager>(); }
            var so = new SerializedObject(am);
            so.FindProperty("defaultBgm").objectReferenceValue = bank.musicLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(am);
            EditorSceneManager.MarkSceneDirty(am.gameObject.scene);
        }

        // ================================================================== sons

        private static float[] Jump() { var b = Buf(0.18f); Tone(b, 0, 0.18f, 320, 760, Sine, 0.5f, 0.005f, 0.08f); return b; }
        private static float[] Land() { var b = Buf(0.2f); Noise(b, 0, 0.18f, 0.35f, 0.002f, 0.12f, 0.15f); Tone(b, 0, 0.15f, 120, 60, Sine, 0.4f, 0.002f, 0.1f); return b; }
        private static float[] Slide() { var b = Buf(0.45f); Noise(b, 0, 0.45f, 0.3f, 0.03f, 0.25f, 0.35f, sweepTo: 0.08f); return b; }
        private static float[] Throw() { var b = Buf(0.2f); Noise(b, 0, 0.2f, 0.3f, 0.01f, 0.12f, 0.5f, sweepTo: 0.15f); Tone(b, 0, 0.12f, 500, 900, Sine, 0.12f, 0.005f, 0.08f); return b; }
        private static float[] Stomp() { var b = Buf(0.32f); Tone(b, 0, 0.3f, 180, 520, Square, 0.3f, 0.002f, 0.15f, vibrato: 18); Noise(b, 0, 0.06f, 0.3f, 0.001f, 0.05f, 0.4f); return b; }
        private static float[] Hurt() { var b = Buf(0.35f); Tone(b, 0, 0.33f, 260, 140, Square, 0.35f, 0.003f, 0.12f, vibrato: 30); return b; }
        private static float[] Die() { var b = Buf(0.8f); int[] n = { 72, 67, 64, 60 }; for (int i = 0; i < n.Length; i++) { Tone(b, i * 0.15f, 0.22f, Hz(n[i]), Hz(n[i]) * 0.97f, Square, 0.25f, 0.005f, 0.1f); } return b; }
        private static float[] Hit() { var b = Buf(0.18f); Tone(b, 0, 0.16f, 170, 60, Sine, 0.6f, 0.001f, 0.1f); Noise(b, 0, 0.08f, 0.35f, 0.001f, 0.06f, 0.6f); return b; }
        private static float[] Pop() { var b = Buf(0.3f); Tone(b, 0, 0.22f, 900, 180, Sine, 0.45f, 0.001f, 0.12f); Noise(b, 0, 0.12f, 0.3f, 0.001f, 0.1f, 0.7f); return b; }
        private static float[] Windup() { var b = Buf(0.45f); Tone(b, 0, 0.45f, 110, 220, Saw, 0.18f, 0.05f, 0.08f, vibrato: 40); return b; }
        private static float[] Coin() { var b = Buf(0.32f); Bell(b, 0, Hz(83), 0.35f, 0.08f); Bell(b, 0.07f, Hz(88), 0.4f, 0.25f); return b; }
        private static float[] RareCoin() { var b = Buf(0.9f); int[] n = { 84, 88, 91, 96 }; for (int i = 0; i < n.Length; i++) { Bell(b, i * 0.08f, Hz(n[i]), 0.3f, 0.6f); } return b; }
        private static float[] CheckpointSfx() { var b = Buf(1.0f); int[] n = { 79, 83, 86, 91 }; for (int i = 0; i < n.Length; i++) { Bell(b, i * 0.06f, Hz(n[i]), 0.25f, 0.8f); } return b; }
        private static float[] Fanfare() { var b = Buf(0.9f); int[] n = { 72, 76, 79, 84 }; float[] d = { 0.1f, 0.1f, 0.1f, 0.5f }; float t = 0; for (int i = 0; i < n.Length; i++) { Tone(b, t, d[i] + 0.05f, Hz(n[i]), Hz(n[i]), SoftSquare, 0.22f, 0.005f, 0.08f); t += d[i]; } return b; }
        private static float[] Splash() { var b = Buf(0.7f); Noise(b, 0, 0.7f, 0.45f, 0.005f, 0.6f, 0.5f, sweepTo: 0.05f); Tone(b, 0, 0.2f, 300, 90, Sine, 0.3f, 0.002f, 0.15f); return b; }
        private static float[] Crack() { var b = Buf(0.45f); var r = new System.Random(7); for (int i = 0; i < 7; i++) { Noise(b, (float)r.NextDouble() * 0.25f, 0.06f, 0.4f, 0.001f, 0.05f, 0.9f); } Tone(b, 0, 0.3f, 2400, 1800, Sine, 0.08f, 0.001f, 0.25f); return b; }
        private static float[] Chime() { var b = Buf(1.4f); Noise(b, 0, 1.0f, 0.06f, 0.4f, 0.5f, 0.2f); int[] n = { 76, 81, 83, 88 }; for (int i = 0; i < n.Length; i++) { Bell(b, 0.1f + i * 0.12f, Hz(n[i]), 0.18f, 0.9f); } return b; }
        private static float[] Blip() { var b = Buf(0.04f); Tone(b, 0, 0.04f, 620, 600, SoftSquare, 0.12f, 0.002f, 0.02f); return b; }
        private static float[] Roar() { var b = Buf(1.4f); Tone(b, 0, 1.4f, 70, 52, Saw, 0.45f, 0.15f, 0.4f, vibrato: 7); Tone(b, 0, 1.4f, 105, 78, Saw, 0.25f, 0.15f, 0.4f, vibrato: 9); Noise(b, 0, 1.3f, 0.2f, 0.2f, 0.5f, 0.15f); return b; }
        private static float[] Boom() { var b = Buf(1.0f); Tone(b, 0, 0.9f, 70, 28, Sine, 0.9f, 0.002f, 0.6f); Noise(b, 0, 0.6f, 0.35f, 0.002f, 0.5f, 0.12f); return b; }
        private static float[] Glitch()
        {
            var b = Buf(0.9f); var r = new System.Random(3);
            for (int i = 0; i < 9; i++) { float f = 300 + (float)r.NextDouble() * 900; Tone(b, i * 0.07f, 0.06f, f, f, Square, 0.22f, 0.001f, 0.01f); }
            Tone(b, 0.62f, 0.25f, Hz(69), Hz(69), SoftSquare, 0.25f, 0.005f, 0.15f); Tone(b, 0.62f, 0.25f, Hz(75), Hz(75), SoftSquare, 0.18f, 0.005f, 0.15f);
            return b;
        }
        private static float[] CoreHit() { var b = Buf(0.9f); Tone(b, 0, 0.5f, 140, 40, Sine, 0.8f, 0.001f, 0.3f); Noise(b, 0, 0.3f, 0.45f, 0.001f, 0.25f, 0.6f); Bell(b, 0.05f, Hz(93), 0.25f, 0.7f); return b; }
        private static float[] Victory()
        {
            var b = Buf(2.2f); int[] n = { 72, 76, 79, 84, 79, 84, 88 }; float[] d = { 0.12f, 0.12f, 0.12f, 0.3f, 0.12f, 0.12f, 0.9f }; float t = 0;
            for (int i = 0; i < n.Length; i++) { Tone(b, t, d[i] + 0.08f, Hz(n[i]), Hz(n[i]), SoftSquare, 0.22f, 0.005f, 0.1f); Bell(b, t, Hz(n[i] + 12), 0.08f, 0.4f); t += d[i]; }
            Pad(b, 1.0f, 1.2f, new[] { 60, 64, 67, 72 }, 0.06f);
            return b;
        }
        private static float[] GameOverJingle()
        {
            var b = Buf(2.0f); int[] n = { 67, 66, 65, 64 }; float t = 0;
            for (int i = 0; i < n.Length; i++) { float d = i == 3 ? 0.9f : 0.3f; Tone(b, t, d, Hz(n[i]), Hz(n[i]) * (i == 3 ? 0.96f : 1f), SoftSquare, 0.22f, 0.01f, 0.2f, vibrato: i == 3 ? 6 : 0); t += 0.32f; }
            Pad(b, 0.9f, 1.0f, new[] { 52, 55, 59 }, 0.06f);
            return b;
        }

        // ================================================================== músicas (loops)

        private static float[] MusicLevel()
        {
            const float bpm = 96f; float beat = 60f / bpm; int bars = 8;
            var b = Buf(bars * 4 * beat);
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } }; // Am F C G
            int[] scale = { 69, 72, 74, 76, 79, 81, 84 };                                                                 // pentatônica de Lá menor
            var r = new System.Random(11);
            for (int bar = 0; bar < bars; bar++) {
                var ch = chords[(bar / 2) % 4];
                float t0 = bar * 4 * beat;
                if (bar % 2 == 0) { Pad(b, t0, 8 * beat, ch, 0.05f, wrap: true); }
                Pluck(b, t0, Hz(ch[0] - 12), 0.22f, 0.9f, wrap: true);
                Pluck(b, t0 + 2 * beat, Hz(ch[0] - 12), 0.16f, 0.8f, wrap: true);
                for (int e = 0; e < 8; e++) {
                    if (r.NextDouble() < 0.42) { Bell(b, t0 + e * beat * 0.5f, Hz(scale[r.Next(scale.Length)]), 0.07f, 0.9f, wrap: true); }
                }
            }
            return Normalize(b, 0.7f);
        }

        private static float[] MusicBoss()
        {
            const float bpm = 138f; float beat = 60f / bpm; int bars = 8;
            var b = Buf(bars * 4 * beat);
            int[] roots = { 38, 34, 36, 33 }; // Ré, Si bemol, Dó, Lá
            var r = new System.Random(5);
            for (int bar = 0; bar < bars; bar++) {
                int root = roots[(bar / 2) % 4];
                float t0 = bar * 4 * beat;
                for (int e = 0; e < 8; e++) {
                    float t = t0 + e * beat * 0.5f;
                    int note = root + (e % 4 == 3 ? 12 : 0);
                    Tone(b, t, beat * 0.45f, Hz(note), Hz(note), Saw, 0.16f, 0.004f, 0.06f, wrap: true, lowpass: 0.25f);
                }
                for (int q = 0; q < 4; q++) {
                    float t = t0 + q * beat;
                    Tone(b, t, 0.22f, 130, 42, Sine, 0.55f, 0.001f, 0.15f, wrap: true);                        // bumbo
                    if (q % 2 == 1) { Noise(b, t, 0.16f, 0.22f, 0.001f, 0.12f, 0.5f, wrap: true); Tone(b, t, 0.08f, 220, 180, Sine, 0.12f, 0.001f, 0.06f, wrap: true); } // caixa
                    Noise(b, t + beat * 0.5f, 0.04f, 0.06f, 0.001f, 0.03f, 0.95f, wrap: true);                // chimbal
                }
                if (bar % 2 == 1) {
                    int[] stab = { root + 24, root + 27, root + 31 };
                    for (int s = 0; s < 3; s++) { Tone(b, t0 + (1.5f + s) * beat, beat * 0.3f, Hz(stab[r.Next(3)]), Hz(stab[r.Next(3)]), SoftSquare, 0.07f, 0.005f, 0.05f, wrap: true); }
                }
            }
            return Normalize(b, 0.75f);
        }

        // ================================================================== síntese

        private delegate float Wave(float phase);
        private static float Sine(float p) => Mathf.Sin(p * 2f * Mathf.PI);
        private static float Square(float p) => (p % 1f) < 0.5f ? 1f : -1f;
        private static float SoftSquare(float p) => (float)Math.Tanh(Mathf.Sin(p * 2f * Mathf.PI) * 2.5f) * 0.8f;
        private static float Saw(float p) => 2f * (p % 1f) - 1f;

        private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static void Add(float[] b, int i, float v, bool wrap)
        {
            if (wrap) { b[((i % b.Length) + b.Length) % b.Length] += v; } else if (i >= 0 && i < b.Length) { b[i] += v; }
        }

        private static float Env(float t, float dur, float attack, float release)
        {
            if (t < 0f || t > dur) { return 0f; }
            float a = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float r = release > 0f ? Mathf.Clamp01((dur - t) / release) : 1f;
            return a * r;
        }

        private static void Tone(float[] b, float start, float dur, float f0, float f1, Wave wave, float amp, float attack, float release,
            float vibrato = 0f, bool wrap = false, float lowpass = 1f)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            double phase = 0; float lp = 0f;
            for (int i = 0; i < n; i++) {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(f0, f1, t / dur) * (1f + (vibrato > 0 ? Mathf.Sin(t * vibrato * 6.283f) * 0.03f : 0f));
                phase += f / Rate;
                float v = wave((float)(phase % 1.0)) * amp * Env(t, dur, attack, release);
                lp += (v - lp) * lowpass;
                Add(b, s0 + i, lp, wrap);
            }
        }

        private static void Noise(float[] b, float start, float dur, float amp, float attack, float release, float brightness,
            float sweepTo = -1f, bool wrap = false)
        {
            var r = new System.Random(Mathf.RoundToInt(start * 1000) + 17);
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            float lp = 0f;
            for (int i = 0; i < n; i++) {
                float t = i / (float)Rate;
                float k = sweepTo >= 0f ? Mathf.Lerp(brightness, sweepTo, t / dur) : brightness;
                float x = (float)(r.NextDouble() * 2 - 1);
                lp += (x - lp) * Mathf.Clamp01(k);
                Add(b, s0 + i, lp * amp * Env(t, dur, attack, release), wrap);
            }
        }

        /// <summary>Sino/sininho: senoide com harmônico inarmônico e decaimento exponencial.</summary>
        private static void Bell(float[] b, float start, float f, float amp, float dur, bool wrap = false)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < n; i++) {
                float t = i / (float)Rate;
                float e = Mathf.Exp(-t * 5f / dur) * Mathf.Clamp01(t / 0.003f);
                float v = (Mathf.Sin(t * f * 6.2832f) + 0.35f * Mathf.Sin(t * f * 2.76f * 6.2832f) * Mathf.Exp(-t * 12f)) * amp * e;
                Add(b, s0 + i, v, wrap);
            }
        }

        private static void Pluck(float[] b, float start, float f, float amp, float dur, bool wrap = false)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            for (int i = 0; i < n; i++) {
                float t = i / (float)Rate;
                float e = Mathf.Exp(-t * 4f) * Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((dur - t) / 0.05f);
                float p = t * f;
                float v = (Mathf.Sin(p * 6.2832f) * 0.8f + (2f * Mathf.Abs(2f * (p % 1f) - 1f) - 1f) * 0.3f) * amp * e;
                Add(b, s0 + i, v, wrap);
            }
        }

        private static void Pad(float[] b, float start, float dur, int[] notes, float amp, bool wrap = false)
        {
            foreach (int n in notes) {
                Tone(b, start, dur, Hz(n), Hz(n), Sine, amp, dur * 0.3f, dur * 0.35f, wrap: wrap);
                Tone(b, start, dur, Hz(n) * 1.004f, Hz(n) * 1.004f, Sine, amp * 0.6f, dur * 0.3f, dur * 0.35f, wrap: wrap);
            }
        }

        private static float[] Normalize(float[] b, float peak)
        {
            float max = 0.0001f;
            foreach (float v in b) { max = Mathf.Max(max, Mathf.Abs(v)); }
            float k = peak / max;
            for (int i = 0; i < b.Length; i++) { b[i] = (float)Math.Tanh(b[i] * k * 1.1f) * 0.95f; }
            return b;
        }

        // ================================================================== arquivo

        private static AudioClip Save(string name, float[] samples, bool music = false, float gain = 1f)
        {
            if (!music) { samples = Normalize(samples, 0.85f * gain); }
            string path = $"{Dir}/{name}.wav";
            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (music) {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
            } else {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
            }
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static void WriteWav(string path, float[] samples)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataSize);
            foreach (float s in samples) { w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f)); }
        }
    }
}
