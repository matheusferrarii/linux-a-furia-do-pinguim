using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using TSB = LinuxFuriaPinguim.EditorTools.TrailSceneryBuilder;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Parte 3 do gerador: as 4 fases.
    ///  Fase 1 Montanha (mapa original, tutorial)        — chefe BSOD
    ///  Fase 2 Vale Glacial (mapa novo)                  — chefe KERNEL PANIC (investida)
    ///  Fase 3 Ruínas Soterradas (mapa novo, estreito)   — chefe SEGFAULT (invoca monstros, chuva de gelo)
    ///  Fase 4 Pico da Montanha Ancestral (nevasca)      — chefe BSOD FINAL (todos os ataques)
    /// Cada fase: mais inimigos, monstros mais agressivos, chefes com mais núcleos, guarda mais curta e
    /// sequência de teclas mais longa.
    /// </summary>
    public static partial class GameplayBuilder
    {
        public const string Phase1Scene = "Assets/Scenes/Mountain.unity";
        public const string Phase2Scene = "Assets/Scenes/Fase2_ValeGlacial.unity";
        public const string Phase3Scene = "Assets/Scenes/Fase3_RuinasSoterradas.unity";
        public const string Phase4Scene = "Assets/Scenes/Fase4_Pico.unity";
        private const string OldPhase2Scene = "Assets/Scenes/Mountain_Fase2.unity";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";

        internal sealed class PhaseSettings
        {
            public int Number;
            public string ScenePath;
            public string NextScene = "";
            public LevelForge.MapStyle Map;            // null = usa o mapa que já está na cena (Fase 1)
            public float[] Shooters, Monsters;
            public int TutorialMonsters;
            public bool CombatTutorial;
            public int MonsterHp = 2;
            public float Aggression = 1f;
            public float ShooterFire = 2.8f, ShooterRange = 15f;
            public float[] Logs, Branches, Ice, Cracks, Icicles, RuinArches = Array.Empty<float>();
            public (string name, bool blizzard, float start)[] Areas;
            public string[] NpcOrder;                 // início, checkpoint 1, 2, 3, fim
            // chefe
            public string BossName = "BSOD";
            public Color BossColor = new Color(0.62f, 0.76f, 0.9f);
            public Color GuardColor = new Color(0.4f, 1f, 0.55f);
            public string FaceOk = "■   ■", FaceSad = ":(", FaceSadText = "Seu reino encontrou um problema";
            public float BossScale = 1f;
            public bool Slam = true, Throw = true, Charge, IceRain, Summon;
            public int BossCores = 3, BossStagger = 4, BossExtraShards, QteLength = 4;
            public float BossFreeze = 4.5f, BossSpeed = 1f, QteTime = 4.2f, GuardPeriod = 2.2f, GuardWindow = 0.8f;
            public Func<Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)>> Dialogues;
            public Func<int, int, int, List<Mission>> Missions;
        }

        internal static PhaseSettings Ph = Phase1();

        private static string SceneName(string path) => Path.GetFileNameWithoutExtension(path);

        internal static PhaseSettings Phase1() => new PhaseSettings {
            Number = 1, ScenePath = Phase1Scene, NextScene = SceneName(Phase2Scene),
            Shooters = new[] { 0.16f, 0.31f, 0.40f, 0.47f, 0.62f, 0.70f, 0.83f, 0.91f },
            Monsters = new[] { 0.09f, 0.20f, 0.28f, 0.36f, 0.44f, 0.50f, 0.57f, 0.66f, 0.73f, 0.80f, 0.87f, 0.94f },
            TutorialMonsters = 2, CombatTutorial = true,
            Logs = new[] { 0.05f, 0.14f, 0.23f, 0.31f, 0.655f, 0.715f, 0.86f, 0.93f },
            Branches = new[] { 0.10f, 0.19f, 0.28f, 0.685f, 0.75f, 0.88f },
            Ice = new[] { 0.37f, 0.44f, 0.51f, 0.58f, 0.95f },
            Cracks = new[] { 0.40f, 0.47f, 0.55f, 0.60f, 0.83f, 0.90f, 0.96f },
            Icicles = new[] { 0.64f, 0.67f, 0.70f, 0.73f, 0.76f, 0.785f },
            RuinArches = new[] { 0.63f },
            Areas = new[] { ("Floresta Congelada", false, 0f), ("Vale Glacial", false, 0.35f), ("Ruínas Soterradas", false, 0.62f), ("Pico da Montanha Ancestral", true, 0.8f) },
            NpcOrder = new[] { "anciao", "debi", "arch", "fedora", "gentoo" },
            Dialogues = Dialogues, Missions = Missions,
        };

        internal static PhaseSettings Phase2() => new PhaseSettings {
            Number = 2, ScenePath = Phase2Scene, NextScene = SceneName(Phase3Scene),
            Map = new LevelForge.MapStyle {
                Seed = 2207, Segments = 24, SegmentLength = 40f, MaxTurn = 45f, MinWidth = 15f, MaxWidth = 26f,
                WallHeight = 34f, BankWidth = 20f, HillAmplitude = 22f, TotalRise = 18f,
                SnowA = new Color(0.9f, 0.96f, 1f), SnowB = new Color(0.66f, 0.82f, 0.96f),
                LightColor = new Color(1f, 0.82f, 0.68f), LightIntensity = 0.95f, LightEuler = new Vector3(18f, -60f, 0f),
                Ambient = new Color(0.45f, 0.48f, 0.62f), FogColor = new Color(0.78f, 0.74f, 0.82f), FogDensity = 0.004f,
                SkyTint = new Color(0.75f, 0.55f, 0.6f), SkyExposure = 1.0f,
            },
            Shooters = Spread(0.06f, 0.94f, 11), Monsters = Spread(0.04f, 0.95f, 16),
            MonsterHp = 2, Aggression = 1.2f, ShooterFire = 2.2f, ShooterRange = 17f,
            Logs = Spread(0.08f, 0.9f, 5), Branches = Spread(0.12f, 0.85f, 4),
            Ice = Spread(0.15f, 0.92f, 8), Cracks = Spread(0.1f, 0.95f, 8), Icicles = new[] { 0.55f, 0.6f, 0.65f },
            Areas = new[] { ("Vale Glacial", false, 0f), ("Cachoeiras Congeladas", false, 0.5f) },
            NpcOrder = new[] { "inicio", "debi", "arch", "fedora", "anciao" },
            BossName = "KERNEL PANIC", BossColor = new Color(0.92f, 0.94f, 1f), FaceOk = "> <", FaceSad = "x_x", FaceSadText = "Kernel panic - not syncing",
            BossScale = 1.1f, Slam = true, Throw = true, Charge = true,
            BossCores = 3, BossStagger = 5, QteLength = 5, QteTime = 3.8f, BossSpeed = 1.2f, GuardPeriod = 2.0f, GuardWindow = 0.65f,
            Dialogues = DialoguesPhase2, Missions = (c, e, r) => PhaseMissions(c, e, r, "Atravesse o Vale Glacial", "Derrote o KERNEL PANIC"),
        };

        internal static PhaseSettings Phase3() => new PhaseSettings {
            Number = 3, ScenePath = Phase3Scene, NextScene = SceneName(Phase4Scene),
            Map = new LevelForge.MapStyle {
                Seed = 3109, Segments = 26, SegmentLength = 32f, MaxTurn = 70f, MinWidth = 11f, MaxWidth = 16f,
                WallHeight = 26f, BankWidth = 9f, HillAmplitude = 12f, TotalRise = 24f, StepEvery = 1.0f,
                SnowA = new Color(0.82f, 0.85f, 0.9f), SnowB = new Color(0.55f, 0.58f, 0.66f),
                LightColor = new Color(0.75f, 0.8f, 1f), LightIntensity = 0.7f, LightEuler = new Vector3(55f, 20f, 0f),
                Ambient = new Color(0.3f, 0.32f, 0.42f), FogColor = new Color(0.36f, 0.38f, 0.48f), FogDensity = 0.008f,
                SkyTint = new Color(0.35f, 0.38f, 0.5f), SkyExposure = 0.65f,
            },
            Shooters = Spread(0.05f, 0.95f, 13), Monsters = Spread(0.03f, 0.96f, 20),
            MonsterHp = 2, Aggression = 1.4f, ShooterFire = 1.9f, ShooterRange = 18f,
            Logs = Spread(0.1f, 0.9f, 4), Branches = Spread(0.06f, 0.94f, 8),
            Ice = Spread(0.2f, 0.8f, 3), Cracks = Spread(0.08f, 0.92f, 6), Icicles = Spread(0.04f, 0.96f, 14),
            RuinArches = Spread(0.1f, 0.9f, 6),
            Areas = new[] { ("Ruínas Soterradas", false, 0f), ("Salões Esquecidos", false, 0.5f) },
            NpcOrder = new[] { "inicio", "arch", "debi", "gentoo", "anciao" },
            BossName = "SEGFAULT", BossColor = new Color(0.55f, 0.45f, 0.7f), FaceOk = "0x0", FaceSad = "!!", FaceSadText = "Segmentation fault (core dumped)",
            BossScale = 1.15f, Slam = true, Throw = true, Charge = false, IceRain = true, Summon = true,
            BossCores = 4, BossStagger = 5, QteLength = 5, QteTime = 3.4f, BossSpeed = 1.25f, GuardPeriod = 1.9f, GuardWindow = 0.6f, BossExtraShards = 1,
            Dialogues = DialoguesPhase3, Missions = (c, e, r) => PhaseMissions(c, e, r, "Encontre a saída das Ruínas", "Derrote o SEGFAULT"),
        };

        internal static PhaseSettings Phase4() => new PhaseSettings {
            Number = 4, ScenePath = Phase4Scene, NextScene = "",
            Map = new LevelForge.MapStyle {
                Seed = 4421, Segments = 26, SegmentLength = 36f, MaxTurn = 55f, MinWidth = 12f, MaxWidth = 18f,
                WallHeight = 30f, BankWidth = 14f, HillAmplitude = 30f, TotalRise = 110f,
                SnowA = new Color(0.97f, 0.98f, 1f), SnowB = new Color(0.8f, 0.86f, 0.95f),
                LightColor = new Color(0.85f, 0.9f, 1f), LightIntensity = 0.8f, LightEuler = new Vector3(40f, 120f, 0f),
                Ambient = new Color(0.5f, 0.55f, 0.65f), FogColor = new Color(0.82f, 0.87f, 0.94f), FogDensity = 0.006f,
                SkyTint = new Color(0.6f, 0.65f, 0.75f), SkyExposure = 0.85f,
            },
            Shooters = Spread(0.04f, 0.95f, 15), Monsters = Spread(0.03f, 0.96f, 22),
            MonsterHp = 3, Aggression = 1.55f, ShooterFire = 1.7f, ShooterRange = 19f,
            Logs = Spread(0.06f, 0.94f, 7), Branches = Spread(0.1f, 0.9f, 6),
            Ice = Spread(0.08f, 0.92f, 6), Cracks = Spread(0.05f, 0.95f, 9), Icicles = Spread(0.1f, 0.9f, 8),
            Areas = new[] { ("Encosta do Pico", true, 0f), ("Pico da Montanha Ancestral", true, 0.55f) },
            NpcOrder = new[] { "inicio", "fedora", "debi", "arch", "gentoo" },
            BossName = "BSOD FINAL", BossColor = new Color(0.45f, 0.62f, 0.95f), FaceOk = "■   ■", FaceSad = ":(", FaceSadText = "Seu reino encontrou um problema",
            BossScale = 1.3f, Slam = true, Throw = true, Charge = true, IceRain = true, Summon = true,
            BossCores = 5, BossStagger = 6, QteLength = 6, QteTime = 3.2f, BossSpeed = 1.35f, GuardPeriod = 1.7f, GuardWindow = 0.55f, BossExtraShards = 2,
            Dialogues = DialoguesPhase4, Missions = (c, e, r) => PhaseMissions(c, e, r, "Suba até o Pico da Montanha Ancestral", "Desligue o BSOD de vez"),
        };

        private static float[] Spread(float from, float to, int count)
        {
            var r = new float[count];
            for (int i = 0; i < count; i++) { r[i] = Mathf.Lerp(from, to, count == 1 ? 0.5f : i / (float)(count - 1)); }
            return r;
        }

        // ------------------------------------------------------------------ missões das fases novas

        private static List<Mission> PhaseMissions(int coins, int enemies, int rare, string goalTitle, string bossTitle) => new List<Mission> {
            new Mission { id = "falar_inicio", title = "Ouça o aviso no começo da trilha", type = MissionType.TalkTo, key = "inicio", activeAtStart = true, main = true, bonusCoins = 0 },
            new Mission { id = "topo", title = goalTitle, type = MissionType.ReachGoal, main = true, bonusCoins = 0 },
            new Mission { id = "peixes", title = $"Colete {coins} peixes", type = MissionType.CollectCoins, target = coins, bonusCoins = 25 },
            new Mission { id = "capangas", title = $"Derrote {enemies} capangas", type = MissionType.DefeatEnemies, target = enemies, bonusCoins = 20 },
            new Mission { id = "checkpoints", title = "Ative os 3 checkpoints", type = MissionType.ReachCheckpoint, target = 3, bonusCoins = 10 },
            new Mission { id = "dourados", title = $"Encontre os {rare} peixes dourados", type = MissionType.CollectRare, target = Mathf.Max(1, rare), bonusCoins = 30 },
            new Mission { id = "boss", title = bossTitle, type = MissionType.DefeatBoss, main = true, bonusCoins = 80 },
        };

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> Phase(
            (string id, string name, string[] lines, string repeat, string[] missions)[] npcs)
        {
            var d = new Dictionary<string, (string, DialogueLine[], DialogueLine[], string[], float)>();
            for (int i = 0; i < npcs.Length; i++) {
                var n = npcs[i];
                // o NPC do começo da trilha usa a chave "inicio" (missão "Ouça o aviso")
                d[n.id] = (n.name, n.lines.Select(L).ToArray(), new[] { L(n.repeat) }, n.missions, n.id == "anciao" || n.id == "gentoo" ? 1.12f : 1f);
            }
            return d;
        }

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> DialoguesPhase2() => Phase(new[] {
            ("gentoo", "Sábio Gentoo", new[] {
                "Linux, o BSOD não foi desligado: ele REINICIOU e mandou um guarda à frente.",
                "No fim deste vale está o KERNEL PANIC, uma fera que se joga em investidas. Quando ele se agachar, saia da frente!",
                "Ele só fica vulnerável por instantes: espere a mira ficar VERDE para acertar. Fora disso ele defende.",
            }, "Mira verde = ataque. Mira cinza = ele defende.", new[] { "topo", "peixes", "capangas" }),
            ("debi", "Batedora Debi", new[] {
                "O vale está coberto de gelo e de fendas. No gelo, frear demora: pule com antecedência.",
                "E os monstros daqui atacam rápido. Quando tremerem, já desvie!",
            }, "Gelo: freie cedo e pule as fendas!", new[] { "checkpoints" }),
            ("arch", "Construtora Arch", new[] {
                "Quatro peixes dourados se perderam pelo vale. Cada um recupera um coração.",
            }, "Procure os dourados nos cantos!", new[] { "dourados" }),
            ("fedora", "Guardiã Fedora", new[] {
                "Quando o chefe TRAVAR, aparece uma sequência de teclas. Digite rápido e sem errar: é o golpe decisivo!",
            }, "Chefe travou? Digite a sequência!", Array.Empty<string>()),
            ("anciao", "Ancião Tux", new[] {
                "O KERNEL PANIC está logo além do portal. Paciência: só ataque com a guarda aberta.",
            }, "Mira verde, golpe. Travou, sequência!", Array.Empty<string>()),
        }).Rekey("gentoo");

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> DialoguesPhase3() => Phase(new[] {
            ("fedora", "Guardiã Fedora", new[] {
                "Bem-vindo às Ruínas Soterradas. Os corredores são estreitos e o teto range: olhe as sombras no chão.",
                "No fundo das ruínas vive o SEGFAULT. Ele invoca Monstros de Gelo e faz chover gelo. Não fique parado!",
            }, "Sombra no chão = gelo caindo!", new[] { "topo", "peixes", "capangas" }),
            ("arch", "Construtora Arch", new[] {
                "Estes arcos eram de um povo antigo de pinguins. O BSOD soterrou tudo num só formato.",
                "Os peixes dourados caíram entre os escombros. Explore!",
            }, "Dourados nos cantos das ruínas!", new[] { "dourados" }),
            ("debi", "Batedora Debi", new[] {
                "Os capangas aqui atiram muito rápido. Use os pilares para se proteger e revide.",
            }, "Ande de lado e revide!", new[] { "checkpoints" }),
            ("gentoo", "Sábio Gentoo", new[] {
                "A sequência do SEGFAULT é mais longa e o tempo é menor. Respire e digite com calma... mas rápido!",
            }, "Sequência longa: atenção!", Array.Empty<string>()),
            ("anciao", "Ancião Tux", new[] {
                "Depois do SEGFAULT só resta o Pico. Vença e o caminho para o BSOD estará aberto.",
            }, "Vença o SEGFAULT!", Array.Empty<string>()),
        }).Rekey("fedora");

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> DialoguesPhase4() => Phase(new[] {
            ("anciao", "Ancião Tux", new[] {
                "Chegamos ao Pico da Montanha Ancestral. A nevasca não para e o BSOD espera no topo.",
                "Ele usa TUDO: pisão, estilhaços, investida, chuva de gelo e monstros. Cinco núcleos.",
                "Se ele vencer, o reino trava para sempre. Vá, Linux!",
            }, "Suba até o topo!", new[] { "topo", "peixes", "capangas" }),
            ("fedora", "Guardiã Fedora", new[] { "A subida é longa e os monstros daqui são os mais ferozes. Ative os checkpoints!" }, "Checkpoints salvam vidas!", new[] { "checkpoints" }),
            ("debi", "Batedora Debi", new[] { "Os últimos peixes dourados estão escondidos na encosta. Você vai precisar dos corações." }, "Corações extras: dourados!", new[] { "dourados" }),
            ("arch", "Construtora Arch", new[] { "A guarda do BSOD FINAL abre por muito pouco tempo. Olhe a mira, não o chefe." }, "Olhe a mira!", Array.Empty<string>()),
            ("gentoo", "Sábio Gentoo", new[] {
                "É o fim da jornada. Seis teclas por sequência, e quase sem tempo.",
                "Desligue o BSOD de uma vez por todas. O Reino de Kernel conta com você!",
            }, "Desligue o BSOD!", Array.Empty<string>()),
        }).Rekey("anciao");

        // ------------------------------------------------------------------ batch: todas as fases

        public static void BuildAllPhasesBatch()
        {
            string outDir = GetArg("-outDir") ?? "GameplayOut";
            Directory.CreateDirectory(outDir);
            int code = 0;
            var fullLog = new System.Text.StringBuilder();
            try {
                AudioSynthBuilder.BuildAll();
                fullLog.AppendLine("Áudio gerado.");

                string only = GetArg("-phases");
                foreach (var phase in new[] { Phase1(), Phase2(), Phase3(), Phase4() }) {
                    if (only != null && !only.Contains(phase.Number.ToString())) { continue; }
                    Ph = phase;
                    fullLog.AppendLine($"=== Fase {phase.Number}");
                    if (phase.Map != null) {
                        var mlog = new System.Text.StringBuilder();
                        if (!LevelForge.BuildMap(Phase1Scene, phase.ScenePath, SceneName(phase.ScenePath), phase.Map, mlog)) { code = 3; }
                        fullLog.Append(mlog);
                        if (!TSB.Generate(null)) { code = 4; fullLog.Append(TSB.s_Log); continue; }
                        fullLog.AppendLine("Cenário: " + TSB.s_Log.ToString().Split('\n').LastOrDefault(l => l.Contains("floresta")));
                    } else {
                        EditorSceneManager.OpenScene(phase.ScenePath);
                    }
                    if (!Generate()) { code = 2; }
                    fullLog.Append(TSB.s_Log);
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    string shots = Path.Combine(outDir, "fase" + phase.Number);
                    Directory.CreateDirectory(shots);
                    Capture(shots);
                    AddToBuildSettings(phase.ScenePath);
                }

                // remove a "Fase 2" antiga (clone da Fase 1)
                RemoveFromBuildSettings(OldPhase2Scene);
                AssetDatabase.DeleteAsset(OldPhase2Scene);
                AssetDatabase.DeleteAsset(MatDir + "/CeuNoturno.mat");
                Ph = Phase1();
            } catch (Exception e) {
                fullLog.AppendLine("EXCEPTION: " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), fullLog.ToString());
            EditorApplication.Exit(code);
        }

        [MenuItem("Tools/Furia Pinguim/Fase/Gerar Fases 2, 3 e 4 (mapas novos)")]
        public static void GenerateNewPhasesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            var log = new System.Text.StringBuilder();
            bool ok = true;
            foreach (var phase in new[] { Phase2(), Phase3(), Phase4() }) {
                Ph = phase;
                ok &= LevelForge.BuildMap(Phase1Scene, phase.ScenePath, SceneName(phase.ScenePath), phase.Map, log);
                ok &= TSB.Generate(null);
                ok &= Generate();
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                AddToBuildSettings(phase.ScenePath);
            }
            Ph = Phase1();
            Debug.Log(log.ToString());
            EditorUtility.DisplayDialog("Fases", ok ? "Fases 2, 3 e 4 geradas." : "Alguma fase falhou. Veja o Console.", "OK");
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) { return; }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void RemoveFromBuildSettings(string path)
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToArray();
        }
    }

    internal static class DialogueDictExtensions
    {
        /// <summary>O NPC do começo responde pela chave "inicio" (missão "Ouça o aviso").</summary>
        public static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> Rekey(
            this Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> d, string firstNpc)
        {
            d["inicio"] = d[firstNpc];
            return d;
        }
    }
}
