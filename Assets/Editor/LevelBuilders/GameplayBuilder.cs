using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dypsloom.DypThePenguin.Scripts.Character;
using Dypsloom.DypThePenguin.Scripts.Damage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using TSB = LinuxFuriaPinguim.EditorTools.TrailSceneryBuilder;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Monta o ciclo jogável da fase Mountain ao longo do trajeto:
    /// peixes (moedas), peixes dourados (curam), checkpoints, portal final, capangas do BSOD,
    /// Monstros de Gelo, NPCs com diálogo e missões. Tudo fica em "_Gameplay".
    /// </summary>
    public static partial class GameplayBuilder
    {
        private const string ScenePath = "Assets/Scenes/Mountain.unity";
        private const string RootName = "_Gameplay";
        private const string MatDir = "Assets/Gameplay/Materials";

        private const string Demo = "Assets/Dypsloom/DypThePenguin/Demo/";
        private const string FishModel = Demo + "Models/M_Fish_01.fbx";
        private const string RareFishModel = Demo + "Models/M_Fish_02.fbx";
        private const string CrystalModel = Demo + "Models/M_Crystal.fbx";
        private const string SnowballModel = Demo + "Models/M_Snow_ball.fbx";
        private const string PenguinModel = Demo + "Characters/Models/Dyp.fbx";
        private const string PenguinAnimator = Demo + "Characters/Animations/Character Animator.controller";
        private const string NicePenguinMat = Demo + "Material/Mat_nice_penguin.mat";
        private const string CrystalMat = Demo + "Material/Mat_Crystal.mat";
        private const string SnowballMat = Demo + "Material/Mat_SnowBall.mat";
        private const string ShooterPrefab = Demo + "Prefabs/Enemy Shooter.prefab";
        private const string PlayerSnowball = Demo + "Environment/Projectiles/Player SnowBall.prefab";
        private const string EnemyPuff = Demo + "Effects/Puff Effect Enemy.prefab";
        private const string IceCircle = "Assets/GeeZyyGames/Winter/prefab/ice circle.prefab";
        private const string TitleFont = "Assets/UI/Fonts/Bangers.ttf";
        private const string BodyFont = "Assets/UI/Fonts/Roboto-Bold.ttf";
        private const string PanelSprite = "Assets/UI/Sprites/Buttons/Panel_Large_Icy.png";
        private const string ButtonSprite = "Assets/UI/Sprites/Buttons/Panel_Blank_1.png";

        // caminho
        private static int[] s_Center;       // célula central por "passo" do caminho
        private static float[] s_WallDist;   // distância até a borda (dentro do caminho)
        private static int[] s_FromCenter;   // passos até a linha central
        private static int s_MaxStep;
        private static float P;              // altura do pinguim
        private static float s_ModelHeight;  // altura do modelo do pinguim (para NPCs/inimigos)
        private static Transform s_Root;
        private static readonly List<(string name, int k)> s_Shots = new List<(string, int)>();

        // ------------------------------------------------------------------ menus / batch

        [MenuItem("Tools/Furia Pinguim/Fase/Gerar gameplay (peixes, inimigos, NPCs, missões)")]
        public static void GenerateMenu()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
                EditorSceneManager.OpenScene(ScenePath);
            }
            bool ok = Generate();
            Debug.Log(TSB.s_Log.ToString());
            if (ok) { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); }
            EditorUtility.DisplayDialog("Gameplay", ok ? "Gameplay gerada. Salve a cena (Ctrl+S)." : "Falhou. Veja o Console.", "OK");
        }

        [MenuItem("Tools/Furia Pinguim/Fase/Remover gameplay gerada")]
        public static void ClearMenu()
        {
            Clear();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void GenerateBatch()
        {
            string outDir = GetArg("-outDir") ?? "GameplayOut";
            Directory.CreateDirectory(outDir);
            int code = 0;
            try {
                EditorSceneManager.OpenScene(ScenePath);
                if (Generate()) {
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    Capture(outDir);
                } else { code = 2; }
            } catch (Exception e) {
                TSB.s_Log ??= new System.Text.StringBuilder();
                TSB.s_Log.AppendLine("EXCEPTION: " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), TSB.s_Log?.ToString() ?? "");
            EditorApplication.Exit(code);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        public static void Clear()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) {
                if (go.name == RootName) { Undo.DestroyObjectImmediate(go); }
            }
        }

        // ------------------------------------------------------------------ geração

        public static bool Generate()
        {
            Clear();
            if (!TSB.PrepareWalkMap()) { return false; }
            var log = TSB.s_Log;
            var rng = new System.Random(325);
            s_Shots.Clear();
            s_ExtraShots.Clear();

            var player = Object.FindAnyObjectByType<PenguinCharacter>();
            if (player == null) { log.AppendLine("PenguinCharacter não encontrado."); return false; }
            P = TSB.s_PenguinHeight;
            s_ModelHeight = Height(player.GetComponentsInChildren<SkinnedMeshRenderer>(), P * 0.8f);

            BuildCenterline();
            log.AppendLine($"Caminho: {s_MaxStep} passos. Altura do modelo do pinguim {s_ModelHeight:F2}");

            var rootGo = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootGo, "Gerar gameplay");
            s_Root = rootGo.transform;
            Transform fishRoot = Group("Peixes"), enemyRoot = Group("Inimigos"), npcRoot = Group("NPCs"), cpRoot = Group("Checkpoints");

            var mats = BuildMaterials();

            // --- marcos do caminho (em "passos" = unidades ao longo do trajeto)
            int K(float f) => Mathf.Clamp(Mathf.RoundToInt(f * s_MaxStep), 2, s_MaxStep - 2);
            int[] checkpoints = { K(0.26f), K(0.52f), K(0.76f) };
            int goalK = s_MaxStep - 4;
            var o = Ph.NpcOrder;
            var npcSpots = new (string id, int k)[] {
                (o[0], Mathf.Min(14, s_MaxStep / 20)), (o[1], checkpoints[0] + 7), (o[2], checkpoints[1] + 7),
                (o[3], checkpoints[2] + 7), (o[4], goalK - 16),
            };

            // --- checkpoints e portal
            for (int i = 0; i < checkpoints.Length; i++) { CreateCheckpoint(cpRoot, i + 1, checkpoints[i], mats); }
            var extra = BuildExtraMaterials();
            BuildBossSection(goalK, mats, extra);

            // --- NPCs
            var dialogues = Ph.Dialogues();
            int side = 1;
            bool firstNpc = true;
            foreach (var (id, k) in npcSpots) {
                var d = dialogues[id];
                // O primeiro (Ancião Tux / quem abre a fase) fica à frente do jogador no nascimento, de frente para ele.
                Vector3? at = firstNpc ? InFrontOfPlayer(player.transform, side) : null;
                if (firstNpc) { log.AppendLine(at.HasValue ? $"NPC inicial à frente do jogador em {at.Value}" : "NPC inicial: sem espaço à frente, usando o caminho."); }
                CreateNpc(npcRoot, id, d.name, k, side, d.first, d.repeat, d.missions, d.scale, at, firstNpc ? player.transform.position : (Vector3?)null);
                side = -side;
                firstNpc = false;
            }
            s_ExtraShots.Add(("inicio_visao_do_jogador", player.transform.position, player.transform.eulerAngles.y));

            // --- inimigos
            var shooterK = Ph.Shooters.Select(K).ToArray();
            var monsterK = Ph.Monsters.Select(K).ToArray();
            int enemies = 0;
            side = 1;
            foreach (int k in shooterK) { if (CreateShooter(enemyRoot, k, side)) { enemies++; } side = -side; }
            for (int i = 0; i < monsterK.Length; i++) {
                bool tutorial = i < Ph.TutorialMonsters; // fase 1: os dois primeiros ensinam a lutar
                if (CreateIceMonster(enemyRoot, monsterK[i], mats, tutorial)) { enemies++; }
            }
            // Mini tutorial de combate (só na fase 1): dicas antes do primeiro monstro e do primeiro capanga
            if (Ph.CombatTutorial) {
            CombatHint(enemyRoot, monsterK[0] - 10, "tut_monstro",
                "Monstro de Gelo! Clique ou F: bola de neve — a mira marca o inimigo sozinha");
            CombatHint(enemyRoot, monsterK[0] - 3, "tut_pisao",
                "Pule na cabeça dele ou deslize com Ctrl. Segure o botão direito para travar a câmera");
            CombatHint(enemyRoot, monsterK[2] - 8, "tut_bote",
                "Monstros grandes avisam antes do bote: quando tremerem, saia da frente e revide!");
            CombatHint(enemyRoot, shooterK[0] - 10, "tut_capanga",
                "Capanga atirador! Ande de lado para desviar e revide com bolas de neve");
            }

            // --- peixes pelo caminho (evita checkpoints, NPCs e o portal)
            var avoid = checkpoints.Concat(npcSpots.Select(n => n.k)).Append(goalK).ToList();
            int coins = 0;
            for (int k = 18; k < goalK - 8; k += 5) {
                if (avoid.Any(a => Mathf.Abs(a - k) < 7)) { continue; }
                Vector3 c = Point(k);
                float amp = Mathf.Clamp(WallDist(k) - 2.5f, 0f, 2.6f);
                Vector3 pos = c + Perp(k) * (Mathf.Sin(k * 0.06f) * amp);
                if (CreateFish(fishRoot, pos, rare: false, mats)) { coins++; }
            }
            // Anéis de peixes nos trechos largos
            for (int k = 60; k < goalK - 20; k += 130) {
                int best = k; float bestW = 0f;
                for (int j = k; j < Mathf.Min(k + 60, goalK - 20); j++) { if (WallDist(j) > bestW) { bestW = WallDist(j); best = j; } }
                if (bestW < 7f || avoid.Any(a => Mathf.Abs(a - best) < 10)) { continue; }
                Vector3 c = Point(best);
                for (int a = 0; a < 8; a++) {
                    float ang = a * Mathf.PI / 4f;
                    if (CreateFish(fishRoot, c + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 3.2f, false, mats)) { coins++; }
                }
            }

            // --- peixes dourados nos cantos (longe da linha central = exploração)
            int rare = 0;
            foreach (var (from, to) in new[] { (0.12f, 0.28f), (0.33f, 0.50f), (0.55f, 0.72f), (0.76f, 0.92f) }) {
                int cell = HiddenCorner(K(from), K(to));
                if (cell < 0) { continue; }
                var pos = TSB.CellCenter(cell);
                if (CreateFish(fishRoot, pos, rare: true, mats)) { rare++; s_Shots.Add(($"dourado_{rare}", TSB.s_Steps[cell])); }
            }

            // --- obstáculos, perigos e biomas (densidade cresce ao longo da fase)
            var busy = avoid.Concat(shooterK).Concat(monsterK).ToList();
            PlaceChallenges(busy, mats, extra);

            ConfigurePlayer(player);
            int coinTarget = Mathf.Max(10, Mathf.RoundToInt(coins * 0.6f / 10f) * 10);
            int enemyTarget = Mathf.Max(3, Mathf.RoundToInt(enemies * 0.6f));
            CreateSession(player, Ph.Missions(coinTarget, enemyTarget, rare), extra.Snow);

            log.AppendLine($"Peixes: {coins}, dourados: {rare}, inimigos: {enemies}, checkpoints: {checkpoints.Length}, NPCs: {npcSpots.Length}");
            log.AppendLine($"Missões: {coinTarget} peixes, {enemyTarget} capangas, {rare} dourados");
            Physics.SyncTransforms();
            WalkTest(player);
            return true;
        }

        // ------------------------------------------------------------------ caminho

        private static void BuildCenterline()
        {
            int W = TSB.s_W, H = TSB.s_H, N = W * H;
            var walk = TSB.s_Walk;
            var steps = TSB.s_Steps;

            // distância até a borda do caminho (chamfer)
            s_WallDist = new float[N];
            for (int i = 0; i < N; i++) { s_WallDist[i] = walk[i] ? 1e6f : 0f; }
            for (int z = 0; z < H; z++)
            for (int x = 0; x < W; x++) {
                int i = z * W + x; float v = s_WallDist[i];
                if (x > 0) v = Mathf.Min(v, s_WallDist[i - 1] + 1f);
                if (z > 0) { v = Mathf.Min(v, s_WallDist[i - W] + 1f); if (x > 0) v = Mathf.Min(v, s_WallDist[i - W - 1] + 1.414f); if (x < W - 1) v = Mathf.Min(v, s_WallDist[i - W + 1] + 1.414f); }
                s_WallDist[i] = v;
            }
            for (int z = H - 1; z >= 0; z--)
            for (int x = W - 1; x >= 0; x--) {
                int i = z * W + x; float v = s_WallDist[i];
                if (x < W - 1) v = Mathf.Min(v, s_WallDist[i + 1] + 1f);
                if (z < H - 1) { v = Mathf.Min(v, s_WallDist[i + W] + 1f); if (x < W - 1) v = Mathf.Min(v, s_WallDist[i + W + 1] + 1.414f); if (x > 0) v = Mathf.Min(v, s_WallDist[i + W - 1] + 1.414f); }
                s_WallDist[i] = v;
            }

            // agrupa células por passo e escolhe, para cada passo, a mais central perto da anterior
            s_MaxStep = 0;
            for (int i = 0; i < N; i++) { if (walk[i] && steps[i] > s_MaxStep) { s_MaxStep = steps[i]; } }
            var byStep = new List<int>[s_MaxStep + 1];
            for (int i = 0; i < N; i++) { if (walk[i]) { (byStep[steps[i]] ??= new List<int>()).Add(i); } }
            s_Center = new int[s_MaxStep + 1];
            Vector3 prev = TSB.CellCenter(byStep[0][0]);
            for (int k = 0; k <= s_MaxStep; k++) {
                var list = byStep[k];
                if (list == null || list.Count == 0) { s_Center[k] = s_Center[Mathf.Max(0, k - 1)]; continue; }
                int best = list[0]; float bestScore = float.MinValue;
                foreach (int c in list) {
                    float score = s_WallDist[c] - 0.6f * Vector3.Distance(TSB.CellCenter(c), prev);
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                s_Center[k] = best;
                prev = TSB.CellCenter(best);
            }

            // distância (em passos) até a linha central, para achar cantinhos escondidos
            s_FromCenter = Enumerable.Repeat(int.MaxValue, N).ToArray();
            var q = new Queue<int>();
            foreach (int c in s_Center.Distinct()) { s_FromCenter[c] = 0; q.Enqueue(c); }
            while (q.Count > 0) {
                int i = q.Dequeue(); int x = i % W, z = i / W;
                foreach (var (nx, nz) in new[] { (x + 1, z), (x - 1, z), (x, z + 1), (x, z - 1) }) {
                    if (!TSB.InGrid(nx, nz)) { continue; }
                    int n = nz * W + nx;
                    if (!walk[n] || s_FromCenter[n] != int.MaxValue) { continue; }
                    s_FromCenter[n] = s_FromCenter[i] + 1; q.Enqueue(n);
                }
            }
        }

        private static Vector3 Point(int k)
        {
            k = Mathf.Clamp(k, 0, s_MaxStep);
            var c = TSB.CellCenter(s_Center[k]);
            return TSB.GroundAt(c, out var g) ? g : c;
        }

        private static Vector3 Dir(int k)
        {
            Vector3 a = TSB.CellCenter(s_Center[Mathf.Clamp(k - 6, 0, s_MaxStep)]);
            Vector3 b = TSB.CellCenter(s_Center[Mathf.Clamp(k + 6, 0, s_MaxStep)]);
            Vector3 d = b - a; d.y = 0;
            return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        private static Vector3 Perp(int k) => Vector3.Cross(Vector3.up, Dir(k));
        private static float WallDist(int k) => s_WallDist[s_Center[Mathf.Clamp(k, 0, s_MaxStep)]];

        /// <summary>Ponto andável a até 'max' unidades para o lado do caminho, sem encostar na borda.</summary>
        private static Vector3 SideOf(int k, int side, float max, float margin)
        {
            Vector3 c = Point(k);
            float off = Mathf.Clamp(WallDist(k) - margin, 0f, max);
            Vector3 p = c + Perp(k) * (side * off);
            return TSB.GroundAt(p, out var g) ? g : p;
        }

        /// <summary>
        /// Ponto andável à frente do jogador (≈6–9 m, levemente de lado para não bloquear a passagem),
        /// longe das bordas. Null se não houver espaço.
        /// </summary>
        private static Vector3? InFrontOfPlayer(Transform player, int side)
        {
            Vector3 fwd = player.forward; fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) { fwd = Vector3.forward; }
            fwd.Normalize();
            Vector3 toPath = Point(16) - player.position; toPath.y = 0;
            // o jogador pode nascer um pouco no ar (fase 1): compara com o chão embaixo dele
            float baseY = TSB.GroundAt(player.position, out var under) ? under.y : player.position.y;
            var dirs = new List<Vector3> { fwd };
            if (toPath.sqrMagnitude > 1f) { dirs.Add(Vector3.Slerp(fwd, toPath.normalized, 0.5f).normalized); dirs.Add(toPath.normalized); }
            foreach (float room in new[] { 1.6f, 1.0f }) {
                foreach (var d in dirs) {
                    if (Vector3.Dot(d, fwd) < 0.2f) { continue; } // tem que estar na frente da câmera inicial
                    Vector3 right = Vector3.Cross(Vector3.up, d);
                    foreach (float dist in new[] { 7f, 6f, 8f, 9f, 5f, 10f, 11f, 4.5f }) {
                        foreach (float lat in new[] { 2.2f * side, 1.2f * side, 0f, -1.2f * side, -2.2f * side }) {
                            Vector3 p = player.position + d * dist + right * lat;
                            if (!Roomy(p, room)) { continue; }
                            if (!TSB.GroundAt(p, out var g)) { continue; }
                            if (Mathf.Abs(g.y - baseY) > 2.5f) { continue; } // nada de NPC em cima de pedra/barranco
                            return g;
                        }
                    }
                }
            }
            var log = TSB.s_Log;
            log.AppendLine($"  jogador {player.position} frente {fwd} caminho {toPath.normalized}; andável: " +
                string.Join(" ", new[] { 2f, 4f, 6f, 8f, 10f }.Select(s => {
                    Vector3 p = player.position + fwd * s;
                    string h = TSB.GroundAt(p, out var g) ? (g.y - baseY).ToString("F1") : "sem chão";
                    return $"{s}m=walk{(TSB.IsWalk(p) ? 1 : 0)}/room{(Roomy(p, 1f) ? 1 : 0)}/dy {h}";
                })));
            return null;
        }

        private static bool Roomy(Vector3 p, float r)
        {
            if (!TSB.IsWalk(p)) { return false; }
            for (int a = 0; a < 8; a++) {
                float ang = a * Mathf.PI / 4f;
                if (!TSB.IsWalk(p + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * r)) { return false; }
            }
            return true;
        }

        private static int HiddenCorner(int fromK, int toK)
        {
            int best = -1, bestScore = -1;
            var steps = TSB.s_Steps;
            for (int i = 0; i < s_FromCenter.Length; i++) {
                if (!TSB.s_Walk[i] || steps[i] < fromK || steps[i] > toK) { continue; }
                if (s_WallDist[i] < 2.5f || s_FromCenter[i] == int.MaxValue) { continue; }
                if (s_FromCenter[i] > bestScore) { bestScore = s_FromCenter[i]; best = i; }
            }
            return bestScore >= 6 ? best : best; // mesmo sem canto, coloca no ponto mais afastado
        }

        // ------------------------------------------------------------------ objetos

        private static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(s_Root, false);
            return t;
        }

        private static bool CreateFish(Transform parent, Vector3 at, bool rare, Mats mats)
        {
            if (!TSB.GroundAt(at, out var ground)) { return false; }
            var go = new GameObject(rare ? "Peixe Dourado" : "Peixe");
            go.transform.SetParent(parent, false);
            go.transform.position = ground + Vector3.up * (P * (rare ? 0.55f : 0.45f));
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = P * 0.4f;

            // peixe dourado: menor que o comum e com brilho discreto (é segredo, não farol)
            var visual = Model(rare ? RareFishModel : FishModel, go.transform, P * (rare ? 0.22f : 0.3f), out _);
            if (visual == null) { Object.DestroyImmediate(go); return false; }
            if (rare) {
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = r.sharedMaterials.Select(_ => mats.Gold).ToArray(); }
                var light = new GameObject("Brilho").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.type = LightType.Point; light.color = new Color(1f, 0.85f, 0.4f); light.range = P * 1.4f; light.intensity = 0.9f;
                light.shadows = LightShadows.None;
            }
            go.AddComponent<FishPickup>().Setup(rare, rare ? 5 : 1, rare ? 1 : 0, visual.transform);
            return true;
        }

        private static void CreateCheckpoint(Transform parent, int index, int k, Mats mats)
        {
            Vector3 c = Point(k);
            Vector3 dir = Dir(k);
            var go = new GameObject($"Checkpoint {index}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(c, Quaternion.LookRotation(dir));
            float width = Mathf.Max(6f, WallDist(k) * 2f + 2f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, P, 0);
            box.size = new Vector3(width, P * 3f, P * 1.2f);

            var baseGo = InstantiatePrefab(IceCircle, go.transform);
            if (baseGo != null) { Fit(baseGo, P * 2.2f, horizontal: true); baseGo.transform.localPosition = Vector3.up * 0.03f; }

            // Cristal ao lado do caminho (não bloqueia a passagem)
            var pillarRoot = new GameObject("Cristal").transform;
            pillarRoot.SetParent(go.transform, false);
            float side = Mathf.Clamp(WallDist(k) - 1.8f, 1.5f, 5f);
            pillarRoot.position = (TSB.GroundAt(c + Perp(k) * side, out var g) ? g : c + Perp(k) * side);
            var crystal = Model(CrystalModel, pillarRoot, P * 1.5f, out _);
            var renderers = crystal != null ? crystal.GetComponentsInChildren<Renderer>() : Array.Empty<Renderer>();
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(pillarRoot, false);
            light.transform.localPosition = Vector3.up * P;
            light.type = LightType.Point; light.color = new Color(0.5f, 0.9f, 1f); light.range = P * 5f; light.intensity = 3f; light.shadows = LightShadows.None;

            var spawn = new GameObject("Renascer aqui").transform;
            spawn.SetParent(go.transform, false);
            spawn.localPosition = Vector3.up * 0.3f;

            go.AddComponent<Checkpoint>().Setup(spawn, renderers, mats.CheckpointOn, light, pillarRoot);
            s_Shots.Add(($"checkpoint_{index}", k));
        }

        /// <summary>Portal de gelo (anel de cristais girando + luz). O componente é adicionado por quem chama.</summary>
        private static (GameObject go, Transform spinner) CreatePortalVisual(Transform parent, Vector3 c, Quaternion rot, Mats mats, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(c, rot);
            var sphere = go.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.center = Vector3.up * P;
            sphere.radius = P * 1.2f;

            var baseGo = InstantiatePrefab(IceCircle, go.transform);
            if (baseGo != null) { Fit(baseGo, P * 4f, horizontal: true); baseGo.transform.localPosition = Vector3.up * 0.03f; }

            var spinner = new GameObject("Anel").transform;
            spinner.SetParent(go.transform, false);
            for (int i = 0; i < 6; i++) {
                float a = i * Mathf.PI / 3f;
                var holder = new GameObject("Cristal " + i).transform;
                holder.SetParent(spinner, false);
                holder.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (P * 1.9f);
                var cr = Model(CrystalModel, holder, P * (i % 2 == 0 ? 1.6f : 1.1f), out _);
                if (cr != null) { foreach (var r in cr.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.Portal; } }
            }
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = Vector3.up * P * 1.2f;
            light.type = LightType.Point; light.color = new Color(0.55f, 0.65f, 1f); light.range = P * 8f; light.intensity = 4f; light.shadows = LightShadows.None;
            return (go, spinner);
        }

        private static void CreateNpc(Transform parent, string id, string name, int k, int side, DialogueLine[] first, DialogueLine[] repeat,
            string[] missions, float scale, Vector3? at = null, Vector3? lookAt = null)
        {
            Vector3 pos = at ?? SideOf(k, side, 4f, 2.2f);
            var go = new GameObject("NPC " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<SphereCollider>().isTrigger = true;

            var modelRoot = new GameObject("Modelo").transform;
            modelRoot.SetParent(go.transform, false);
            Vector3 face = (lookAt ?? Point(k)) - pos; face.y = 0;
            modelRoot.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : -Perp(k) * side);
            var model = Model(PenguinModel, modelRoot, s_ModelHeight * scale, out _);
            if (model != null) {
                var anim = model.GetComponentInChildren<Animator>();
                if (anim == null) { anim = model.AddComponent<Animator>(); }
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PenguinAnimator);
                anim.applyRootMotion = false;
                var nice = AssetDatabase.LoadAssetAtPath<Material>(NicePenguinMat);
                if (nice != null) {
                    foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                        r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("penguin") ? nice : m).ToArray();
                    }
                }
            }
            modelRoot.gameObject.AddComponent<NpcAnimator>(); // idle no chão + aceno (sem isso fica na pose de queda)
            var body = modelRoot.gameObject.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * (s_ModelHeight * scale * 0.5f);
            body.height = s_ModelHeight * scale;
            body.radius = s_ModelHeight * scale * 0.28f;

            // "!" amarelo flutuando (some depois da primeira conversa)
            var marker = new GameObject("Marcador !");
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = Vector3.up * (s_ModelHeight * scale + P * 0.45f);
            var tm = marker.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font; tm.text = "!"; tm.fontSize = 96; tm.characterSize = P * 0.06f; tm.anchor = TextAnchor.MiddleCenter;
            tm.color = new Color(1f, 0.82f, 0.2f); tm.fontStyle = FontStyle.Bold;
            marker.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            marker.AddComponent<FloatingMarker>();

            go.AddComponent<NpcDialogue>().Setup(id, name, first, repeat, missions, modelRoot, marker);
            s_Shots.Add(("npc_" + id, k));
        }

        private static bool CreateShooter(Transform parent, int k, int side)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShooterPrefab);
            if (prefab == null) { return false; }
            Vector3 pos = SideOf(k, side, 7f, 2.5f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Capanga do BSOD (atirador)";
            go.transform.position = pos;
            Vector3 face = Point(k) - pos - Dir(k) * 4f; face.y = 0;
            go.transform.rotation = Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face.normalized : -Dir(k));
            // Mede só o corpo (sem efeitos de partícula/morte), para ficar do tamanho do Linux.
            float h = Height(go.GetComponentsInChildren<SkinnedMeshRenderer>(true), 0f);
            if (h > 0.01f) { go.transform.localScale *= s_ModelHeight * 1.05f / h; }

            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) { rb.isKinematic = true; }
            var enemy = go.GetComponentInChildren<Enemy>();
            if (enemy != null) {
                var so = new SerializedObject(enemy);
                so.FindProperty("m_ViewDistance").floatValue = 24f;
                so.FindProperty("m_AttackDistance").floatValue = Ph.ShooterRange;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var spawner = go.GetComponentInChildren<AutomaticProjectileSpawner>();
            if (spawner != null) {
                var so = new SerializedObject(spawner);
                so.FindProperty("m_FirePeriod").floatValue = Ph.ShooterFire;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var dmgHost = go.GetComponentInChildren<Damageable>();
            var stomp = (dmgHost != null ? dmgHost.gameObject : go).AddComponent<Stompable>();
            s_Shots.Add(("capanga_" + k, k));
            return stomp != null;
        }

        private static bool CreateIceMonster(Transform parent, int k, Mats mats, bool tutorial)
        {
            float half = Mathf.Clamp(WallDist(k) - 2f, 0f, 6f);
            Vector3 a, b;
            if (half >= 2.5f) {
                a = Point(k) - Perp(k) * half; b = Point(k) + Perp(k) * half;
            } else {
                a = Point(k - 5); b = Point(k + 5);
            }
            if (!TSB.GroundAt(a, out a) || !TSB.GroundAt(b, out b)) { return false; }

            var go = new GameObject("Monstro de Gelo");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(a, Quaternion.LookRotation(Flat(b - a).sqrMagnitude > 0.01f ? Flat(b - a).normalized : Vector3.forward));
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;

            float h = P * 0.85f;
            var solid = go.AddComponent<CapsuleCollider>();
            solid.center = Vector3.up * (h * 0.5f); solid.height = h; solid.radius = h * 0.38f;
            var hitbox = new GameObject("Hitbox").AddComponent<CapsuleCollider>();
            hitbox.transform.SetParent(go.transform, false);
            hitbox.isTrigger = true; hitbox.center = Vector3.up * (h * 0.5f); hitbox.height = h * 1.1f; hitbox.radius = h * 0.55f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var body = Model(CrystalModel, visual, h, out _);
            if (body != null) { foreach (var r in body.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.Monster; } }
            // olhos
            for (int s = -1; s <= 1; s += 2) {
                var eye = Model(SnowballModel, visual, h * 0.2f, out _);
                if (eye == null) { continue; }
                eye.transform.localPosition = new Vector3(s * h * 0.14f, h * 0.62f, h * 0.27f);
                foreach (var r in eye.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.EyeWhite; }
                var pupil = Model(SnowballModel, visual, h * 0.09f, out _);
                if (pupil == null) { continue; }
                pupil.transform.localPosition = new Vector3(s * h * 0.14f, h * 0.62f, h * 0.36f);
                foreach (var r in pupil.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.Pupil; }
            }

            // estrelinhas de tontura (Dypsloom) sobre a cabeça
            GameObject dizzy = null;
            var dizzyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DizzyFx);
            if (dizzyPrefab != null) {
                dizzy = (GameObject)PrefabUtility.InstantiatePrefab(dizzyPrefab, go.transform);
                dizzy.transform.localPosition = Vector3.up * (h * 1.05f);
                dizzy.transform.localRotation = Quaternion.identity;
                float dw = Bounds(dizzy).size.x;
                if (dw > 0.01f) { dizzy.transform.localScale *= h * 0.9f / dw; }
            }

            int hp = tutorial ? 1 : Ph.MonsterHp;
            var dmg = go.AddComponent<Damageable>();
            var so = new SerializedObject(dmg);
            so.FindProperty("m_MaxHp").intValue = hp;
            so.FindProperty("m_CurrentHp").intValue = hp;
            so.FindProperty("m_InvincibilityTime").floatValue = 0.25f;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<IceMonster>().Setup(a, b, visual, AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPuff), dizzy, tutorial, Ph.Aggression);
            go.AddComponent<Stompable>();
            if (tutorial) { go.name = "Monstro de Gelo (tutorial)"; }
            s_Shots.Add(("monstro_" + k, k));
            return true;
        }

        private static void ConfigurePlayer(PenguinCharacter player)
        {
            var dmg = player.GetComponent<CharacterDamageable>();
            if (dmg != null) {
                var so = new SerializedObject(dmg);
                so.FindProperty("m_MaxHp").intValue = 3;
                so.FindProperty("m_CurrentHp").intValue = 3;
                so.FindProperty("m_InvincibilityTime").floatValue = 1.2f;
                so.ApplyModifiedProperties();
            }
            var pso = new SerializedObject(player);
            pso.FindProperty("m_SlideDamage").intValue = 2;
            pso.ApplyModifiedProperties();

            var thrower = player.GetComponent<SnowballThrower>();
            if (thrower == null) { thrower = Undo.AddComponent<SnowballThrower>(player.gameObject); }
            if (player.GetComponent<CombatTargeting>() == null) { Undo.AddComponent<CombatTargeting>(player.gameObject); }
            thrower.Setup(BuildAssistSnowball(), AssistSnowballSpeed);
            EditorUtility.SetDirty(thrower);
            if (player.GetComponent<DamageFlash>() == null) { Undo.AddComponent<DamageFlash>(player.gameObject); }
        }

        private static FeedbackBank LoadOrBuildBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<FeedbackBank>(AudioSynthBuilder.BankPath);
            return bank != null ? bank : AudioSynthBuilder.BuildAll();
        }

        private static void CreateSession(PenguinCharacter player, List<Mission> missions, Material snow)
        {
            var go = new GameObject("Sessão de Jogo (HUD, vidas, missões)");
            go.transform.SetParent(s_Root, false);
            var session = go.AddComponent<GameSession>();
            float minY = float.MaxValue;
            for (int k = 0; k <= s_MaxStep; k += 10) { minY = Mathf.Min(minY, Point(k).y); }
            session.EditorSetup(player, 3, minY - 30f, missions,
                AssetDatabase.LoadAssetAtPath<Font>(TitleFont),
                AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite),
                AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSprite), snow, LoadOrBuildBank(), Ph.NextScene, Ph.Areas[0].name, Ph.Areas[0].blizzard,
                AssetDatabase.LoadAssetAtPath<Font>(BodyFont));
            EditorUtility.SetDirty(session);
        }

        // ------------------------------------------------------------------ conteúdo (história)

        private static List<Mission> Missions(int coins, int enemies, int rare) => new List<Mission> {
            new Mission { id = "falar_anciao", title = "Fale com o Ancião Tux", type = MissionType.TalkTo, key = "anciao", activeAtStart = true, main = true, bonusCoins = 0 },
            new Mission { id = "topo", title = "Chegue ao topo da Montanha Ancestral", type = MissionType.ReachGoal, main = true, bonusCoins = 0 },
            new Mission { id = "peixes", title = $"Colete {coins} peixes", type = MissionType.CollectCoins, target = coins, bonusCoins = 20 },
            new Mission { id = "capangas", title = $"Derrote {enemies} capangas do BSOD", type = MissionType.DefeatEnemies, target = enemies, bonusCoins = 15 },
            new Mission { id = "checkpoints", title = "Ative os 3 checkpoints", type = MissionType.ReachCheckpoint, target = 3, bonusCoins = 10 },
            new Mission { id = "dourados", title = $"Encontre os {rare} peixes dourados", type = MissionType.CollectRare, target = Mathf.Max(1, rare), bonusCoins = 25 },
            new Mission { id = "falar_gentoo", title = "Fale com o Sábio Gentoo no cume", type = MissionType.TalkTo, key = "gentoo", bonusCoins = 10 },
            new Mission { id = "boss", title = "Derrote o BSOD", type = MissionType.DefeatBoss, main = true, bonusCoins = 50 },
        };

        private static DialogueLine L(string text) => new DialogueLine { text = text };

        private static Dictionary<string, (string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, float scale)> Dialogues() =>
            new Dictionary<string, (string, DialogueLine[], DialogueLine[], string[], float)> {
                ["anciao"] = ("Ancião Tux", new[] {
                    L("Linux! Graças ao Kernel você chegou."),
                    L("Há séculos nós, pinguins, vivemos livres aqui no Reino Congelado de Kernel. Cada um cuida do seu pedaço de gelo do seu jeito... caótico, mas em harmonia."),
                    L("Mas algo antigo e monolítico acordou nas profundezas da Montanha Ancestral: o BSOD, um golem de gelo colossal."),
                    L("Ele quer travar o mundo inteiro sob um único sistema rígido. Por onde passa, congela trilhas, florestas e vales... e os capangas dele já estão na nossa trilha."),
                    L("Você é teimoso demais para ser instalado à força, eu sei. Suba até o topo da Montanha Ancestral. Junte peixes pelo caminho e derrote os capangas do BSOD."),
                    L("Se você parar, se hesitar... o reino inteiro trava para sempre. Vá!"),
                }, new[] { L("O topo da Montanha Ancestral fica no fim da trilha. Não pare, Linux!") },
                new[] { "topo", "peixes", "capangas" }, 1.15f),

                ["debi"] = ("Batedora Debi", new[] {
                    L("Psiu! Aqui, Linux. Sou a Debi, batedora da vila. Estável como uma rocha... e de olho em tudo."),
                    L("Os capangas do BSOD atiram bolas de gelo de longe. Responda com as suas: clique ou F arremessa uma bola de neve."),
                    L("Quando um inimigo aparece na sua frente, ele ganha uma mira amarela: é nele que a bola vai. Segure o botão direito para travar a câmera nele."),
                    L("Os Monstros de Gelo tremem antes de dar o bote. Saia da frente: depois do bote eles ficam tontos. Aí é só pular na cabeça ou deslizar com Ctrl!"),
                    L("Esses cristais brilhantes são checkpoints. Se cair, você volta para o último que ativou. Ative todos!"),
                }, new[] { L("Clique ou F: bola de neve no alvo marcado. Botão direito: travar câmera. Ctrl: deslizar. Pular na cabeça também derruba!") },
                new[] { "checkpoints" }, 1f),

                ["arch"] = ("Construtora Arch", new[] {
                    L("Eu sou a Arch. Construí meu iglu peça por peça, do jeito que eu quis, e não vou deixar o BSOD congelar tudo num formato só."),
                    L("Ouvi dizer que existem peixes dourados escondidos nos cantos do caminho. Eles valem muito... e recuperam um coração!"),
                    L("Fique de olho nos cantinhos e desvios. Quem explora encontra."),
                }, new[] { L("Os peixes dourados ficam nos cantos, fora do caminho principal. Eles curam um coração!") },
                new[] { "dourados" }, 1f),

                ["fedora"] = ("Guardiã Fedora", new[] {
                    L("Alto! Ah, é você, Linux. Sou a Fedora, guardiã do caminho da montanha."),
                    L("Daqui pra frente a trilha sobe até o pico. O frio fica mais forte e os capangas, mais numerosos."),
                    L("O Sábio Gentoo espera no cume. Ele compilou tudo o que sabemos sobre o BSOD... levou só alguns dias."),
                }, new[] { L("O Sábio Gentoo está no cume. Siga a trilha para cima!") },
                new[] { "falar_gentoo" }, 1f),

                ["gentoo"] = ("Sábio Gentoo", new[] {
                    L("Você chegou ao cume, jovem Linux. Impressionante."),
                    L("O BSOD dorme além deste pico, mas já sente a sua presença. Quando ele tomar dano, vai travar por um instante, piscando em azul."),
                    L("Essa é a janela: acerte o golpe decisivo enquanto ele estiver travado."),
                    L("Bolas de neve e deslizes só enchem a barra de travamento dele. Quando ele travar, acerte qualquer golpe: é isso que quebra um núcleo."),
                    L("E cuidado com o pisão: quando ele erguer os braços, pule a onda de choque!"),
                    L("Atravesse o portal de gelo. A verdadeira batalha começa do outro lado."),
                }, new[] { L("Encha a barra azul, espere ele travar e acerte! Pule as ondas de choque.") },
                new[] { "boss" }, 1.1f),
            };

        // ------------------------------------------------------------------ materiais

        private sealed class Mats { public Material Gold, Monster, EyeWhite, Pupil, CheckpointOn, Portal; }

        private static Mats BuildMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Gameplay")) { AssetDatabase.CreateFolder("Assets", "Gameplay"); }
            if (!AssetDatabase.IsValidFolder(MatDir)) { AssetDatabase.CreateFolder("Assets/Gameplay", "Materials"); }
            var crystal = AssetDatabase.LoadAssetAtPath<Material>(CrystalMat);
            var snow = AssetDatabase.LoadAssetAtPath<Material>(SnowballMat);
            var standard = Shader.Find("Standard");

            Material Make(string name, Material from, Color color, Color emission, float metallic = 0f, float gloss = 0.5f)
            {
                string path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) {
                    mat = from != null ? new Material(from) : new Material(standard);
                    AssetDatabase.CreateAsset(mat, path);
                }
                if (mat.HasProperty("_Color")) { mat.SetColor("_Color", color); }
                if (mat.HasProperty("_BaseColor")) { mat.SetColor("_BaseColor", color); }
                if (mat.HasProperty("_Metallic")) { mat.SetFloat("_Metallic", metallic); }
                if (mat.HasProperty("_Glossiness")) { mat.SetFloat("_Glossiness", gloss); }
                if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor")) {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                EditorUtility.SetDirty(mat);
                return mat;
            }

            var mats = new Mats {
                Gold = Make("PeixeDourado", null, new Color(0.95f, 0.75f, 0.2f), new Color(0.22f, 0.13f, 0.0f), 0.8f, 0.85f),
                // Opaco e mais escuro que os cristais do cenário, para o inimigo se destacar.
                Monster = Make("MonstroDeGeloOpaco", null, new Color(0.22f, 0.5f, 0.95f, 1f), new Color(0.04f, 0.16f, 0.42f), 0.1f, 0.85f),
                EyeWhite = Make("OlhoBranco", snow, Color.white, Color.black),
                Pupil = Make("Pupila", null, new Color(0.05f, 0.05f, 0.08f), Color.black, 0f, 0.8f),
                CheckpointOn = Make("CheckpointAtivo", crystal, new Color(0.6f, 1f, 1f, 1f), new Color(0.25f, 0.85f, 1f) * 1.5f),
                Portal = Make("PortalDeGelo", crystal, new Color(0.65f, 0.6f, 1f, 1f), new Color(0.35f, 0.3f, 1f) * 1.4f),
            };
            AssetDatabase.SaveAssets();
            return mats;
        }

        // ------------------------------------------------------------------ util

        private static GameObject InstantiatePrefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { TSB.s_Log.AppendLine("Asset não encontrado: " + path); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        /// <summary>Instancia um modelo com a altura desejada, apoiado no pivô do pai.</summary>
        private static GameObject Model(string path, Transform parent, float height, out float radius)
        {
            radius = 0f;
            var go = InstantiatePrefab(path, parent);
            if (go == null) { return null; }
            foreach (var c in go.GetComponentsInChildren<Collider>()) { Object.DestroyImmediate(c); }
            go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            if (b.size.y < 1e-4f) { return go; }
            float s = height / b.size.y;
            go.transform.localScale = Vector3.one * s;
            b = Bounds(go);
            // apoia a base no pivô do pai e centraliza em XZ
            Vector3 offset = parent.position - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += offset;
            radius = Mathf.Max(b.extents.x, b.extents.z);
            return go;
        }

        private static void Fit(GameObject go, float size, bool horizontal)
        {
            go.transform.localScale = Vector3.one;
            var b = Bounds(go);
            float current = horizontal ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            if (current > 1e-4f) { go.transform.localScale = Vector3.one * (size / current); }
            foreach (var c in go.GetComponentsInChildren<Collider>()) { Object.DestroyImmediate(c); }
        }

        private static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { return new Bounds(go.transform.position, Vector3.zero); }
            var b = rs[0].bounds; foreach (var r in rs) { b.Encapsulate(r.bounds); }
            return b;
        }

        private static float Height(Renderer[] rs, float fallback)
        {
            if (rs == null || rs.Length == 0) { return fallback; }
            var b = rs[0].bounds; foreach (var r in rs) { b.Encapsulate(r.bounds); }
            return b.size.y > 0.01f ? b.size.y : fallback;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

        // ------------------------------------------------------------------ screenshots

        private static void Capture(string outDir)
        {
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            float pitch = follow != null ? follow.FixedPitch : 14f;
            float dist = follow != null ? follow.FixedDistance : 10f;
            float height = follow != null ? follow.FixedHeightOffset : 2.6f;
            var player = Object.FindAnyObjectByType<PenguinCharacter>();
            var main = Camera.main;

            var cam = new GameObject("_ShotCam").AddComponent<Camera>();
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; cam.fieldOfView = main.fieldOfView; }
            cam.farClipPlane = 3000f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            Vector3 oldPos = player.transform.position; Quaternion oldRot = player.transform.rotation;

            // No máximo 2 fotos de cada tipo (npc_, capanga_, monstro_...).
            var shots = s_Shots.GroupBy(s => s.name.Split('_')[0]).SelectMany(g => g.Key == "npc" ? g.Take(1) : g.Take(1)).ToList();
            var views = new List<(string name, Vector3 pos, float yaw)>();
            foreach (var (name, k) in shots) {
                int at = Mathf.Clamp(k - 9, 0, s_MaxStep);
                Vector3 gp = Point(at);
                Vector3 to = Point(k) - gp; to.y = 0;
                Vector3 d = to.sqrMagnitude > 1f ? to.normalized : Dir(at);
                views.Add((name, gp, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg));
            }
            views.AddRange(s_ExtraShots);
            foreach (var (name, g, yaw) in views) {
                player.transform.SetPositionAndRotation(g, Quaternion.Euler(0, yaw, 0));
                var rot = Quaternion.Euler(pitch, yaw, 0f);
                cam.transform.SetPositionAndRotation(g + Vector3.up * height - rot * Vector3.forward * dist, rot);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
            }
            // Prévia do HUD: diálogo + retícula num monstro (o HUD normal só existe em Play)
            var monster = Object.FindAnyObjectByType<IceMonster>();
            if (monster != null) {
                Vector3 mpos = monster.transform.position;
                Vector3 back = -monster.transform.forward;
                Vector3 g = mpos + back * 7f;
                Vector3 to = mpos - g; to.y = 0;
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                player.transform.SetPositionAndRotation(g, Quaternion.Euler(0, yaw, 0));
                var rot = Quaternion.Euler(pitch + 5f, yaw, 0f);
                cam.transform.SetPositionAndRotation(g + Vector3.up * height - rot * Vector3.forward * (dist + 1.5f), rot);
                var hudGo = new GameObject("_HudPreview");
                var session = Object.FindAnyObjectByType<GameSession>();
                var hud = hudGo.AddComponent<GameHUD>();
                var so = new SerializedObject(session);
                hud.Init(session, (Font)so.FindProperty("titleFont").objectReferenceValue, (Font)so.FindProperty("bodyFont").objectReferenceValue,
                    (Sprite)so.FindProperty("panelSprite").objectReferenceValue, (Sprite)so.FindProperty("buttonSprite").objectReferenceValue);
                Vector3 sp = cam.WorldToScreenPoint(monster.GetComponent<Collider>().bounds.center);
                hud.EditorPreview(cam, "Batedora Debi", "Quando um inimigo aparece na sua frente, ele ganha uma mira amarela: é nele que a bola vai. Segure o botão direito para travar a câmera nele.",
                    new Vector2(sp.x, sp.y) * (1920f / rt.width));
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, "hud_preview.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
                Object.DestroyImmediate(hudGo);
            }

            player.transform.SetPositionAndRotation(oldPos, oldRot);
            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(rt);
        }
    }
}
