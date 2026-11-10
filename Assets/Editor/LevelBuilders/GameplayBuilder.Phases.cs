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
    /// Parte 3 do gerador: as fases. Fase 1 (dia, subida, com tutorial) e Fase 2 "Reinicialização"
    /// (noite, descida, sem tutorial, mais inimigos e mais agressivos, BSOD com 4 núcleos).
    /// </summary>
    public static partial class GameplayBuilder
    {
        public const string Phase1Scene = "Assets/Scenes/Mountain.unity";
        public const string Phase2Scene = "Assets/Scenes/Mountain_Fase2.unity";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";

        internal sealed class PhaseSettings
        {
            public int Number;
            public string NextScene = "";
            public float[] Shooters, Monsters;
            public int TutorialMonsters;
            public bool CombatTutorial;
            public int MonsterHp = 2;
            public float Aggression = 1f;
            public float ShooterFire = 2.8f, ShooterRange = 15f;
            public float[] Logs, Branches, Ice, Cracks, Icicles;
            public (string name, bool blizzard, float start)[] Areas;
            public string[] NpcOrder;                 // início, checkpoint 1, 2, 3, fim
            public int BossCores = 3, BossStagger = 4, BossExtraShards;
            public float BossFreeze = 4.5f, BossSpeed = 1f;
            public bool Night;
            public Func<Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)>> Dialogues;
            public Func<int, int, int, List<Mission>> Missions;
        }

        internal static PhaseSettings Ph = Phase1();

        internal static PhaseSettings Phase1() => new PhaseSettings {
            Number = 1,
            NextScene = Path.GetFileNameWithoutExtension(Phase2Scene),
            Shooters = new[] { 0.16f, 0.31f, 0.40f, 0.47f, 0.62f, 0.70f, 0.83f, 0.91f },
            Monsters = new[] { 0.09f, 0.20f, 0.28f, 0.36f, 0.44f, 0.50f, 0.57f, 0.66f, 0.73f, 0.80f, 0.87f, 0.94f },
            TutorialMonsters = 2, CombatTutorial = true,
            Logs = new[] { 0.05f, 0.14f, 0.23f, 0.31f, 0.655f, 0.715f, 0.86f, 0.93f },
            Branches = new[] { 0.10f, 0.19f, 0.28f, 0.685f, 0.75f, 0.88f },
            Ice = new[] { 0.37f, 0.44f, 0.51f, 0.58f, 0.95f },
            Cracks = new[] { 0.40f, 0.47f, 0.55f, 0.60f, 0.83f, 0.90f, 0.96f },
            Icicles = new[] { 0.64f, 0.67f, 0.70f, 0.73f, 0.76f, 0.785f },
            Areas = new[] { ("Floresta Congelada", false, 0f), ("Vale Glacial", false, 0.35f), ("Ruínas Soterradas", false, 0.62f), ("Pico da Montanha Ancestral", true, 0.8f) },
            NpcOrder = new[] { "anciao", "debi", "arch", "fedora", "gentoo" },
            Dialogues = Dialogues, Missions = Missions,
        };

        /// <summary>Fase 2: mesma montanha, de noite e de cima para baixo. Tudo mais difícil.</summary>
        internal static PhaseSettings Phase2() => new PhaseSettings {
            Number = 2,
            NextScene = "",
            Shooters = new[] { 0.07f, 0.15f, 0.22f, 0.29f, 0.35f, 0.42f, 0.49f, 0.56f, 0.63f, 0.70f, 0.77f, 0.84f, 0.90f, 0.95f },
            Monsters = new[] { 0.05f, 0.10f, 0.14f, 0.19f, 0.24f, 0.28f, 0.33f, 0.38f, 0.43f, 0.47f, 0.52f, 0.58f, 0.62f, 0.67f, 0.73f, 0.80f, 0.86f, 0.93f },
            TutorialMonsters = 0, CombatTutorial = false,
            MonsterHp = 2, Aggression = 1.45f,
            ShooterFire = 1.9f, ShooterRange = 18f,
            Logs = new[] { 0.06f, 0.12f, 0.17f, 0.67f, 0.71f, 0.76f, 0.81f, 0.86f, 0.91f, 0.96f },
            Branches = new[] { 0.09f, 0.25f, 0.69f, 0.74f, 0.79f, 0.84f, 0.89f, 0.94f },
            Ice = new[] { 0.03f, 0.40f, 0.45f, 0.50f, 0.55f, 0.60f },
            Cracks = new[] { 0.04f, 0.14f, 0.41f, 0.44f, 0.48f, 0.52f, 0.56f, 0.59f, 0.63f, 0.78f },
            Icicles = new[] { 0.21f, 0.23f, 0.26f, 0.28f, 0.30f, 0.32f, 0.34f, 0.36f, 0.18f, 0.11f },
            Areas = new[] { ("Pico da Montanha Ancestral", true, 0f), ("Ruínas Soterradas", false, 0.2f), ("Vale Glacial", false, 0.38f), ("Floresta Congelada", false, 0.65f) },
            NpcOrder = new[] { "gentoo", "fedora", "arch", "debi", "anciao" },
            BossCores = 4, BossStagger = 6, BossFreeze = 3.2f, BossSpeed = 1.3f, BossExtraShards = 2,
            Night = true,
            Dialogues = DialoguesPhase2, Missions = MissionsPhase2,
        };

        // ------------------------------------------------------------------ fase 2: conteúdo

        private static List<Mission> MissionsPhase2(int coins, int enemies, int rare) => new List<Mission> {
            new Mission { id = "falar_gentoo", title = "Fale com o Sábio Gentoo", type = MissionType.TalkTo, key = "gentoo", activeAtStart = true, main = true, bonusCoins = 0 },
            new Mission { id = "vila", title = "Desça a montanha até a vila", type = MissionType.ReachGoal, main = true, bonusCoins = 0 },
            new Mission { id = "peixes", title = $"Colete {coins} peixes", type = MissionType.CollectCoins, target = coins, bonusCoins = 25 },
            new Mission { id = "capangas", title = $"Derrote {enemies} capangas do BSOD", type = MissionType.DefeatEnemies, target = enemies, bonusCoins = 20 },
            new Mission { id = "checkpoints", title = "Ative os 3 checkpoints", type = MissionType.ReachCheckpoint, target = 3, bonusCoins = 10 },
            new Mission { id = "dourados", title = $"Encontre os {rare} peixes dourados", type = MissionType.CollectRare, target = Mathf.Max(1, rare), bonusCoins = 30 },
            new Mission { id = "falar_anciao", title = "Encontre o Ancião Tux na vila", type = MissionType.TalkTo, key = "anciao", bonusCoins = 10 },
            new Mission { id = "boss", title = "Desligue o BSOD reiniciado", type = MissionType.DefeatBoss, main = true, bonusCoins = 80 },
        };

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> DialoguesPhase2() =>
            new Dictionary<string, (string, DialogueLine[], DialogueLine[], string[], float)> {
                ["gentoo"] = ("Sábio Gentoo", new[] {
                    L("Linux! Você derrubou o BSOD... mas ele não foi desligado. Ele REINICIOU."),
                    L("Durante a noite o núcleo dele desceu a montanha. Está indo direto para a vila."),
                    L("E voltou pior: mais capangas, Monstros de Gelo mais ferozes e o próprio BSOD mais resistente."),
                    L("Desça a montanha sem parar. Se você hesitar, a vila trava para sempre!"),
                }, new[] { L("Desça até a vila! Não pare!") },
                new[] { "vila", "peixes", "capangas" }, 1.1f),

                ["fedora"] = ("Guardiã Fedora", new[] {
                    L("Cuidado, Linux! Desde que ele passou, as estalactites das ruínas não param de cair."),
                    L("E os capangas agora atiram mais rápido e de mais longe. Use a mira e não fique parado na frente deles."),
                    L("Ative os checkpoints: hoje você vai precisar."),
                }, new[] { L("Sombras no chão = gelo caindo. Capangas: ande de lado e revide.") },
                new[] { "checkpoints" }, 1f),

                ["arch"] = ("Construtora Arch", new[] {
                    L("O vale virou um rinque! Gelo para todo lado e as fendas estão mais largas."),
                    L("Os peixes dourados se esconderam de novo nos cantos. Ache todos: cada um recupera um coração."),
                }, new[] { L("No gelo, frear demora: pule as fendas com antecedência!") },
                new[] { "dourados" }, 1f),

                ["debi"] = ("Batedora Debi", new[] {
                    L("Os Monstros de Gelo estão mais rápidos: o aviso antes do bote ficou bem curto."),
                    L("Dica de batedora: acerte uma bola de neve enquanto ele treme e o bote é cancelado."),
                    L("O Ancião está na vila, logo à frente. Corre!"),
                }, new[] { L("Acerte o monstro enquanto ele treme para cancelar o bote!") },
                new[] { "falar_anciao" }, 1f),

                ["anciao"] = ("Ancião Tux", new[] {
                    L("Linux! O BSOD reiniciado está logo além daquele portal, cercando a vila."),
                    L("Ele está mais resistente: são quatro núcleos, e ele demora mais para travar."),
                    L("E fica travado por menos tempo. Quando a tela azul aparecer, não desperdice a chance."),
                    L("É agora ou nunca. Desligue esse BSOD de uma vez por todas!"),
                }, new[] { L("Quatro núcleos. Encha a barra, espere travar e acerte!") },
                new[] { "boss" }, 1.15f),
            };

        // ------------------------------------------------------------------ batch: todas as fases

        /// <summary>
        /// Unity -batchmode -executeMethod LinuxFuriaPinguim.EditorTools.GameplayBuilder.BuildAllPhasesBatch -outDir pasta
        /// Gera os sons, a Fase 1, cria a Fase 2 (cópia noturna e invertida) e coloca a música no menu.
        /// </summary>
        public static void BuildAllPhasesBatch()
        {
            string outDir = GetArg("-outDir") ?? "GameplayOut";
            Directory.CreateDirectory(outDir);
            int code = 0;
            var fullLog = new System.Text.StringBuilder();
            try {
                var bank = AudioSynthBuilder.BuildAll();
                fullLog.AppendLine("Áudio gerado.");

                // Fase 1
                EditorSceneManager.OpenScene(Phase1Scene);
                Ph = Phase1();
                if (!Generate()) { code = 2; }
                fullLog.Append(TSB.s_Log);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Directory.CreateDirectory(Path.Combine(outDir, "fase1"));
                Capture(Path.Combine(outDir, "fase1"));

                // Fase 2 (cópia da 1)
                AssetDatabase.DeleteAsset(Phase2Scene);
                AssetDatabase.CopyAsset(Phase1Scene, Phase2Scene);
                EditorSceneManager.OpenScene(Phase2Scene);
                Ph = Phase2();
                ApplyNight();
                if (!MovePlayerToPathEnd()) { code = 3; }
                if (!Generate()) { code = 2; }
                fullLog.AppendLine("--- Fase 2"); fullLog.Append(TSB.s_Log);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Directory.CreateDirectory(Path.Combine(outDir, "fase2"));
                Capture(Path.Combine(outDir, "fase2"));

                // Menu: música
                EditorSceneManager.OpenScene(MenuScene);
                AudioSynthBuilder.ApplyMenuMusic(bank);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

                AddToBuildSettings(Phase2Scene);
                Ph = Phase1();
            } catch (Exception e) {
                fullLog.AppendLine("EXCEPTION: " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), fullLog.ToString());
            EditorApplication.Exit(code);
        }

        [MenuItem("Tools/Furia Pinguim/Fase/Gerar Fase 2 (noite, descida, difícil)")]
        public static void GeneratePhase2Menu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            AssetDatabase.DeleteAsset(Phase2Scene);
            AssetDatabase.CopyAsset(Phase1Scene, Phase2Scene);
            EditorSceneManager.OpenScene(Phase2Scene);
            Ph = Phase2();
            ApplyNight();
            bool ok = MovePlayerToPathEnd() && Generate();
            Ph = Phase1();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AddToBuildSettings(Phase2Scene);
            EditorUtility.DisplayDialog("Fase 2", ok ? "Fase 2 gerada em " + Phase2Scene : "Falhou. Veja o Console.", "OK");
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) { return; }
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ fase 2: noite e início invertido

        private static void ApplyNight()
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) {
                if (light.type != LightType.Directional) { continue; }
                light.intensity = 0.45f;
                light.color = new Color(0.55f, 0.65f, 1f);
                light.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.2f, 0.32f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.08f, 0.11f, 0.2f);

            string path = MatDir + "/CeuNoturno.mat";
            if (!AssetDatabase.IsValidFolder(MatDir)) { BuildMaterials(); }
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, path); }
            sky.SetColor("_SkyTint", new Color(0.2f, 0.25f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.08f, 0.1f, 0.16f));
            sky.SetFloat("_AtmosphereThickness", 0.45f);
            sky.SetFloat("_Exposure", 0.32f);
            sky.SetFloat("_SunSize", 0.025f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        /// <summary>Fase 2 começa no fim da Fase 1 (cume) e desce: põe o pinguim lá, virado para a descida.</summary>
        private static bool MovePlayerToPathEnd()
        {
            if (!TSB.PrepareWalkMap()) { return false; }
            BuildCenterline();
            int end = Mathf.Max(0, s_MaxStep - 12);
            Vector3 pos = Point(end);
            Vector3 down = -Dir(end);
            var player = Object.FindAnyObjectByType<PenguinCharacter>();
            if (player == null) { return false; }
            player.transform.SetPositionAndRotation(pos + Vector3.up * 0.2f, Quaternion.LookRotation(down));
            var cam = Object.FindAnyObjectByType<CameraFollow>();
            if (cam != null) {
                var rot = Quaternion.LookRotation(down) * Quaternion.Euler(14f, 0f, 0f);
                cam.transform.SetPositionAndRotation(pos + Vector3.up * 2.6f - rot * Vector3.forward * 10f, rot);
            }
            EditorUtility.SetDirty(player.transform);
            Physics.SyncTransforms();
            return true;
        }
    }
}
