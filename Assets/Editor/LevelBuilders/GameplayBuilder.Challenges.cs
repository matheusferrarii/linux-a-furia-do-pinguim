using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using TSB = LinuxFuriaPinguim.EditorTools.TrailSceneryBuilder;

namespace LinuxFuriaPinguim.EditorTools
{
    /// <summary>
    /// Parte 2 do gerador: obstáculos e perigos da trilha, biomas (trechos da fase), nevasca e o boss BSOD.
    /// </summary>
    public static partial class GameplayBuilder
    {
        private const string WinterRock = "Assets/GeeZyyGames/Winter/prefab/rock.prefab";
        private const string WinterRockSmall = "Assets/GeeZyyGames/Winter/prefab/rock small.prefab";
        private const string WinterWoodMat = "Assets/GeeZyyGames/Winter/materials/wood.mat";
        private const string DesertGate = "Assets/GeeZyyGames/Desert/prefab/gate.prefab";
        private const string PuffMat = Demo + "Effects/PuffParticle.mat";
        private const string EnemyProjectile = Demo + "Environment/Projectiles/Enemy Projectile.prefab";
        private const int IgnoreRaycastLayer = 2;
        private const string DizzyFx = Demo + "Effects/DizzyEffect.prefab";
        private const string AssistSnowballPath = "Assets/Gameplay/Prefabs/BolaDeNeveAssistida.prefab";
        private const float AssistSnowballSpeed = 18f;

        /// <summary>
        /// Cópia da bola de neve do Dypsloom (sem mexer no pacote): mais rápida (18 u/s em vez de 8),
        /// colisão um pouco maior e curva leve até o alvo marcado.
        /// </summary>
        private static GameObject BuildAssistSnowball()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Gameplay/Prefabs")) { AssetDatabase.CreateFolder("Assets/Gameplay", "Prefabs"); }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerSnowball);
            if (source == null) { return null; }
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(tmp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            tmp.name = "BolaDeNeveAssistida";
            var proj = tmp.GetComponent<Dypsloom.DypThePenguin.Scripts.Damage.Projectile>();
            if (proj != null) {
                var so = new SerializedObject(proj);
                so.FindProperty("m_Speed").floatValue = AssistSnowballSpeed;
                so.FindProperty("m_LifeTime").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var sphere = tmp.GetComponent<SphereCollider>();
            if (sphere != null) { sphere.radius *= 1.5f; }
            if (tmp.GetComponent<HomingSnowball>() == null) { tmp.AddComponent<HomingSnowball>(); }
            var saved = PrefabUtility.SaveAsPrefabAsset(tmp, AssistSnowballPath);
            Object.DestroyImmediate(tmp);
            return saved;
        }

        private static void CombatHint(Transform parent, int k, string key, string message)
        {
            k = Mathf.Clamp(k, 2, s_MaxStep - 2);
            var t = Across(parent, "Dica de combate", k, out float width);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.up * 3f;
            box.size = new Vector3(width + 4f, 8f, 3f);
            t.gameObject.AddComponent<HintTrigger>().Setup(key, message);
        }

        private static readonly List<(string name, Vector3 pos, float yaw)> s_ExtraShots = new List<(string, Vector3, float)>();

        private sealed class ExtraMats { public Material Snow, Water, Ice, Wood, SnowCap, Shadow, Stone, BossBody, BossFrozen, BossScreen, Wave, ArenaFloor; }

        // ------------------------------------------------------------------ materiais extras

        private static ExtraMats BuildExtraMaterials()
        {
            var standard = Shader.Find("Standard");
            Material Make(string name, Material from, Shader shader, Color color, Color emission, float gloss = 0.5f, bool transparent = false)
            {
                string path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) {
                    mat = from != null ? new Material(from) : new Material(shader ?? standard);
                    AssetDatabase.CreateAsset(mat, path);
                }
                if (mat.HasProperty("_Color")) { mat.SetColor("_Color", color); }
                if (mat.HasProperty("_Glossiness")) { mat.SetFloat("_Glossiness", gloss); }
                if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor")) {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
                if (transparent && mat.shader == standard) {
                    // Standard em modo Fade
                    mat.SetFloat("_Mode", 2f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;
                }
                EditorUtility.SetDirty(mat);
                return mat;
            }

            var puff = AssetDatabase.LoadAssetAtPath<Material>(PuffMat);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WinterWoodMat);
            var sprite = Shader.Find("Sprites/Default");
            var m = new ExtraMats {
                Snow = Make("NeveParticula", puff, Shader.Find("Particles/Standard Unlit"), Color.white, Color.black),
                Water = Make("AguaGelada", null, standard, new Color(0.05f, 0.22f, 0.42f), new Color(0.0f, 0.08f, 0.2f), 0.97f),
                Ice = Make("GeloEscorregadio", null, standard, new Color(0.45f, 0.8f, 1f, 0.9f), new Color(0.08f, 0.28f, 0.42f), 0.98f, transparent: true),
                Wood = wood != null ? wood : Make("Madeira", null, standard, new Color(0.45f, 0.28f, 0.18f), Color.black, 0.2f),
                SnowCap = Make("NeveTopo", null, standard, new Color(0.95f, 0.98f, 1f), Color.black, 0.15f),
                Shadow = Make("SombraEstalactite", null, standard, new Color(0.05f, 0.1f, 0.2f, 0.55f), Color.black, 0f, transparent: true),
                Stone = Make("PedraRuinas", null, standard, new Color(0.58f, 0.64f, 0.72f), Color.black, 0.2f),
                BossBody = Make("BSOD_Corpo", null, standard, new Color(0.62f, 0.76f, 0.9f), new Color(0.02f, 0.05f, 0.1f), 0.6f),
                BossFrozen = Make("BSOD_Travado", null, standard, new Color(0f, 0.47f, 0.84f), new Color(0f, 0.35f, 0.9f) * 1.6f, 0.9f),
                BossScreen = Make("BSOD_Tela", null, standard, new Color(0f, 0.47f, 0.84f), new Color(0f, 0.4f, 0.85f) * 1.3f, 0.9f),
                Wave = Make("OndaDeChoque", null, sprite, new Color(0.65f, 0.92f, 1f, 0.9f), Color.black),
                ArenaFloor = Make("ArenaNeve", null, standard, new Color(0.86f, 0.95f, 1f), new Color(0.02f, 0.05f, 0.08f), 0.35f),
            };
            AssetDatabase.SaveAssets();
            return m;
        }

        // ------------------------------------------------------------------ obstáculos e biomas

        private static void PlaceChallenges(List<int> busy, Mats mats, ExtraMats x)
        {
            var root = Group("Obstaculos e perigos");
            var log = TSB.s_Log;
            int K(float f) => Mathf.Clamp(Mathf.RoundToInt(f * s_MaxStep), 8, s_MaxStep - 8);
            var used = new List<int>(busy);

            // Procura, perto do ponto desejado, o trecho mais estreito livre (obstáculo atravessa o corredor todo).
            int Spot(float f, float maxHalfWidth = 18f)
            {
                int target = K(f), best = -1; float bestW = float.MaxValue;
                for (int k = target - 25; k <= target + 25; k++) {
                    if (k < 8 || k > s_MaxStep - 8 || used.Any(u => Mathf.Abs(u - k) < 9)) { continue; }
                    var (l, r) = Cross(k);
                    float w = (l + r) * 0.5f;
                    if (WallDist(k) < 2.5f || w > maxHalfWidth) { continue; }
                    // não coloca obstáculo em rampa (galho/tronco em subida podem prender o pinguim)
                    if (Mathf.Abs(Point(k - 5).y - Point(k + 5).y) > 2.2f) { continue; }
                    float score = w + Mathf.Abs(k - target) * 0.05f;
                    if (score < bestW) { bestW = score; best = k; }
                }
                if (best >= 0) { used.Add(best); }
                return best;
            }

            int logs = 0, branches = 0, cracks = 0, ice = 0, icicles = 0;
            // Listas por fase (a fase 2 tem bem mais obstáculos)
            foreach (float f in Ph.Ice) { int k = Spot(f, 20f); if (k >= 0 && CreateIcePatch(root, k, x)) { ice++; } }
            foreach (float f in Ph.Cracks) { int k = Spot(f); if (k >= 0 && CreateCrevasse(root, k, x)) { cracks++; } }
            foreach (float f in Ph.Icicles) { int k = Spot(f, 30f); if (k >= 0 && CreateFallingIce(root, k, x)) { icicles++; } }
            foreach (float f in Ph.Logs) { int k = Spot(f); if (k >= 0 && CreateLog(root, k, x)) { logs++; } }
            foreach (float f in Ph.Branches) { int k = Spot(f, 15f); if (k >= 0 && CreateBranch(root, k, x)) { branches++; } }

            // Divisas entre biomas
            var areas = Ph.Areas;
            for (int i = 1; i < areas.Length; i++) {
                int k = K(areas[i].start);
                CreateAreaGate(root, k, areas[i].name, areas[i].blizzard, areas[i - 1].name, areas[i - 1].blizzard);
                if (areas[i].name == "Ruínas Soterradas") { CreateRuinArch(root, k + 4, x); }
            }
            log.AppendLine($"Obstáculos: {logs} troncos, {branches} galhos baixos, {cracks} fendas de água, {ice} placas de gelo, {icicles} estalactites");
        }

        /// <summary>Largura real da trilha naquele ponto: anda para os dois lados até sair da área andável.</summary>
        private static (float left, float right) Cross(int k)
        {
            Vector3 c = TSB.CellCenter(s_Center[Mathf.Clamp(k, 0, s_MaxStep)]);
            Vector3 p = Perp(k);
            float r = 0f, l = 0f;
            for (float s = 0.5f; s < 40f; s += 0.5f) { if (!TSB.IsWalk(c + p * s)) { break; } r = s; }
            for (float s = 0.5f; s < 40f; s += 0.5f) { if (!TSB.IsWalk(c - p * s)) { break; } l = s; }
            return (l, r);
        }

        private static Transform Across(Transform parent, string name, int k, out float width)
        {
            var (l, r) = Cross(k);
            width = Mathf.Min(l + r + 3f, 50f);   // passa um pouco das bordas (encosta nas barreiras)
            Vector3 center = Point(k) + Perp(k) * ((r - l) * 0.5f);
            if (TSB.GroundAt(center, out var g)) { center = g; }
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.SetPositionAndRotation(center, Quaternion.LookRotation(Dir(k)));
            return go;
        }

        private static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Quaternion? rot = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider) { Object.DestroyImmediate(go.GetComponent<Collider>()); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            if (mat != null) { go.GetComponent<Renderer>().sharedMaterial = mat; }
            return go;
        }

        /// <summary>Tronco caído atravessando a trilha: precisa pular.</summary>
        private static bool CreateLog(Transform parent, int k, ExtraMats x)
        {
            var t = Across(parent, "Tronco caído (pule)", k, out float width);
            float d = P * 0.42f;
            var side = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, d * 0.5f, 0), new Vector3(d, width * 0.5f, d), x.Wood, side);
            Prim(PrimitiveType.Cube, t, new Vector3(0, d * 0.95f, 0), new Vector3(width * 0.96f, d * 0.22f, d * 0.75f), x.SnowCap);
            for (int s = -1; s <= 1; s += 2) {   // pontas cortadas
                Prim(PrimitiveType.Cylinder, t, new Vector3(s * width * 0.5f, d * 0.5f, 0), new Vector3(d * 0.92f, 0.04f, d * 0.92f), x.SnowCap, side);
            }
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, d * 0.5f, 0);
            box.size = new Vector3(width, d, d);
            AddHint(t, "tronco", "Tronco no caminho! Pule com Espaço", width, 6f);
            s_Shots.Add(("tronco_" + k, k));
            return true;
        }

        /// <summary>Galho baixo: só passa deslizando de barriga (por cima é bloqueado).</summary>
        private static bool CreateBranch(Transform parent, int k, ExtraMats x)
        {
            var t = Across(parent, "Galho baixo (deslize)", k, out float width);
            float clearance = P * 0.55f;   // em pé não passa (altura P); deslizando (0,4·P) passa
            float d = P * 0.3f;
            var side = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0, clearance + d * 0.5f, 0), new Vector3(d, width * 0.5f, d), x.Wood, side);
            Prim(PrimitiveType.Cube, t, new Vector3(0, clearance + d * 0.95f, 0), new Vector3(width * 0.95f, d * 0.3f, d * 0.8f), x.SnowCap);
            for (int s = -1; s <= 1; s += 2) {   // troncos de apoio nas bordas
                Prim(PrimitiveType.Cylinder, t, new Vector3(s * (width * 0.5f + 0.2f), P * 1.6f, 0), new Vector3(d * 1.6f, P * 1.6f, d * 1.6f), x.Wood);
            }
            // folhas nevadas penduradas
            for (float u = -width * 0.4f; u <= width * 0.4f; u += 2.2f) {
                Prim(PrimitiveType.Sphere, t, new Vector3(u, clearance + d * 1.2f, 0), new Vector3(1.4f, 0.6f, 1.1f), x.SnowCap);
            }
            var beam = t.gameObject.AddComponent<BoxCollider>();
            beam.center = new Vector3(0, clearance + d * 0.5f, 0);
            beam.size = new Vector3(width, d, d);
            // parede invisível acima do galho: não dá para pular por cima
            var wall = new GameObject("Bloqueio acima do galho");
            wall.layer = IgnoreRaycastLayer;
            wall.transform.SetParent(t, false);
            var wb = wall.AddComponent<BoxCollider>();
            wb.center = new Vector3(0, clearance + d + 20f, 0);
            wb.size = new Vector3(width, 40f, d);
            AddHint(t, "galho", "Galho baixo! Deslize com Ctrl para passar", width, 7f);
            s_Shots.Add(("galho_" + k, k));
            return true;
        }

        /// <summary>Fenda de água gelada: cair = perder vida. Pule!</summary>
        private static bool CreateCrevasse(Transform parent, int k, ExtraMats x)
        {
            var t = Across(parent, "Fenda de água gelada (pule)", k, out float width);
            float depth = P * 1.0f;
            Prim(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0), new Vector3(width, 0.08f, depth), x.Water);
            // bordas de gelo quebrado
            for (int s = -1; s <= 1; s += 2) {
                for (float u = -width * 0.48f; u <= width * 0.48f; u += 1.3f) {
                    var shard = Prim(PrimitiveType.Cube, t, new Vector3(u, 0.12f, s * depth * 0.5f), new Vector3(1.1f, 0.25f, 0.4f), x.SnowCap,
                        Quaternion.Euler(0f, (u * 37f) % 25f - 12f, (u * 53f) % 14f - 7f));
                    shard.name = "Borda";
                }
            }
            var trigger = t.gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0, 0.6f, 0);
            trigger.size = new Vector3(width, 1.2f, depth * 0.8f);
            t.gameObject.AddComponent<WaterHazard>().Setup(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPuff));
            s_Shots.Add(("fenda_" + k, k));
            return true;
        }

        /// <summary>Placa de gelo escorregadio cobrindo a largura da trilha.</summary>
        private static bool CreateIcePatch(Transform parent, int k, ExtraMats x)
        {
            var t = Across(parent, "Gelo escorregadio", k, out float width);
            float length = P * 6f;
            Prim(PrimitiveType.Cube, t, new Vector3(0, 0.04f, 0), new Vector3(width, 0.06f, length), x.Ice);
            var zone = t.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0, 1.5f, 0);
            zone.size = new Vector3(width, 3f, length);
            t.gameObject.AddComponent<IceZone>();
            s_Shots.Add(("gelo_" + k, k));
            return true;
        }

        /// <summary>Estalactite que cai do alto (Ruínas Soterradas).</summary>
        private static bool CreateFallingIce(Transform parent, int k, ExtraMats x)
        {
            Vector3 c = Point(k) + Perp(k) * (Mathf.Sin(k) * Mathf.Min(2f, WallDist(k) - 2f));
            if (!TSB.GroundAt(c, out c)) { return false; }
            var t = new GameObject("Estalactite").transform;
            t.SetParent(parent, false);
            t.position = c;
            var shadow = Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, 0), new Vector3(P * 1.4f, 0.02f, P * 1.4f), x.Shadow);
            shadow.name = "Sombra";
            var iceHolder = new GameObject("Gelo").transform;
            iceHolder.SetParent(t, false);
            var crystal = Model(CrystalModel, iceHolder, P * 1.3f, out _);
            if (crystal != null) {
                iceHolder.localRotation = Quaternion.Euler(180f, 0f, 0f); // ponta para baixo
                iceHolder.localPosition = Vector3.up * (P * 1.3f);
            }
            var fall = t.gameObject.AddComponent<FallingIce>();
            fall.Setup(iceHolder, shadow.transform, AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPuff), P * 0.75f);
            AddHint(t, "estalactite", "Estalactites! Fuja das sombras no chão", 12f, 12f);
            s_Shots.Add(("estalactite_" + k, k));
            return true;
        }

        private static void CreateAreaGate(Transform parent, int k, string ahead, bool blizAhead, string behind, bool blizBehind)
        {
            var t = Across(parent, "Divisa: " + ahead, k, out float width);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, 10f, 0);
            box.size = new Vector3(width + 30f, 40f, 2f);
            t.gameObject.AddComponent<AreaGate>().Setup(ahead, blizAhead, behind, blizBehind);
        }

        /// <summary>Arco em ruínas na entrada das Ruínas Soterradas (portão antigo, pedra fria).</summary>
        private static void CreateRuinArch(Transform parent, int k, ExtraMats x)
        {
            var t = Across(parent, "Arco em ruínas", k, out float width);
            var gate = Model(DesertGate, t, P * 3.2f, out float radius);
            if (gate == null) { return; }
            foreach (var r in gate.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = r.sharedMaterials.Select(_ => x.Stone).ToArray(); }
            // Ajusta a largura do arco à trilha (só visual, sem colisão)
            float w = radius * 2f;
            if (w > 0.1f) { gate.transform.localScale = new Vector3(gate.transform.localScale.x * (width + 2f) / w, gate.transform.localScale.y, gate.transform.localScale.z); }
            s_Shots.Add(("ruinas_" + k, k));
        }

        private static void AddHint(Transform t, string key, string message, float width, float before)
        {
            var hint = new GameObject("Dica");
            hint.transform.SetParent(t, false);
            hint.transform.localPosition = new Vector3(0, 2f, -before);
            var box = hint.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width + 4f, 6f, 3f);
            hint.AddComponent<HintTrigger>().Setup(key, message);
        }

        // ------------------------------------------------------------------ boss

        private static void BuildBossSection(int goalK, Mats mats, ExtraMats x)
        {
            const float R = 22f;
            Vector3 summit = Point(goalK);
            Vector3 center = new Vector3(TSB.s_MinX - 140f, summit.y + 30f, summit.z);
            var arena = Group("Covil do BSOD (arena)");
            arena.position = center;

            // chão circular de neve
            var floor = Prim(PrimitiveType.Cylinder, arena, new Vector3(0, -1f, 0), new Vector3(R * 2f, 1f, R * 2f), x.ArenaFloor);
            floor.name = "Chão";
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            // base rochosa (a arena flutua sobre o abismo)
            var under = Prim(PrimitiveType.Sphere, arena, new Vector3(0, -9f, 0), new Vector3(R * 2.1f, 16f, R * 2.1f), x.Stone);
            under.name = "Base rochosa";

            // borda: cristais + barreira invisível
            for (int i = 0; i < 28; i++) {
                float a = i * Mathf.PI * 2f / 28f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var holder = new GameObject("Cristal da borda " + i).transform;
                holder.SetParent(arena, false);
                holder.localPosition = dir * (R + 1.5f);
                holder.localRotation = Quaternion.Euler(Random(i) * 20f - 10f, i * 47f, Random(i + 7) * 20f - 10f);
                // baixos: a câmera fica atrás do pinguim e não pode ser tapada pela borda
                var edge = Model(CrystalModel, holder, P * (0.6f + Random(i + 3) * 0.7f), out _);
                if (edge != null) { foreach (var r in edge.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.Monster; } }
                var wall = new GameObject("Barreira " + i);
                wall.layer = IgnoreRaycastLayer;
                wall.transform.SetParent(arena, false);
                wall.transform.localPosition = dir * (R + 0.5f) + Vector3.up * 15f;
                wall.transform.localRotation = Quaternion.LookRotation(dir);
                wall.AddComponent<BoxCollider>().size = new Vector3(2f * Mathf.PI * R / 28f + 1.5f, 34f, 1f);
            }
            // picos de gelo gigantes ao fundo
            for (int i = 0; i < 9; i++) {
                float a = i * Mathf.PI * 2f / 9f + 0.3f;
                var holder = new GameObject("Pico " + i).transform;
                holder.SetParent(arena, false);
                holder.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (R + 16f + Random(i) * 8f) + Vector3.down * 6f;
                Model(CrystalModel, holder, P * (4f + Random(i + 11) * 4f), out _);
            }
            var light = new GameObject("Luz do covil").AddComponent<Light>();
            light.transform.SetParent(arena, false);
            light.transform.localPosition = Vector3.up * 14f;
            light.type = LightType.Point; light.color = new Color(0.5f, 0.7f, 1f); light.range = R * 2.2f; light.intensity = 1.6f; light.shadows = LightShadows.None;

            // nascimento na arena e portal da vitória
            var spawn = new GameObject("Renascer na arena").transform;
            spawn.SetParent(arena, false);
            spawn.localPosition = new Vector3(-(R - 11f), 0.3f, 0); // câmera (10 atrás) ainda dentro da arena
            spawn.localRotation = Quaternion.LookRotation(Vector3.right);
            var victory = CreatePortalVisual(arena, center, Quaternion.identity, mats, "Portal da vitória (fim da fase)");
            victory.go.AddComponent<LevelGoal>().Setup(victory.spinner);

            var boss = CreateBoss(arena, center + Vector3.right * 6f, Quaternion.LookRotation(Vector3.left), center, R, mats, x, victory.go);

            // portal no cume que leva até a arena
            var portal = CreatePortalVisual(s_Root, summit, Quaternion.LookRotation(Dir(goalK)), mats, "Portal do cume (leva ao chefe)");
            portal.go.AddComponent<BossPortal>().Setup(spawn, boss, portal.spinner);
            s_Shots.Add(("portal_cume", goalK));
            s_ExtraShots.Add(("arena_bsod", spawn.position, 90f));
            s_ExtraShots.Add(("arena_bsod_perto", center + Vector3.left * 2f, 90f));
        }

        private static BsodBoss CreateBoss(Transform parent, Vector3 pos, Quaternion rot, Vector3 arenaCenter, float arenaR, Mats mats, ExtraMats x, GameObject victoryPortal)
        {
            float H = P * 3.2f * Ph.BossScale;
            var go = new GameObject(Ph.BossName + " (chefe)");
            var bodyMat = BossMaterial("Chefe_" + Ph.BossName.Replace(" ", ""), Ph.BossColor, Ph.BossColor * 0.08f);
            var guardMat = BossMaterial("ChefeGuardaAberta", Ph.GuardColor, Ph.GuardColor * 0.9f);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var solid = go.AddComponent<CapsuleCollider>();
            solid.center = Vector3.up * (H * 0.5f); solid.height = H; solid.radius = H * 0.22f;
            var hitbox = new GameObject("Hitbox").AddComponent<CapsuleCollider>();
            hitbox.transform.SetParent(go.transform, false);
            hitbox.isTrigger = true; hitbox.center = Vector3.up * (H * 0.5f); hitbox.height = H * 1.05f; hitbox.radius = H * 0.3f;

            var body = new GameObject("Corpo").transform;
            body.SetParent(go.transform, false);
            var parts = new List<Renderer>();
            GameObject Part(string path, Vector3 local, float height, Material mat)
            {
                var holder = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path)).transform;
                holder.SetParent(body, false);
                holder.localPosition = local;
                var m = Model(path, holder, height, out _);
                if (m != null) {
                    foreach (var r in m.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray(); parts.Add(r); }
                }
                return holder.gameObject;
            }
            Part(WinterRockSmall, new Vector3(-H * 0.18f, 0, 0), H * 0.28f, bodyMat);   // pernas
            Part(WinterRockSmall, new Vector3(H * 0.18f, 0, 0), H * 0.28f, bodyMat);
            Part(WinterRock, new Vector3(0, H * 0.2f, 0), H * 0.55f, bodyMat);            // tronco
            Part(WinterRock, new Vector3(0, H * 0.68f, 0), H * 0.3f, bodyMat);            // cabeça
            var spikeL = Part(CrystalModel, new Vector3(-H * 0.2f, H * 0.62f, -H * 0.1f), H * 0.35f, mats.Monster);
            spikeL.transform.localRotation = Quaternion.Euler(-20f, 0, 25f);
            var spikeR = Part(CrystalModel, new Vector3(H * 0.2f, H * 0.62f, -H * 0.1f), H * 0.35f, mats.Monster);
            spikeR.transform.localRotation = Quaternion.Euler(-20f, 0, -25f);

            Transform Arm(float side)
            {
                var pivot = new GameObject(side < 0 ? "Braço esquerdo" : "Braço direito").transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(side * H * 0.34f, H * 0.66f, 0);
                var hang = new GameObject("Cristal").transform;
                hang.SetParent(pivot, false);
                hang.localRotation = Quaternion.Euler(180f, 0, 0);   // ponta para baixo
                var m = Model(CrystalModel, hang, H * 0.5f, out _);
                if (m != null) { foreach (var r in m.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats.Monster; parts.Add(r); } }
                return pivot;
            }
            var armL = Arm(-1f);
            var armR = Arm(1f);

            // "tela" no rosto: olhos normais / ":(" quando trava
            var screen = Prim(PrimitiveType.Quad, body, new Vector3(0, H * 0.82f, H * 0.16f), new Vector3(H * 0.34f, H * 0.2f, 1f), x.BossScreen);
            screen.name = "Tela";
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject Face(string name, string text, float size, Vector3 offset)
            {
                var f = new GameObject(name);
                f.transform.SetParent(screen.transform, false);
                f.transform.localPosition = new Vector3(offset.x, offset.y, -0.02f);
                f.transform.localRotation = Quaternion.identity;
                f.transform.localScale = new Vector3(1f / (H * 0.34f), 1f / (H * 0.2f), 1f);
                var tm = f.AddComponent<TextMesh>();
                tm.font = font; tm.text = text; tm.fontSize = 100; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center; tm.color = Color.white;
                f.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return f;
            }
            var faceOk = Face("Olhos", Ph.FaceOk, H * 0.012f, Vector3.zero);
            var faceSad = new GameObject("Tela azul");
            faceSad.transform.SetParent(screen.transform, false);
            Face(":(", Ph.FaceSad, H * 0.02f, new Vector3(0, 0.12f, 0)).transform.SetParent(faceSad.transform, true);
            Face("Erro", Ph.FaceSadText, H * 0.0035f, new Vector3(0, -0.28f, 0)).transform.SetParent(faceSad.transform, true);
            // o quad olha para +Z do boss? Quad padrão é visível de -Z; gira para ficar de frente
            screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // onda de choque (anel)
            var wave = new GameObject("Onda de choque").transform;
            wave.SetParent(go.transform, false);
            var lr = wave.gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 48; lr.widthMultiplier = 0.7f;
            for (int i = 0; i < 48; i++) { float a = i * Mathf.PI * 2f / 48f; lr.SetPosition(i, new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a))); }
            lr.sharedMaterial = x.Wave;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var throwPoint = new GameObject("Mão que arremessa").transform;
            throwPoint.SetParent(go.transform, false);
            throwPoint.localPosition = new Vector3(0, H * 0.7f, H * 0.32f + 1.2f);

            var boss = go.AddComponent<BsodBoss>();
            boss.Setup(body, armL, armR, parts.ToArray(), x.BossFrozen, faceOk, faceSad, wave, throwPoint,
                AssetDatabase.LoadAssetAtPath<GameObject>(EnemyProjectile), AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPuff),
                victoryPortal, arenaCenter, arenaR, H);
            boss.SetDifficulty(Ph.BossCores, Ph.BossStagger, Ph.BossFreeze, Ph.BossSpeed, Ph.BossExtraShards);
            boss.SetIdentity(Ph.BossName, guardMat, Ph.QteLength, Ph.QteTime, Ph.GuardPeriod, Ph.GuardWindow);

            // moldes (desativados) usados pelos ataques: Monstro de Gelo invocado e estalactite da chuva de gelo
            GameObject minion = null;
            if (Ph.Summon && CreateIceMonster(parent, Mathf.Max(10, s_MaxStep - 20), mats, false)) {
                minion = parent.GetChild(parent.childCount - 1).gameObject;
                minion.name = "Molde: monstro invocado";
                minion.transform.position = arenaCenter;
                var mon = minion.GetComponent<IceMonster>();
                if (mon != null) { mon.SetPatrol(arenaCenter + Vector3.left * 5f, arenaCenter + Vector3.right * 5f, Ph.Aggression); }
                minion.SetActive(false);
            }
            GameObject icicle = null;
            if (Ph.IceRain) {
                icicle = new GameObject("Molde: estalactite");
                icicle.transform.SetParent(parent, false);
                var ice = Model(CrystalModel, icicle.transform, P * 1.2f, out _);
                if (ice != null) { ice.transform.localRotation = Quaternion.Euler(180f, 0, 0); ice.transform.localPosition += Vector3.up * P * 1.2f; }
                icicle.SetActive(false);
            }
            boss.SetAttacks(Ph.Slam, Ph.Throw, Ph.Charge, Ph.IceRain, Ph.Summon, minion, icicle, x.Shadow);
            go.AddComponent<Stompable>();
            EditorUtility.SetDirty(boss);
            return boss;
        }

        /// <summary>
        /// Teste de percurso: uma cápsula do tamanho do pinguim anda pela linha central do início ao portal.
        /// Bloqueios que não são obstáculos de propósito (tronco, galho, inimigo, NPC) = bug de fase.
        /// </summary>
        internal static int WalkTest(PenguinCharacter player)
        {
            var cc = player.GetComponent<CharacterController>();
            float radius = cc != null ? cc.radius * player.transform.lossyScale.x * 0.9f : 0.5f;
            float height = cc != null ? cc.height * player.transform.lossyScale.y : P;
            int blocks = 0;
            for (int k = 6; k < s_MaxStep - 6; k += 3) {
                Vector3 a = Point(k), b = Point(k + 3);
                Vector3 dir = b - a;
                float dist = dir.magnitude;
                if (dist < 0.01f) { continue; }
                Vector3 p1 = a + Vector3.up * (radius + 0.45f), p2 = a + Vector3.up * Mathf.Max(radius + 0.45f, height - radius);
                var hits = Physics.CapsuleCastAll(p1, p2, radius, dir / dist, dist, ~0, QueryTriggerInteraction.Ignore);
                foreach (var h in hits) {
                    var t = h.collider.transform;
                    if (t.IsChildOf(player.transform)) { continue; }
                    string n = t.name + "|" + (t.parent != null ? t.parent.name : "");
                    if (n.Contains("Tronco") || n.Contains("Galho") || n.Contains("galho") || n.Contains("NPC") || n.Contains("Modelo")) { continue; }
                    if (h.collider.GetComponentInParent<Stompable>() != null) { continue; }
                    if (h.collider is TerrainCollider) { continue; }                  // chão/rampa do terreno
                    if (Vector3.Dot(h.normal, Vector3.up) > 0.6f) { continue; }        // piso
                    blocks++;
                    TSB.s_Log.AppendLine($"  BLOQUEIO no passo {k}: {t.name} ({(t.parent != null ? t.parent.name : "-")}) em {h.point}");
                    break;
                }
            }
            TSB.s_Log.AppendLine(blocks == 0 ? "Teste de percurso: OK, caminho livre do início ao chefe" : $"Teste de percurso: {blocks} bloqueio(s)");
            return blocks;
        }

        private static Material BossMaterial(string name, Color color, Color emission)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_Color", color);
            mat.SetFloat("_Glossiness", 0.55f);
            if (emission.maxColorComponent > 0.001f) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emission); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static float Random(int seed)
        {
            uint h = (uint)(seed * 2654435761u);
            h ^= h >> 13; h *= 1274126177u; h ^= h >> 16;
            return (h & 0xFFFF) / 65535f;
        }
    }
}
