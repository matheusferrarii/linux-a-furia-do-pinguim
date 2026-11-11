using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Constrói o MAPA de uma fase do zero: trilha com curvas e desníveis (seed), terreno de cânion
    /// esculpido em volta dela, textura de neve, iluminação e cercas nas bordas. Depois disso os geradores
    /// de cenário (floresta/barreiras) e de gameplay funcionam igual à Fase 1.
    /// </summary>
    public static class LevelForge
    {
        public sealed class MapStyle
        {
            public int Seed = 1;
            public int Segments = 22;
            public float SegmentLength = 38f;
            public float MaxTurn = 50f;
            public float MinWidth = 13f, MaxWidth = 22f;
            public float WallHeight = 30f;
            public float BankWidth = 16f;
            public float HillAmplitude = 18f;
            public float TotalRise = 30f;          // quanto a trilha sobe do começo ao fim
            public float StepEvery;                 // >0: degraus (ruínas)
            public Color SnowA = new Color(0.92f, 0.96f, 1f), SnowB = new Color(0.78f, 0.86f, 0.95f);
            public Color LightColor = Color.white;
            public float LightIntensity = 1f;
            public Vector3 LightEuler = new Vector3(45f, -30f, 0f);
            public Color Ambient = new Color(0.5f, 0.55f, 0.65f);
            public Color FogColor = new Color(0.75f, 0.82f, 0.9f);
            public float FogDensity = 0.004f;
            public Color SkyTint = new Color(0.5f, 0.6f, 0.8f);
            public float SkyExposure = 1.1f;
        }

        private const string TerrainDir = "Assets/Gameplay/Terrains";
        private static readonly string[] KeepComponents = { "PenguinCharacter", "Camera", "Light", "Canvas", "EventSystem", "PauseMenuController", "AudioManager" };

        /// <summary>Cria a cena a partir da Fase 1 (aproveita jogador, câmera, pausa) e troca todo o cenário.</summary>
        public static bool BuildMap(string templateScene, string targetScene, string name, MapStyle style, System.Text.StringBuilder log)
        {
            AssetDatabase.DeleteAsset(targetScene);
            if (!AssetDatabase.CopyAsset(templateScene, targetScene)) { log.AppendLine("Não consegui copiar " + templateScene); return false; }
            var scene = EditorSceneManager.OpenScene(targetScene);

            // 1) apaga todo o cenário antigo, mantendo jogador, câmera, luz, menus de pausa
            foreach (var root in scene.GetRootGameObjects()) {
                bool keep = KeepComponents.Any(c => root.GetComponentInChildren(TypeByName(c), true) != null);
                if (root.GetComponentInChildren<Light>(true) != null && root.GetComponentInChildren<Light>(true).type != LightType.Directional
                    && root.GetComponentInChildren<PenguinCharacter>(true) == null) { keep = false; }
                if (!keep) { Object.DestroyImmediate(root); }
            }

            // 2) trilha
            var rng = new System.Random(style.Seed);
            var path = BuildPath(style, rng);
            log.AppendLine($"Mapa {name}: trilha com {path.Count} pontos, comprimento ~{path.Count * 2f:F0} m");

            // 3) terreno
            var terrain = BuildTerrain(name, path, style, rng, log);

            // 4) cercas nas bordas (o gerador de cenário transforma em barreira + floresta)
            int fences = PlaceFences(path);
            log.AppendLine($"Cercas de borda: {fences}");

            // 5) jogador no começo, virado para a trilha
            var player = Object.FindAnyObjectByType<PenguinCharacter>();
            var start = path[6];
            Vector3 fwd = Flat(path[12].pos - path[2].pos).normalized;
            if (player != null) { player.transform.SetPositionAndRotation(start.pos + Vector3.up * 0.3f, Quaternion.LookRotation(fwd)); }
            var cam = Object.FindAnyObjectByType<CameraFollow>();
            if (cam != null) {
                var rot = Quaternion.LookRotation(fwd) * Quaternion.Euler(14f, 0f, 0f);
                cam.transform.SetPositionAndRotation(start.pos + Vector3.up * 2.6f - rot * Vector3.forward * 10f, rot);
            }

            // 6) luz, céu e neblina
            ApplyLighting(name, style);
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            return terrain != null;
        }

        private static System.Type TypeByName(string n) => n switch {
            "PenguinCharacter" => typeof(PenguinCharacter),
            "Camera" => typeof(Camera),
            "Light" => typeof(Light),
            "Canvas" => typeof(Canvas),
            "EventSystem" => typeof(UnityEngine.EventSystems.EventSystem),
            "PauseMenuController" => typeof(UI.PauseMenuController),
            "AudioManager" => typeof(UI.AudioManager),
            _ => typeof(Transform),
        };

        // ------------------------------------------------------------------ trilha

        public struct PathPoint { public Vector3 pos; public float width; public Vector3 tangent; }

        private static List<PathPoint> BuildPath(MapStyle s, System.Random rng)
        {
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var ctrl = new List<Vector3> { Vector3.zero };
            float heading = R(0f, 360f);
            float bound = s.Segments * s.SegmentLength * 0.32f;
            for (int i = 1; i <= s.Segments; i++) {
                Vector3 best = Vector3.zero; bool found = false;
                for (int tries = 0; tries < 40 && !found; tries++) {
                    float turn = R(-s.MaxTurn, s.MaxTurn) * (tries < 20 ? 1f : 1.8f);
                    float h = heading + turn;
                    Vector3 prev = ctrl[ctrl.Count - 1];
                    // perto da borda do mapa: puxa de volta para o centro
                    if (prev.magnitude > bound * 0.8f) {
                        float toCenter = Mathf.Atan2(-prev.x, -prev.z) * Mathf.Rad2Deg;
                        h = Mathf.LerpAngle(h, toCenter, 0.5f);
                    }
                    Vector3 c = prev + new Vector3(Mathf.Sin(h * Mathf.Deg2Rad), 0f, Mathf.Cos(h * Mathf.Deg2Rad)) * s.SegmentLength;
                    bool clear = true;
                    for (int j = 0; j < ctrl.Count - 2 && clear; j++) {
                        if (Flat(ctrl[j] - c).magnitude < s.SegmentLength * 1.9f) { clear = false; }
                    }
                    if (clear) { best = c; heading = h; found = true; }
                }
                if (!found) { break; }
                ctrl.Add(best);
            }

            // Catmull-Rom: pontos a cada ~2 m, com largura e altura suaves
            var pts = new List<PathPoint>();
            int n = ctrl.Count;
            float total = 0f;
            var raw = new List<Vector3>();
            for (int i = 0; i < n - 1; i++) {
                Vector3 p0 = ctrl[Mathf.Max(0, i - 1)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(n - 1, i + 2)];
                int steps = Mathf.CeilToInt(s.SegmentLength / 2f);
                for (int k = 0; k < steps; k++) { raw.Add(CatmullRom(p0, p1, p2, p3, k / (float)steps)); }
            }
            raw.Add(ctrl[n - 1]);
            for (int i = 1; i < raw.Count; i++) { total += Flat(raw[i] - raw[i - 1]).magnitude; }

            float acc = 0f;
            float wSeed = R(0f, 100f), hSeed = R(0f, 100f);
            for (int i = 0; i < raw.Count; i++) {
                if (i > 0) { acc += Flat(raw[i] - raw[i - 1]).magnitude; }
                float t = acc / Mathf.Max(1f, total);
                float width = Mathf.Lerp(s.MinWidth, s.MaxWidth, Mathf.PerlinNoise(wSeed, t * 6f));
                // sobe ao longo do caminho com ondulações suaves (inclinação sempre baixa)
                float y = s.TotalRise * t + (Mathf.PerlinNoise(hSeed, t * 5f) - 0.5f) * Mathf.Min(10f, s.TotalRise * 0.4f + 6f);
                // degraus suaves (ruínas): patamares com rampa curta — nunca íngreme demais para o pinguim
                if (s.StepEvery > 0f) {
                    float stepped = Mathf.Floor(y / s.StepEvery) * s.StepEvery;
                    float frac = (y - stepped) / s.StepEvery;
                    y = stepped + s.StepEvery * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((frac - 0.5f) * 2f));
                }
                var p = raw[i]; p.y = y;
                pts.Add(new PathPoint { pos = p, width = width });
            }
            for (int i = 0; i < pts.Count; i++) {
                Vector3 tg = Flat(pts[Mathf.Min(pts.Count - 1, i + 1)].pos - pts[Mathf.Max(0, i - 1)].pos).normalized;
                var pp = pts[i]; pp.tangent = tg.sqrMagnitude > 0.001f ? tg : Vector3.forward; pts[i] = pp;
            }
            return pts;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ------------------------------------------------------------------ terreno

        private static Terrain BuildTerrain(string name, List<PathPoint> path, MapStyle s, System.Random rng, System.Text.StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(TerrainDir)) {
                if (!AssetDatabase.IsValidFolder("Assets/Gameplay")) { AssetDatabase.CreateFolder("Assets", "Gameplay"); }
                AssetDatabase.CreateFolder("Assets/Gameplay", "Terrains");
            }
            float margin = 170f;
            float minX = path.Min(p => p.pos.x) - margin, maxX = path.Max(p => p.pos.x) + margin;
            float minZ = path.Min(p => p.pos.z) - margin, maxZ = path.Max(p => p.pos.z) + margin;
            float size = Mathf.Max(maxX - minX, maxZ - minZ);
            const int res = 513;
            float cell = size / (res - 1);

            // grade espacial dos pontos da trilha (busca rápida do mais próximo)
            const float bucket = 12f;
            var buckets = new Dictionary<long, List<int>>();
            for (int i = 0; i < path.Count; i++) {
                long key = Key(Mathf.FloorToInt(path[i].pos.x / bucket), Mathf.FloorToInt(path[i].pos.z / bucket));
                if (!buckets.TryGetValue(key, out var l)) { buckets[key] = l = new List<int>(); }
                l.Add(i);
            }

            float minPathY = path.Min(p => p.pos.y);
            var heights = new float[res, res];
            float hMin = float.MaxValue, hMax = float.MinValue;
            float nx = (float)rng.NextDouble() * 100f, nz = (float)rng.NextDouble() * 100f;
            for (int z = 0; z < res; z++) {
                for (int x = 0; x < res; x++) {
                    float wx = minX + x * cell, wz = minZ + z * cell;
                    // ponto da trilha mais próximo (procura em anéis de buckets)
                    int bx = Mathf.FloorToInt(wx / bucket), bz = Mathf.FloorToInt(wz / bucket);
                    float bestD = float.MaxValue; int best = -1;
                    for (int r = 0; r <= 6 && (best < 0 || r * bucket < bestD + bucket); r++) {
                        for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++) {
                            if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) { continue; }
                            if (!buckets.TryGetValue(Key(bx + dx, bz + dz), out var l)) { continue; }
                            foreach (int i in l) {
                                float ddx = path[i].pos.x - wx, ddz = path[i].pos.z - wz;
                                float d2 = ddx * ddx + ddz * ddz;
                                if (d2 < bestD * bestD || best < 0) { bestD = Mathf.Sqrt(d2); best = i; }
                            }
                        }
                    }
                    float hills = Fbm(wx * 0.012f + nx, wz * 0.012f + nz) * s.HillAmplitude;
                    float h;
                    if (best < 0) {
                        h = minPathY + s.WallHeight + hills;
                    } else {
                        var p = path[best];
                        float inner = p.width * 0.5f + 0.9f;   // a cerca fica em width/2 + 0.6, no chão plano
                        if (bestD <= inner) {
                            h = p.pos.y;
                        } else {
                            float k = Mathf.Clamp01((bestD - inner) / s.BankWidth);
                            k = 1f - (1f - k) * (1f - k);          // sobe íngreme logo depois da cerca (não dá para andar por fora)
                            float rough = (Mathf.PerlinNoise(wx * 0.08f + nz, wz * 0.08f + nx) - 0.5f) * 4f * k;
                            h = p.pos.y + k * (s.WallHeight + hills) + rough;
                        }
                    }
                    heights[z, x] = h;
                    hMin = Mathf.Min(hMin, h); hMax = Mathf.Max(hMax, h);
                }
            }
            float height = Mathf.Max(10f, hMax - hMin + 2f);
            for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) { heights[z, x] = (heights[z, x] - hMin) / height; }

            string dataPath = $"{TerrainDir}/{name}.asset";
            AssetDatabase.DeleteAsset(dataPath);
            var data = new TerrainData { heightmapResolution = res };
            data.size = new Vector3(size, height, size);
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { SnowLayer(name, s) };
            AssetDatabase.CreateAsset(data, dataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terreno";
            go.transform.position = new Vector3(minX, hMin, minZ);
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            t.heightmapPixelError = 4f;
            log.AppendLine($"Terreno {size:F0}x{size:F0} m, altura {height:F0} m");
            return t;
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        private static float Fbm(float x, float z)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int i = 0; i < 4; i++) { v += Mathf.PerlinNoise(x * f, z * f) * a; a *= 0.5f; f *= 2.1f; }
            return v;
        }

        private static TerrainLayer SnowLayer(string name, MapStyle s)
        {
            string texPath = $"{TerrainDir}/{name}_neve.png";
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
            for (int y = 0; y < 256; y++)
            for (int x = 0; x < 256; x++) {
                float n = Fbm(x * 0.04f, y * 0.04f) * 0.7f + Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.3f;
                tex.SetPixel(x, y, Color.Lerp(s.SnowB, s.SnowA, Mathf.SmoothStep(0.25f, 0.75f, n)));
            }
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            string layerPath = $"{TerrainDir}/{name}_neve.terrainlayer";
            AssetDatabase.DeleteAsset(layerPath);
            var layer = new TerrainLayer {
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
                tileSize = new Vector2(14f, 14f),
                smoothness = 0.35f,
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        // ------------------------------------------------------------------ cercas de borda

        private const string FencePrefab = "Assets/GeeZyyGames/Winter/prefab/fence.prefab";

        private static int PlaceFences(List<PathPoint> path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FencePrefab);
            if (prefab == null) { return 0; }
            var root = new GameObject("Cercas da trilha").transform;

            // Cercas CURTAS (escala 4 ≈ 9 m) acompanham as curvas sem invadir a trilha.
            const float Scale = 4f;
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.localScale = Vector3.one * Scale;
            var mesh = probe.GetComponentInChildren<MeshFilter>().sharedMesh;
            Vector3 ext = Vector3.Scale(mesh.bounds.extents, Vector3.one * Scale);
            bool alongX = ext.x >= ext.z;
            float length = Mathf.Max(ext.x, ext.z) * 2f * 0.98f;
            Object.DestroyImmediate(probe);
            Quaternion axisFix = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            int count = 0;
            void Fence(Vector3 a, Vector3 b)
            {
                Vector3 dir = Flat(b - a);
                if (dir.sqrMagnitude < 0.01f) { return; }
                var f = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                f.name = count == 0 ? "fence" : $"fence ({count})";
                f.transform.localScale = Vector3.one * Scale;
                f.transform.SetPositionAndRotation((a + b) * 0.5f, Quaternion.LookRotation(dir.normalized) * axisFix);
                count++;
            }

            // laterais: cada cerca liga dois pontos da borda separados pelo comprimento dela (corda da curva)
            var edges = new List<Vector3>[2];
            var edgeIdx = new List<int>[2];
            for (int s = 0; s < 2; s++) {
                int side = s == 0 ? -1 : 1;
                edges[s] = new List<Vector3>();
                edgeIdx[s] = new List<int>();
                for (int i = 2; i < path.Count - 2; i++) {
                    var p = path[i];
                    Vector3 e = p.pos + Vector3.Cross(Vector3.up, p.tangent) * side * (p.width * 0.5f + 0.6f);
                    // Curva fechada: do lado de dentro a borda "volta para trás" e cruzaria a trilha. Só aceita
                    // pontos que avançam e que não ficam mais perto da trilha do que a meia largura dela.
                    if (edges[s].Count > 0) {
                        Vector3 step = Flat(e - edges[s][edges[s].Count - 1]);
                        if (step.sqrMagnitude < 0.04f || Vector3.Dot(step.normalized, p.tangent) < 0.35f) { continue; }
                        bool tooClose = false;
                        for (int j = Mathf.Max(0, i - 25); j < Mathf.Min(path.Count, i + 25) && !tooClose; j++) {
                            if (Flat(path[j].pos - e).magnitude < path[j].width * 0.5f) { tooClose = true; }
                        }
                        if (tooClose) { continue; }
                    }
                    edges[s].Add(e);
                    edgeIdx[s].Add(i);
                }
                Vector3 start = edges[s][0];
                for (int i = 1; i < edges[s].Count; i++) {
                    if (Flat(edges[s][i] - start).magnitude >= length || i == edges[s].Count - 1) {
                        Fence(start, edges[s][i]);
                        start = edges[s][i];
                    }
                }
            }
            // tampas no começo e no fim (ligando a borda esquerda à direita)
            for (int end = 0; end < 2; end++) {
                Vector3 a = end == 0 ? edges[0][0] : edges[0][edges[0].Count - 1];
                Vector3 b = end == 0 ? edges[1][0] : edges[1][edges[1].Count - 1];
                int pieces = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a).magnitude / length));
                for (int k = 0; k < pieces; k++) { Fence(Vector3.Lerp(a, b, k / (float)pieces), Vector3.Lerp(a, b, (k + 1) / (float)pieces)); }
            }

            // PAREDES INVISÍVEIS CONTÍNUAS seguindo a borda (não dependem das cercas): impossível sair da trilha
            var walls = new GameObject("Paredes da trilha (invisíveis)").transform;
            void Wall(Vector3 a, Vector3 b)
            {
                Vector3 d = Flat(b - a);
                if (d.sqrMagnitude < 0.01f) { return; }
                var w = new GameObject("Parede");
                w.layer = 2; // Ignore Raycast: a câmera não bate nelas
                w.transform.SetParent(walls, false);
                w.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 25f, Quaternion.LookRotation(d.normalized));
                w.AddComponent<BoxCollider>().size = new Vector3(1.4f, 60f, d.magnitude + 0.8f);
            }
            for (int s = 0; s < 2; s++) {
                var list = edges[s];
                for (int i = 0; i < list.Count - 1; i += 3) {
                    int j = Mathf.Min(list.Count - 1, i + 3);
                    // um pouco para fora da cerca, para não roçar no pinguim
                    Vector3 outA = list[i] + Flat(list[i] - path[edgeIdx[s][i]].pos).normalized * 0.4f;
                    Vector3 outB = list[j] + Flat(list[j] - path[edgeIdx[s][j]].pos).normalized * 0.4f;
                    outA.y = Mathf.Min(outA.y, outB.y); outB.y = outA.y;
                    Wall(outA, outB);
                }
            }
            for (int end = 0; end < 2; end++) {
                Vector3 a = end == 0 ? edges[0][0] : edges[0][edges[0].Count - 1];
                Vector3 b = end == 0 ? edges[1][0] : edges[1][edges[1].Count - 1];
                int idx = end == 0 ? edgeIdx[0][0] : edgeIdx[0][edgeIdx[0].Count - 1];
                Vector3 back = (end == 0 ? -1f : 1f) * path[idx].tangent * 1.2f;
                Wall(a + back, b + back);
            }
            return count;
        }

        // ------------------------------------------------------------------ luz

        private static void ApplyLighting(string name, MapStyle s)
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) {
                if (light.type != LightType.Directional) { continue; }
                light.color = s.LightColor; light.intensity = s.LightIntensity;
                light.transform.rotation = Quaternion.Euler(s.LightEuler);
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = s.Ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = s.FogDensity;
            RenderSettings.fogColor = s.FogColor;

            string path = $"Assets/Gameplay/Materials/Ceu_{name}.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, path); }
            sky.SetColor("_SkyTint", s.SkyTint);
            sky.SetColor("_GroundColor", s.FogColor * 0.6f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetFloat("_Exposure", s.SkyExposure);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
