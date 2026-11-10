using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Dypsloom.DypThePenguin.Scripts.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Gera o cenário em volta do trajeto da fase (estilo Crash):
    ///  1. Descobre a área andável fazendo um "flood fill" a partir do pinguim, usando as cercas como paredes.
    ///  2. Troca as cercas da borda por uma barreira invisível alta (não dá para pular) + uma borda
    ///     natural de pedras, cristais de gelo, tocos e pinheiros pequenos.
    ///  3. Preenche o lado de fora do caminho com floresta nevada (densa perto, mais espaçada longe).
    /// Tudo fica dentro do objeto "_CenarioGerado" e pode ser removido/gerado de novo pelo menu.
    /// As cercas originais não são apagadas: só têm o Renderer/Collider desligados.
    /// </summary>
    public static class TrailSceneryBuilder
    {
        private const string ScenePath = "Assets/Scenes/Mountain.unity";
        private const string RootName = "_CenarioGerado";
        private const int Seed = 1989;
        private const int BarrierLayer = 2; // Ignore Raycast: a câmera ignora as barreiras.
        private const float WallHeight = 45f;
        private const float WallThickness = 1.2f;
        private const float Margin = 80f;

        private const string WinterDir = "Assets/GeeZyyGames/Winter/prefab/";
        private const string CrystalPath = "Assets/Dypsloom/DypThePenguin/Demo/Models/M_Crystal.fbx";

        private static readonly Regex FenceName = new Regex(@"^fence( \(\d+\))?$");
        private static readonly Regex PropName = new Regex(
            @"^(fence|pine tree|tree|tree cut|rock|rock small|snowman|igloo|hut|lamp|bench|wood|leaves|flower|vase|ice circle|M_Crystal|Dyp)( \(\d+\))?$");

        // ---------------------------------------------------------------- tipos

        private sealed class Seg
        {
            public Vector3 A, B;           // pontos no chão (y = base da cerca)
            public MeshFilter Source;      // null = conector (fecha vão entre cercas)
            public Vector3 Dir => Flat(B - A).normalized;
            public float Length => Flat(B - A).magnitude;
            public Vector3 Right => Vector3.Cross(Vector3.up, Dir);
        }

        private sealed class Piece
        {
            public string Name;
            public GameObject Prefab;
            public float Height;           // altura do mesh na escala 1
            public float Radius;           // raio no chão na escala 1
            public float PivotToBottom;    // base do mesh - pivô (escala 1)
            public Vector3 CenterOffset;   // centro XZ - pivô (escala 1)
            public bool Tilt;              // pode inclinar um pouco (pedras/cristais)
        }

        // grade 2D (1 célula = 1 unidade)
        internal static int s_W, s_H;
        internal static float s_MinX, s_MinZ;
        private static bool[] s_Blocked;
        internal static bool[] s_Walk;
        internal static int[] s_Steps;
        private static float[] s_Dist;

        internal static float s_PenguinHeight = 1.5f;
        private static float s_TopY;
        private static float s_MinGroundY;
        private static Dictionary<string, Piece> s_Pieces;
        private static Dictionary<long, List<Vector3>> s_Hash; // x,z,raio
        private static Random s_Rng;
        internal static StringBuilder s_Log;

        // ---------------------------------------------------------------- menus

        [MenuItem("Tools/Furia Pinguim/Cenário/Gerar cenário em volta do trajeto (Mountain)")]
        public static void GenerateMenu()
        {
            if (!EnsureMountainOpen()) { return; }
            var ok = Generate(null);
            Debug.Log(s_Log.ToString());
            if (ok) {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Cenário", "Cenário gerado. Salve a cena (Ctrl+S).", "OK");
            } else {
                EditorUtility.DisplayDialog("Cenário", "Não consegui fechar a área do trajeto. Veja o Console.", "OK");
            }
        }

        [MenuItem("Tools/Furia Pinguim/Cenário/Remover cenário gerado (volta as cercas)")]
        public static void ClearMenu()
        {
            if (!EnsureMountainOpen()) { return; }
            Clear();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static bool EnsureMountainOpen()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) { return true; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return false; }
            EditorSceneManager.OpenScene(ScenePath);
            return true;
        }

        // ---------------------------------------------------------------- batchmode

        /// <summary>Unity -batchmode -executeMethod ...GenerateBatch -outDir &lt;pasta&gt; [-dryRun]</summary>
        public static void GenerateBatch()
        {
            string outDir = GetArg("-outDir") ?? "SceneryOut";
            bool dryRun = Environment.GetCommandLineArgs().Contains("-dryRun");
            Directory.CreateDirectory(outDir);
            int code = 0;
            try {
                EditorSceneManager.OpenScene(ScenePath);
                bool ok = Generate(outDir, dryRun);
                if (ok && !dryRun) {
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                    CaptureViews(outDir);
                }
                if (!ok) { code = 2; }
            } catch (Exception e) {
                s_Log ??= new StringBuilder();
                s_Log.AppendLine("EXCEPTION: " + e);
                code = 1;
            }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), s_Log?.ToString() ?? "");
            EditorApplication.Exit(code);
        }

        /// <summary>Só tira screenshots da cena como está.</summary>
        public static void CaptureBatch()
        {
            string outDir = GetArg("-outDir") ?? "SceneryOut";
            Directory.CreateDirectory(outDir);
            s_Log = new StringBuilder();
            int code = 0;
            try {
                EditorSceneManager.OpenScene(ScenePath);
                if (!BuildWalkMap(out _)) { code = 2; } else { CaptureViews(outDir); }
            } catch (Exception e) { s_Log.AppendLine("EXCEPTION: " + e); code = 1; }
            File.WriteAllText(Path.Combine(outDir, "log.txt"), s_Log.ToString());
            EditorApplication.Exit(code);
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // ---------------------------------------------------------------- geração

        public static bool Generate(string debugDir, bool dryRun = false)
        {
            s_Log = new StringBuilder();
            s_Rng = new Random(Seed);
            Clear();
            Physics.SyncTransforms();

            if (!BuildWalkMap(out var segs)) {
                if (debugDir != null) { File.WriteAllText(Path.Combine(debugDir, "walkmap.txt"), DumpWalkMap(3)); }
                return false;
            }
            if (debugDir != null) { File.WriteAllText(Path.Combine(debugDir, "walkmap.txt"), DumpWalkMap(3)); }
            if (dryRun) { return true; }

            LoadPieces();
            ComputeDistanceField();
            s_Hash = new Dictionary<long, List<Vector3>>();

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Gerar cenário");
            var walls = new GameObject("Barreiras invisiveis").transform; walls.SetParent(root.transform, false);
            var border = new GameObject("Borda (pedras e gelo)").transform; border.SetParent(root.transform, false);
            var forest = new GameObject("Floresta").transform; forest.SetParent(root.transform, false);

            int hidden = 0, kept = 0, wallCount = 0, borderCount = 0;
            foreach (var seg in segs) {
                int right = SideWalkCount(seg, +1), left = SideWalkCount(seg, -1);
                if (right == 0 && left == 0) { continue; } // cerca longe do caminho: fica como está

                CreateWall(walls, seg); wallCount++;

                if (right > 0 && left > 0) { kept++; continue; } // divisória no meio do caminho: mantém a cerca

                if (seg.Source != null) { SetFenceVisible(seg.Source, false); hidden++; }
                Vector3 outward = right > 0 ? -seg.Right : seg.Right;
                borderCount += DecorateBorder(border, seg, outward);
            }

            int forestCount = PlaceForest(forest);
            ApplyCameraFraming();

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            s_Log.AppendLine($"Cercas escondidas: {hidden}, cercas mantidas (divisórias): {kept}");
            s_Log.AppendLine($"Barreiras invisíveis: {wallCount}, peças de borda: {borderCount}, floresta: {forestCount}");
            return true;
        }

        /// <summary>Monta o mapa do caminho (usado também pelo gerador de gameplay).</summary>
        internal static bool PrepareWalkMap()
        {
            s_Log = new StringBuilder();
            s_Rng = new Random(Seed);
            Physics.SyncTransforms();
            return BuildWalkMap(out _);
        }

        public static void Clear()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects()) {
                if (go.name == RootName) { Undo.DestroyObjectImmediate(go); }
            }
            foreach (var mf in AllFences()) { SetFenceVisible(mf, true); }
        }

        private static IEnumerable<MeshFilter> AllFences() =>
            Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include)
                .Where(mf => mf.sharedMesh != null && FenceName.IsMatch(mf.gameObject.name));

        private static void SetFenceVisible(MeshFilter mf, bool visible)
        {
            var r = mf.GetComponent<Renderer>();
            if (r != null && r.enabled != visible) { Undo.RecordObject(r, "Cerca"); r.enabled = visible; }
            var c = mf.GetComponent<Collider>();
            if (c != null && c.enabled != visible) { Undo.RecordObject(c, "Cerca"); c.enabled = visible; }
        }

        // ---------------------------------------------------------------- área andável

        private static bool BuildWalkMap(out List<Seg> segs)
        {
            s_Log ??= new StringBuilder();
            segs = new List<Seg>();
            foreach (var mf in AllFences()) { segs.Add(FenceToSeg(mf)); }
            if (segs.Count == 0) { s_Log.AppendLine("Nenhuma cerca encontrada."); return false; }

            var character = Object.FindAnyObjectByType<Character>(FindObjectsInactive.Include);
            if (character == null) { s_Log.AppendLine("Pinguim (Character) não encontrado na cena."); return false; }
            var cc = character.GetComponent<CharacterController>();
            s_PenguinHeight = cc != null ? cc.height * character.transform.lossyScale.y : 1.5f;
            if (s_PenguinHeight < 0.2f || s_PenguinHeight > 10f) { s_PenguinHeight = 1.5f; }

            var pts = segs.SelectMany(s => new[] { s.A, s.B }).ToList();
            s_TopY = pts.Max(p => p.y) + 150f;
            s_MinGroundY = pts.Min(p => p.y) - 40f;
            s_MinX = pts.Min(p => p.x) - Margin;
            s_MinZ = pts.Min(p => p.z) - Margin;
            s_W = Mathf.CeilToInt(pts.Max(p => p.x) + Margin - s_MinX);
            s_H = Mathf.CeilToInt(pts.Max(p => p.z) + Margin - s_MinZ);

            s_Log.AppendLine($"Cercas: {segs.Count}  comprimento médio {segs.Average(s => s.Length):F1}  altura pinguim {s_PenguinHeight:F2}");
            s_Log.AppendLine($"Grade {s_W}x{s_H} a partir de ({s_MinX:F0},{s_MinZ:F0})");

            BuildHeightMap();

            // Fecha vãos pequenos entre cercas vizinhas (aberturas reais do caminho são bem maiores).
            foreach (float bridge in new[] { 6f, 10f, 14f, 19f, -1f }) {
                var withConnectors = new List<Seg>(segs);
                withConnectors.AddRange(MakeConnectors(segs, bridge < 0 ? 19f : bridge));
                if (bridge < 0) {
                    // Última tentativa: liga pontas soltas (fim de linha de cerca) à ponta solta mais próxima.
                    var deadEnds = MakeDeadEndConnectors(withConnectors, 45f).ToList();
                    s_Log.AppendLine($"Ligando {deadEnds.Count} pares de pontas soltas.");
                    withConnectors.AddRange(deadEnds);
                }
                Rasterize(withConnectors);
                RasterizeProps(character);

                Vector2Int start = ToCell(character.transform.position);
                if (!FindFreeCell(ref start)) { s_Log.AppendLine("Pinguim dentro de uma cerca?"); return false; }

                int leakCell = Flood(start);
                int walk = s_Walk.Count(w => w);
                s_Log.AppendLine($"Flood (ponte {bridge}): {walk} células andáveis, vazou = {leakCell >= 0}");
                if (leakCell < 0) {
                    segs = withConnectors;
                    return true;
                }
                if (bridge < 0f) { LogLeakPath(leakCell); }
            }
            s_Log.AppendLine("A área do caminho não está fechada por cercas: o flood vazou para fora. Veja walkmap.txt.");
            return false;
        }

        private static Seg FenceToSeg(MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds;
            var t = mf.transform;
            Vector3 c = t.TransformPoint(b.center);
            Vector3[] axes = {
                t.TransformVector(new Vector3(b.extents.x, 0, 0)),
                t.TransformVector(new Vector3(0, b.extents.y, 0)),
                t.TransformVector(new Vector3(0, 0, b.extents.z)),
            };
            Vector3 along = axes.OrderByDescending(a => Flat(a).magnitude).First();
            float vertical = axes.Sum(a => Mathf.Abs(a.y));
            float bottom = c.y - vertical;
            Vector3 h = Flat(along);
            return new Seg {
                A = new Vector3(c.x - h.x, bottom, c.z - h.z),
                B = new Vector3(c.x + h.x, bottom, c.z + h.z),
                Source = mf,
            };
        }

        private static IEnumerable<Seg> MakeConnectors(List<Seg> segs, float maxGap)
        {
            var ends = new List<(Vector3 p, int seg)>();
            for (int i = 0; i < segs.Count; i++) { ends.Add((segs[i].A, i)); ends.Add((segs[i].B, i)); }
            for (int i = 0; i < ends.Count; i++) {
                for (int j = i + 1; j < ends.Count; j++) {
                    if (ends[i].seg == ends[j].seg) { continue; }
                    var d = Flat(ends[j].p - ends[i].p).magnitude;
                    if (d < 0.5f || d > maxGap || Mathf.Abs(ends[j].p.y - ends[i].p.y) > 16f) { continue; }
                    // Só liga pontas "na mesma linha": o conector precisa seguir mais ou menos uma das cercas.
                    var dir = Flat(ends[j].p - ends[i].p).normalized;
                    float align = Mathf.Max(Mathf.Abs(Vector3.Dot(dir, segs[ends[i].seg].Dir)),
                        Mathf.Abs(Vector3.Dot(dir, segs[ends[j].seg].Dir)));
                    // Vãos curtos: basta seguir mais ou menos a cerca. Vãos longos: tem que estar bem alinhado
                    // com AS DUAS cercas (furo numa linha reta), nunca atravessando a largura do corredor.
                    if (d <= 8f && align < 0.5f) { continue; }
                    if (d > 8f) {
                        float alignBoth = Mathf.Min(Mathf.Abs(Vector3.Dot(dir, segs[ends[i].seg].Dir)),
                            Mathf.Abs(Vector3.Dot(dir, segs[ends[j].seg].Dir)));
                        if (alignBoth < 0.85f) { continue; }
                    }
                    yield return new Seg { A = ends[i].p, B = ends[j].p, Source = null };
                }
            }
        }

        /// <summary>
        /// Pontas soltas = fim de uma linha de cerca que não encosta em nenhuma outra cerca.
        /// Liga pares mutuamente mais próximos (ex.: as duas cercas da trilha que terminam no cume).
        /// </summary>
        private static IEnumerable<Seg> MakeDeadEndConnectors(List<Seg> segs, float maxDist)
        {
            var ends = new List<(Vector3 p, int seg)>();
            for (int i = 0; i < segs.Count; i++) { ends.Add((segs[i].A, i)); ends.Add((segs[i].B, i)); }

            bool IsDead(int e)
            {
                var p = ends[e].p;
                for (int s = 0; s < segs.Count; s++) {
                    if (s == ends[e].seg) { continue; }
                    if (Mathf.Abs(segs[s].A.y - p.y) > 20f && Mathf.Abs(segs[s].B.y - p.y) > 20f) { continue; }
                    if (DistToSeg(p, segs[s]) < 3f) { return false; }
                }
                return true;
            }

            var dead = Enumerable.Range(0, ends.Count).Where(IsDead).ToList();
            foreach (int e in dead) { s_Log.AppendLine($"  ponta solta em ({ends[e].p.x:F0},{ends[e].p.y:F0},{ends[e].p.z:F0})"); }
            int Nearest(int e) => dead.Where(o => o != e && ends[o].seg != ends[e].seg && Mathf.Abs(ends[o].p.y - ends[e].p.y) < 25f)
                .OrderBy(o => Flat(ends[o].p - ends[e].p).sqrMagnitude).DefaultIfEmpty(-1).First();

            var used = new HashSet<int>();
            foreach (int e in dead) {
                if (used.Contains(e)) { continue; }
                int o = Nearest(e);
                if (o < 0 || used.Contains(o) || Nearest(o) != e) { continue; }
                if (Flat(ends[o].p - ends[e].p).magnitude > maxDist) { continue; }
                used.Add(e); used.Add(o);
                var a = ends[e].p; var b = ends[o].p;
                s_Log.AppendLine($"  ligando ({a.x:F0},{a.z:F0}) <-> ({b.x:F0},{b.z:F0})");
                yield return new Seg { A = a, B = b, Source = null };
            }
        }

        private static float DistToSeg(Vector3 p, Seg s)
        {
            Vector3 a = Flat(s.A), b = Flat(s.B), q = Flat(p);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            return (a + ab * t - q).magnitude;
        }

        private static void Rasterize(List<Seg> segs)
        {
            s_Blocked = new bool[s_W * s_H];
            foreach (var s in segs) {
                float len = s.Length;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.25f));
                for (int i = 0; i <= steps; i++) {
                    Vector3 p = Vector3.Lerp(s.A, s.B, i / (float)steps);
                    var c = ToCell(p);
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++) {
                        int x = c.x + dx, z = c.y + dz;
                        if (!InGrid(x, z)) { continue; }
                        float cx = s_MinX + x + 0.5f - p.x, cz = s_MinZ + z + 0.5f - p.z;
                        if (cx * cx + cz * cz <= 1.0f) { s_Blocked[z * s_W + x] = true; }
                    }
                }
            }
        }

        private static bool FindFreeCell(ref Vector2Int c)
        {
            for (int r = 0; r < 6; r++)
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++) {
                int x = c.x + dx, z = c.y + dz;
                if (InGrid(x, z) && !s_Blocked[z * s_W + x]) { c = new Vector2Int(x, z); return true; }
            }
            return false;
        }

        /// <summary>Flood fill a partir do pinguim. Retorna -1 se ficou fechado, ou a 1ª célula da borda alcançada.</summary>
        private static int Flood(Vector2Int start)
        {
            s_Walk = new bool[s_W * s_H];
            s_Steps = new int[s_W * s_H];
            s_Parent = new int[s_W * s_H];
            var q = new Queue<int>();
            int si = start.y * s_W + start.x;
            s_Walk[si] = true; s_Parent[si] = -1; q.Enqueue(si);
            int leak = -1;
            while (q.Count > 0) {
                int i = q.Dequeue();
                int x = i % s_W, z = i / s_W;
                if (leak < 0 && (x == 0 || z == 0 || x == s_W - 1 || z == s_H - 1)) { leak = i; }
                void Try(int nx, int nz)
                {
                    if (!InGrid(nx, nz)) { return; }
                    int ni = nz * s_W + nx;
                    if (s_Walk[ni] || s_Blocked[ni]) { return; }
                    // Relevo: sem chão ou subida/descida íngreme demais para o pinguim = parede natural.
                    float h0 = s_Height[i], h1 = s_Height[ni];
                    if (float.IsNaN(h1) || (!float.IsNaN(h0) && Mathf.Abs(h1 - h0) > MaxStepPerCell)) { return; }
                    s_Walk[ni] = true; s_Steps[ni] = s_Steps[i] + 1; s_Parent[ni] = i; q.Enqueue(ni);
                }
                Try(x + 1, z); Try(x - 1, z); Try(x, z + 1); Try(x, z - 1);
            }
            return leak;
        }

        private static int[] s_Parent;
        internal static float[] s_Height;
        private const float MaxStepPerCell = 0.85f; // ~40° de inclinação

        /// <summary>Altura do chão em cada célula (NaN = sem chão, ex.: oceano/abismo).</summary>
        private static void BuildHeightMap()
        {
            s_Height = new float[s_W * s_H];
            var buffer = new RaycastHit[16];
            float dist = s_TopY - s_MinGroundY + 50f;
            int none = 0;
            for (int z = 0; z < s_H; z++) {
                if (z % 50 == 0) { EditorUtility.DisplayProgressBar("Cenário", "Lendo o relevo...", z / (float)s_H); }
                for (int x = 0; x < s_W; x++) {
                    var origin = new Vector3(s_MinX + x + 0.5f, s_TopY, s_MinZ + z + 0.5f);
                    int n = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, dist, ~0, QueryTriggerInteraction.Ignore);
                    float best = float.NaN, bestDist = float.MaxValue;
                    for (int k = 0; k < n; k++) {
                        var h = buffer[k];
                        if (h.distance >= bestDist || IsGenerated(h.collider.transform)) { continue; }
                        if (h.collider.gameObject.layer == BarrierLayer || PropName.IsMatch(h.collider.gameObject.name)) { continue; }
                        if (h.point.y < s_MinGroundY) { continue; }
                        best = h.point.y; bestDist = h.distance;
                    }
                    s_Height[z * s_W + x] = best;
                    if (float.IsNaN(best)) { none++; }
                }
            }
            EditorUtility.ClearProgressBar();
            s_Log.AppendLine($"Relevo lido: {none} células sem chão de {s_W * s_H}");
        }

        /// <summary>Mostra no log o caminho do pinguim até fora do mapa (onde a cerca está aberta).</summary>
        private static void LogLeakPath(int cell)
        {
            var path = new List<int>();
            for (int c = cell; c >= 0; c = s_Parent[c]) { path.Add(c); }
            path.Reverse();
            s_Log.Append("Caminho do vazamento (x,z a cada 15 passos): ");
            for (int k = 0; k < path.Count; k += 15) {
                var p = CellCenter(path[k]);
                s_Log.Append($"({p.x:F0},{p.z:F0}) ");
            }
            s_Log.AppendLine();
        }

        /// <summary>
        /// Props com colisor (iglu, cabana, pedras, árvores, bonecos de neve) também bloqueiam:
        /// em alguns lugares são eles que fecham o vão entre duas cercas.
        /// </summary>
        private static void RasterizeProps(Character character)
        {
            int count = 0;
            foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude)) {
                if (!col.enabled || col.isTrigger || IsGenerated(col.transform)) { continue; }
                if (col.transform.IsChildOf(character.transform)) { continue; }
                string n = col.gameObject.name;
                if (FenceName.IsMatch(n) || !PropName.IsMatch(n) || n.StartsWith("Dyp")) { continue; }
                var b = col.bounds;
                if (b.size.x > 80f || b.size.z > 80f) { continue; } // não é prop
                // Usa 70% da caixa (copas e bases são mais largas que o tronco/obstáculo real).
                float hx = b.extents.x * 0.7f, hz = b.extents.z * 0.7f;
                var min = ToCell(new Vector3(b.center.x - hx, 0, b.center.z - hz));
                var max = ToCell(new Vector3(b.center.x + hx, 0, b.center.z + hz));
                for (int z = min.y; z <= max.y; z++)
                for (int x = min.x; x <= max.x; x++) {
                    if (InGrid(x, z)) { s_Blocked[z * s_W + x] = true; }
                }
                count++;
            }
            s_Log.AppendLine($"Props com colisor usados como bloqueio: {count}");
        }

        /// <summary>Distância (em unidades) de cada célula até a área andável mais próxima.</summary>
        private static void ComputeDistanceField()
        {
            const float D = 1f, D2 = 1.4142f;
            s_Dist = new float[s_W * s_H];
            for (int i = 0; i < s_Dist.Length; i++) { s_Dist[i] = s_Walk[i] ? 0f : 1e6f; }
            for (int z = 0; z < s_H; z++)
            for (int x = 0; x < s_W; x++) {
                int i = z * s_W + x; float v = s_Dist[i];
                if (x > 0) v = Mathf.Min(v, s_Dist[i - 1] + D);
                if (z > 0) {
                    v = Mathf.Min(v, s_Dist[i - s_W] + D);
                    if (x > 0) v = Mathf.Min(v, s_Dist[i - s_W - 1] + D2);
                    if (x < s_W - 1) v = Mathf.Min(v, s_Dist[i - s_W + 1] + D2);
                }
                s_Dist[i] = v;
            }
            for (int z = s_H - 1; z >= 0; z--)
            for (int x = s_W - 1; x >= 0; x--) {
                int i = z * s_W + x; float v = s_Dist[i];
                if (x < s_W - 1) v = Mathf.Min(v, s_Dist[i + 1] + D);
                if (z < s_H - 1) {
                    v = Mathf.Min(v, s_Dist[i + s_W] + D);
                    if (x < s_W - 1) v = Mathf.Min(v, s_Dist[i + s_W + 1] + D2);
                    if (x > 0) v = Mathf.Min(v, s_Dist[i + s_W - 1] + D2);
                }
                s_Dist[i] = v;
            }
        }

        private static int SideWalkCount(Seg s, int sign)
        {
            int n = 0;
            foreach (float t in new[] { 0.15f, 0.35f, 0.5f, 0.65f, 0.85f }) {
                Vector3 p = Vector3.Lerp(s.A, s.B, t) + s.Right * (2.2f * sign);
                if (IsWalk(p)) { n++; }
            }
            return n;
        }

        // ---------------------------------------------------------------- barreiras

        private static void CreateWall(Transform parent, Seg s)
        {
            var go = new GameObject(s.Source != null ? "Barreira " + s.Source.name : "Barreira (vão)");
            go.layer = BarrierLayer;
            go.transform.SetParent(parent, false);
            Vector3 mid = (s.A + s.B) * 0.5f;
            go.transform.position = mid + Vector3.up * (WallHeight * 0.5f - 3f);
            go.transform.rotation = Quaternion.LookRotation(s.Dir, Vector3.up);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(WallThickness, WallHeight, s.Length + 1.2f);
        }

        // ---------------------------------------------------------------- peças

        private static void LoadPieces()
        {
            s_Pieces = new Dictionary<string, Piece>();
            void Add(string key, string path, bool tilt)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { s_Log.AppendLine("Asset não encontrado: " + path); return; }
                var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                tmp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                tmp.transform.localScale = Vector3.one;
                var rs = tmp.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Object.DestroyImmediate(tmp); return; }
                var b = rs[0].bounds; foreach (var r in rs) { b.Encapsulate(r.bounds); }
                Object.DestroyImmediate(tmp);
                s_Pieces[key] = new Piece {
                    Name = key, Prefab = prefab, Height = Mathf.Max(0.001f, b.size.y),
                    Radius = Mathf.Max(0.001f, Mathf.Max(b.extents.x, b.extents.z)),
                    PivotToBottom = b.min.y, CenterOffset = new Vector3(b.center.x, 0, b.center.z), Tilt = tilt,
                };
                s_Log.AppendLine($"Peça {key}: altura {b.size.y:F3} raio {s_Pieces[key].Radius:F3} (escala 1)");
            }
            Add("pine", WinterDir + "pine tree.prefab", false);
            Add("tree", WinterDir + "tree.prefab", false);
            Add("stump", WinterDir + "tree cut.prefab", false);
            Add("rock", WinterDir + "rock.prefab", true);
            Add("rockSmall", WinterDir + "rock small.prefab", true);
            Add("wood", WinterDir + "wood.prefab", false);
            Add("snowman", WinterDir + "snowman.prefab", false);
            Add("crystal", CrystalPath, true);
        }

        /// <summary>Instancia uma peça com a altura desejada (em alturas de pinguim) apoiada no chão.</summary>
        private static GameObject Spawn(Transform parent, string key, Vector3 at, float heightInPenguins, out float worldRadius,
            bool checkSpace = true, float spacing = 0f)
        {
            worldRadius = 0f;
            if (!s_Pieces.TryGetValue(key, out var piece)) { return null; }
            float scale = heightInPenguins * s_PenguinHeight / piece.Height;
            worldRadius = piece.Radius * scale;

            if (!GroundAt(at, out var ground)) { return null; }
            if (checkSpace && !HasSpace(ground, Mathf.Max(spacing, worldRadius * 0.9f))) { return null; }
            if (OverlapsSceneProp(ground, worldRadius * 0.7f, heightInPenguins * s_PenguinHeight)) { return null; }

            float yaw = (float)s_Rng.NextDouble() * 360f;
            Quaternion rot = Quaternion.Euler(0, yaw, 0);
            if (piece.Tilt) { rot *= Quaternion.Euler(RandRange(-10f, 10f), 0, RandRange(-10f, 10f)); }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(piece.Prefab, parent);
            go.transform.localScale = Vector3.one * scale;
            Vector3 offset = Quaternion.Euler(0, yaw, 0) * (piece.CenterOffset * scale);
            float sink = piece.Tilt ? 0.15f : 0.05f;
            go.transform.SetPositionAndRotation(
                new Vector3(ground.x - offset.x, ground.y - piece.PivotToBottom * scale - sink * piece.Height * scale, ground.z - offset.z),
                rot);
            Register(ground, Mathf.Max(spacing * 0.5f, worldRadius * 0.9f));
            return go;
        }

        // ---------------------------------------------------------------- borda

        private static int DecorateBorder(Transform parent, Seg s, Vector3 outward)
        {
            int count = 0;
            float len = s.Length;
            float t = RandRange(0f, 1.5f);
            while (t < len) {
                Vector3 onLine = s.A + s.Dir * t;
                double roll = s_Rng.NextDouble();
                string key; float h;
                if (roll < 0.34) { key = "rock"; h = RandRange(1.2f, 2.0f); }
                else if (roll < 0.52) { key = "crystal"; h = RandRange(1.1f, 2.0f); }
                else if (roll < 0.66) { key = "rockSmall"; h = RandRange(0.8f, 1.3f); }
                else if (roll < 0.76) { key = "stump"; h = RandRange(0.9f, 1.4f); }
                else if (roll < 0.84) { key = "wood"; h = RandRange(1.0f, 1.5f); }
                else if (roll < 0.985) { key = "pine"; h = RandRange(3.0f, 4.5f); }
                else { key = "snowman"; h = RandRange(1.6f, 2.0f); }

                float radius = s_Pieces.TryGetValue(key, out var p) ? p.Radius * (h * s_PenguinHeight / p.Height) : 1f;
                Vector3 at = onLine + outward * (radius * 0.85f + 0.4f) + s.Dir * RandRange(-0.6f, 0.6f);
                var go = Spawn(parent, key, at, h, out _, checkSpace: true, spacing: radius * 0.8f);
                if (go != null) { count++; }

                // Pedras pequenas ao redor das grandes, para quebrar a linha reta.
                if (go != null && (key == "rock" || key == "crystal") && s_Rng.NextDouble() < 0.6) {
                    Vector3 extra = at + outward * RandRange(0.5f, 1.5f) * radius + s.Dir * RandRange(-1.2f, 1.2f) * radius;
                    if (Spawn(parent, s_Rng.NextDouble() < 0.5 ? "rockSmall" : "crystal", extra, RandRange(0.6f, 1.1f), out _) != null) { count++; }
                }
                t += Mathf.Max(1.2f, radius * RandRange(1.1f, 1.6f));
            }
            return count;
        }

        // ---------------------------------------------------------------- floresta

        private static int PlaceForest(Transform parent)
        {
            var candidates = new List<int>();
            for (int i = 0; i < s_Dist.Length; i++) {
                if (!s_Walk[i] && !s_Blocked[i] && s_Dist[i] >= 3.5f && s_Dist[i] <= 60f) { candidates.Add(i); }
            }
            // Embaralha (determinístico) e tenta posicionar respeitando o espaçamento.
            for (int i = candidates.Count - 1; i > 0; i--) {
                int j = s_Rng.Next(i + 1); (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int placed = 0;
            foreach (int i in candidates) {
                float d = s_Dist[i];
                float spacing = d < 12f ? 5.5f : d < 28f ? 8.5f : 13f;
                Vector3 at = new Vector3(s_MinX + i % s_W + (float)s_Rng.NextDouble(), 0, s_MinZ + i / s_W + (float)s_Rng.NextDouble());
                if (!HasSpace(at, spacing)) { continue; }

                double roll = s_Rng.NextDouble();
                string key; float h;
                if (d < 12f) {
                    if (roll < 0.72) { key = "pine"; h = RandRange(4.0f, 6.5f); }
                    else if (roll < 0.88) { key = "tree"; h = RandRange(3.5f, 5.0f); }
                    else if (roll < 0.95) { key = "rock"; h = RandRange(1.3f, 2.2f); }
                    else { key = "crystal"; h = RandRange(1.3f, 2.4f); }
                } else if (d < 28f) {
                    if (roll < 0.70) { key = "pine"; h = RandRange(5.0f, 8.0f); }
                    else if (roll < 0.88) { key = "tree"; h = RandRange(4.0f, 6.0f); }
                    else if (roll < 0.94) { key = "rock"; h = RandRange(1.8f, 3.0f); }
                    else if (roll < 0.97) { key = "crystal"; h = RandRange(1.6f, 2.8f); }
                    else { key = "stump"; h = RandRange(0.9f, 1.4f); }
                } else {
                    if (roll < 0.88) { key = "pine"; h = RandRange(6.5f, 10f); }
                    else if (roll < 0.95) { key = "rock"; h = RandRange(2.2f, 3.8f); }
                    else { key = "crystal"; h = RandRange(1.8f, 3.2f); }
                }

                // Nada pode invadir o caminho: a distância até a área andável tem que ser maior que o raio.
                float radius = s_Pieces.TryGetValue(key, out var p) ? p.Radius * (h * s_PenguinHeight / p.Height) : 1f;
                if (d < radius * 0.8f + 2.5f) { continue; }

                if (Spawn(parent, key, at, h, out _, checkSpace: false, spacing: spacing) != null) { placed++; }
            }
            return placed;
        }

        // ---------------------------------------------------------------- chão e espaço

        internal static bool GroundAt(Vector3 at, out Vector3 ground)
        {
            ground = default;
            var hits = Physics.RaycastAll(new Vector3(at.x, s_TopY, at.z), Vector3.down, s_TopY - s_MinGroundY + 50f,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits.OrderBy(h => h.distance)) {
                if (IsGenerated(h.collider.transform) || PropName.IsMatch(h.collider.gameObject.name)) { continue; }
                if (h.collider.gameObject.layer == BarrierLayer) { continue; }
                if (h.point.y < s_MinGroundY) { return false; }
                if (h.normal.y < 0.45f) { return false; } // encosta muito íngreme
                ground = h.point;
                return true;
            }
            return false;
        }

        private static bool OverlapsSceneProp(Vector3 ground, float radius, float height)
        {
            var cols = Physics.OverlapCapsule(ground + Vector3.up * (radius + 0.2f), ground + Vector3.up * Mathf.Max(radius + 0.2f, height * 0.6f),
                Mathf.Max(0.3f, radius), ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols) {
                if (c.gameObject.layer == BarrierLayer || IsGenerated(c.transform)) { continue; }
                if (PropName.IsMatch(c.gameObject.name)) { return true; }
            }
            return false;
        }

        internal static bool IsGenerated(Transform t) => t.root.name == RootName || t.root.name == "_Gameplay";

        private static long HashKey(int x, int z) => ((long)x << 32) ^ (uint)z;
        private const float HashCell = 8f;

        private static bool HasSpace(Vector3 p, float minDist)
        {
            int cx = Mathf.FloorToInt(p.x / HashCell), cz = Mathf.FloorToInt(p.z / HashCell);
            int r = Mathf.CeilToInt((minDist + 8f) / HashCell);
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++) {
                if (!s_Hash.TryGetValue(HashKey(cx + dx, cz + dz), out var list)) { continue; }
                foreach (var e in list) {
                    float ddx = e.x - p.x, ddz = e.y - p.z;
                    float need = Mathf.Max(minDist, e.z);
                    if (ddx * ddx + ddz * ddz < need * need) { return false; }
                }
            }
            return true;
        }

        private static void Register(Vector3 p, float radius)
        {
            long k = HashKey(Mathf.FloorToInt(p.x / HashCell), Mathf.FloorToInt(p.z / HashCell));
            if (!s_Hash.TryGetValue(k, out var list)) { s_Hash[k] = list = new List<Vector3>(); }
            list.Add(new Vector3(p.x, p.z, radius));
        }

        /// <summary>
        /// Enquadramento estilo Crash proporcional ao tamanho do pinguim: mais alto e mais longe,
        /// olhando para frente (mostra o caminho e a floresta, não só o chão).
        /// </summary>
        private static void ApplyCameraFraming()
        {
            var follow = Object.FindAnyObjectByType<CameraFollow>();
            if (follow == null) { return; }
            var so = new SerializedObject(follow);
            so.FindProperty("fixedDistance").floatValue = Mathf.Round(s_PenguinHeight * 4f * 10f) / 10f;
            so.FindProperty("fixedPitch").floatValue = 14f;
            so.FindProperty("fixedHeightOffset").floatValue = Mathf.Round(s_PenguinHeight * 1.05f * 10f) / 10f;
            so.FindProperty("autoFollow").boolValue = true;
            so.FindProperty("fixedBehind").boolValue = true;
            so.ApplyModifiedProperties();
            s_Log.AppendLine($"Câmera: distância {follow.FixedDistance}, pitch {follow.FixedPitch}, altura {follow.FixedHeightOffset}");
        }

        // ---------------------------------------------------------------- screenshots

        private static void CaptureViews(string outDir)
        {
            var cam = new GameObject("_ShotCam").AddComponent<Camera>();
            var main = Camera.main;
            if (main != null) {
                cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor;
            }
            cam.fieldOfView = main != null ? main.fieldOfView : 60f;
            cam.farClipPlane = 3000f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;

            void Shot(string name)
            {
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                RenderTexture.active = null;
            }

            // Visão geral de cima.
            var walkCells = Enumerable.Range(0, s_Walk.Length).Where(i => s_Walk[i]).ToList();
            float minX = walkCells.Min(i => i % s_W) + s_MinX, maxX = walkCells.Max(i => i % s_W) + s_MinX;
            float minZ = walkCells.Min(i => i / s_W) + s_MinZ, maxZ = walkCells.Max(i => i / s_W) + s_MinZ;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(maxZ - minZ, (maxX - minX) * 720f / 1280f) * 0.55f;
            cam.transform.SetPositionAndRotation(new Vector3((minX + maxX) / 2, s_TopY + 200, (minZ + maxZ) / 2), Quaternion.Euler(90, 0, 0));
            Shot("00_visao_geral");

            // Vistas de jogo ao longo do caminho (câmera atrás, olhando para onde o caminho segue).
            cam.orthographic = false;
            int maxSteps = walkCells.Max(i => s_Steps[i]);
            float[] marks = { 0.03f, 0.12f, 0.22f, 0.32f, 0.42f, 0.52f, 0.62f, 0.72f, 0.82f, 0.92f };
            for (int m = 0; m < marks.Length; m++) {
                int targetSteps = Mathf.RoundToInt(marks[m] * maxSteps);
                int cell = walkCells.OrderBy(i => Mathf.Abs(s_Steps[i] - targetSteps) * 10 + (int)(-CenterScore(i))).First();
                Vector3 p = CellCenter(cell);
                Vector3 ahead = AheadDirection(cell, 10);
                if (!GroundAt(p, out var g)) { g = p; }
                float yaw = Mathf.Atan2(ahead.x, ahead.z) * Mathf.Rad2Deg;
                var follow = Object.FindAnyObjectByType<CameraFollow>();
                float pitch = follow != null ? follow.FixedPitch : 14f;
                float dist = follow != null ? follow.FixedDistance : 10f;
                float height = follow != null ? follow.FixedHeightOffset : 2.6f;
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                Vector3 focus = g + Vector3.up * height;
                cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * dist, rot);
                // Pinguim de referência no lugar onde o jogador estaria.
                var character = Object.FindAnyObjectByType<Character>();
                Vector3 oldPos = default; Quaternion oldRot = default;
                if (character != null) {
                    oldPos = character.transform.position; oldRot = character.transform.rotation;
                    character.transform.SetPositionAndRotation(g, Quaternion.Euler(0, yaw, 0));
                }
                Shot($"{m + 1:00}_caminho_{(int)(marks[m] * 100)}pct");
                if (character != null) { character.transform.SetPositionAndRotation(oldPos, oldRot); }
            }

            Object.DestroyImmediate(cam.gameObject);
            Object.DestroyImmediate(rt);
            s_Log.AppendLine("Screenshots salvos em " + outDir);
        }

        /// <summary>Quantas células andáveis existem em volta (prefere o meio do caminho, longe das bordas).</summary>
        private static float CenterScore(int i)
        {
            int x0 = i % s_W, z0 = i / s_W, n = 0;
            for (int dz = -4; dz <= 4; dz++)
            for (int dx = -4; dx <= 4; dx++) {
                int x = x0 + dx, z = z0 + dz;
                if (InGrid(x, z) && s_Walk[z * s_W + x]) { n++; }
            }
            return n;
        }

        internal static Vector3 AheadDirection(int cell, int radius)
        {
            int x0 = cell % s_W, z0 = cell / s_W;
            Vector3 sum = Vector3.zero;
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++) {
                int x = x0 + dx, z = z0 + dz;
                if (!InGrid(x, z)) { continue; }
                int i = z * s_W + x;
                if (!s_Walk[i]) { continue; }
                int diff = s_Steps[i] - s_Steps[cell];
                if (diff > 0) { sum += new Vector3(dx, 0, dz).normalized * diff; }
            }
            return sum.sqrMagnitude > 0.001f ? sum.normalized : Vector3.forward;
        }

        private static string DumpWalkMap(int step)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# área andável (1 caractere = {step} unidades; topo = +Z). '#'=cerca '.'=caminho ' '=fora");
            for (int z = s_H - 1; z >= 0; z -= step) {
                for (int x = 0; x < s_W; x += step) {
                    bool walk = false, block = false;
                    for (int dz = 0; dz < step && z - dz >= 0; dz++)
                    for (int dx = 0; dx < step && x + dx < s_W; dx++) {
                        int i = (z - dz) * s_W + x + dx;
                        walk |= s_Walk != null && s_Walk[i];
                        block |= s_Blocked[i];
                    }
                    sb.Append(block ? '#' : walk ? '.' : ' ');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- util

        private static float MeasureHeight(GameObject go, float fallback)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { return fallback; }
            var b = rs[0].bounds; foreach (var r in rs) { b.Encapsulate(r.bounds); }
            return b.size.y > 0.1f ? b.size.y : fallback;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        private static Vector2Int ToCell(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x - s_MinX), Mathf.FloorToInt(p.z - s_MinZ));
        internal static bool InGrid(int x, int z) => x >= 0 && z >= 0 && x < s_W && z < s_H;
        internal static Vector3 CellCenter(int i) => new Vector3(s_MinX + i % s_W + 0.5f, 0, s_MinZ + i / s_W + 0.5f);
        internal static bool IsWalk(Vector3 p) { var c = ToCell(p); return InGrid(c.x, c.y) && s_Walk[c.y * s_W + c.x]; }
        private static float RandRange(float a, float b) => a + (float)s_Rng.NextDouble() * (b - a);
    }
}
