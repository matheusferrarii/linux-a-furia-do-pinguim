using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// HUD da fase, montado por código (não precisa editar a cena):
    /// corações, vidas, peixes, peixes dourados, missões, avisos, diálogo, Game Over e fim de fase.
    /// Visual: texto com contorno + sombra sobre sombreados suaves (sem caixas sólidas tapando a tela).
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private static readonly Color Ice = new Color32(232, 247, 255, 255);
        private static readonly Color IceAccent = new Color32(140, 224, 255, 255);
        private static readonly Color Shade = new Color(0.01f, 0.04f, 0.09f, 1f);
        private static readonly Color Gold = new Color32(255, 205, 60, 255);
        private static readonly Color Heart = new Color32(255, 84, 104, 255);
        private static readonly Color HeartEmpty = new Color(0.08f, 0.1f, 0.16f, 0.55f);
        private static readonly Color Done = new Color32(140, 230, 150, 255);
        private static readonly Color BossBlue = new Color32(120, 170, 255, 255);

        private enum Look { None, Soft, Card }

        /// <summary>Linha de ícones (corações, núcleos do chefe) desenhados com sprite — não depende de símbolos da fonte.</summary>
        private sealed class IconRow
        {
            public RectTransform root;
            public Sprite sprite;
            public float size, gap, rotation;
            public bool centered;
            public readonly List<Image> icons = new List<Image>();
            public int lastValue = -1;
        }

        private GameSession m_Session;
        private Font m_TitleFont;
        private Font m_BodyFont;
        private Sprite m_ButtonSprite;

        private Canvas m_Canvas;
        private IconRow m_HeartRow;
        private Text m_Lives, m_Coins, m_Rare;
        private Text m_Missions;
        private Text m_Toast;
        private CanvasGroup m_ToastGroup;
        private readonly Queue<string> m_ToastQueue = new Queue<string>();
        private bool m_ToastRunning;
        private GameObject m_PromptRoot; private Text m_Prompt; private object m_PromptOwner;
        private GameObject m_DialogRoot; private Text m_DialogName, m_DialogText, m_DialogHint;
        private GameObject m_GameOverRoot;
        private GameObject m_CompleteRoot; private Text m_CompleteSummary;
        private Text m_AreaTitle; private CanvasGroup m_AreaGroup; private Coroutine m_AreaRoutine;
        private GameObject m_BossRoot; private Text m_BossName; private Image m_BossStagger; private IconRow m_BossCores;

        private RectTransform m_Reticle;
        private List<Image> m_ReticleParts;
        private IconRow m_ReticleHearts;
        private Text m_ReticleHint;
        private CombatTargeting m_Targeting;
        private float m_ReticleSpin;

        public bool IsDialogOpen => m_DialogRoot != null && m_DialogRoot.activeSelf;

#if UNITY_EDITOR
        /// <summary>Só para conferir o visual no editor (screenshot do HUD sem dar Play).</summary>
        public void EditorPreview(Camera cam, string speaker, string text, Vector2 reticleScreen)
        {
            m_Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            m_Canvas.worldCamera = cam;
            m_Canvas.planeDistance = 1f;
            m_DialogRoot.SetActive(true);
            m_DialogName.text = speaker; m_DialogText.text = text;
            SetRow(m_HeartRow, 2, 3, Heart, HeartEmpty);
            m_Lives.text = "Vidas  x3"; m_Coins.text = "Peixes  42"; m_Rare.text = "Dourados  1/4";
            m_Missions.text = MissionLine(true, false, "Chegue ao topo da Montanha Ancestral", "") + "\n" +
                              MissionLine(false, false, "Colete 200 peixes", "42/200") + "\n" +
                              MissionLine(true, true, "Fale com o Ancião Tux", "");
            m_Reticle.gameObject.SetActive(true);
            m_Reticle.anchoredPosition = reticleScreen;
            foreach (var img in m_ReticleParts) { img.color = Gold; }
            SetRow(m_ReticleHearts, 1, 2, Heart, HeartEmpty);
            m_ReticleHint.text = "segure botão direito: travar";
        }
#endif

        /// <summary>Retícula sobre o inimigo marcado: amarela = marcado, vermelha = câmera travada (botão direito).</summary>
        private void LateUpdate()
        {
            if (m_Reticle == null) { return; }
            var player = m_Session != null ? m_Session.Player : null;
            if (m_Targeting == null && player != null) { m_Targeting = player.GetComponent<CombatTargeting>(); }
            var cam = Camera.main;
            var target = m_Targeting != null ? m_Targeting.Target : null;
            if (target == null || cam == null || IsDialogOpen) { m_Reticle.gameObject.SetActive(false); return; }

            Vector3 sp = cam.WorldToScreenPoint(target.bounds.center);
            if (sp.z <= 0f) { m_Reticle.gameObject.SetActive(false); return; }
            m_Reticle.gameObject.SetActive(true);
            float scale = m_Canvas.scaleFactor > 0f ? m_Canvas.scaleFactor : 1f;
            m_Reticle.anchoredPosition = new Vector2(sp.x, sp.y) / scale;

            bool locked = m_Targeting.HardLock;
            var boss = target.GetComponentInParent<BsodBoss>();
            m_ReticleSpin += Time.unscaledDeltaTime * (locked ? 0f : 40f);
            m_Reticle.localRotation = Quaternion.Euler(0, 0, locked ? 45f : Mathf.Sin(m_ReticleSpin * 0.05f) * 6f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f;
            m_Reticle.localScale = Vector3.one * (locked ? 0.8f : pulse);
            Color c = locked ? new Color32(255, 90, 90, 255) : Gold;
            if (boss != null && boss.Active && !boss.IsDead) {
                // Chefe: a mira encolhe até a guarda abrir. Verde = ATAQUE AGORA; cinza = ele defende.
                bool open = boss.GuardOpen;
                float toOpen = Mathf.Clamp01((1f - boss.GuardOpenFraction - boss.GuardCycle) / Mathf.Max(0.01f, 1f - boss.GuardOpenFraction));
                m_Reticle.localScale = Vector3.one * (open ? 1.05f : 1f + toOpen * 1.2f);
                c = boss.IsFrozen ? new Color32(120, 200, 255, 255) : open ? new Color32(80, 240, 120, 255) : new Color32(170, 170, 185, 255);
            }
            foreach (var img in m_ReticleParts) { img.color = c; }

            // corações do inimigo e dica (sempre de pé, mesmo com a mira girando)
            m_ReticleHearts.root.localRotation = Quaternion.Inverse(m_Reticle.localRotation);
            m_ReticleHint.rectTransform.localRotation = Quaternion.Inverse(m_Reticle.localRotation);
            var dmg = m_Targeting.TargetDamageable;
            if (dmg != null && dmg.MaxHp <= 10) { SetRow(m_ReticleHearts, dmg.CurrentHp, dmg.MaxHp, Heart, HeartEmpty); }
            else { SetRow(m_ReticleHearts, 0, 0, Heart, HeartEmpty); }
            m_ReticleHint.text = boss != null && boss.Active && !boss.IsDead
                ? (boss.IsFrozen ? "TRAVADO!" : boss.GuardOpen ? "ATAQUE AGORA!" : "espere a guarda abrir...")
                : locked ? "câmera travada" : "segure botão direito: travar";
        }

        public void Init(GameSession session, Font titleFont, Font bodyFont, Sprite panel, Sprite button)
        {
            m_Session = session;
            m_BodyFont = bodyFont != null ? bodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_TitleFont = titleFont != null ? titleFont : m_BodyFont;
            m_ButtonSprite = button;
            Build();
            session.Changed += RefreshStats;
            session.MissionsChanged += RefreshMissions;
        }

        // ------------------------------------------------------------------ montagem

        private void Build()
        {
            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            m_Canvas = canvasGo.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null) {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
            var root = canvasGo.transform;

            // Status (canto superior esquerdo): corações desenhados + números, sem caixa.
            var stats = Panel(root, "Status", new Vector2(0, 1), new Vector2(40, -34), new Vector2(360, 196), look: Look.Soft);
            var hearts = Panel(stats, "Coracoes", new Vector2(0, 1), new Vector2(6, -6), new Vector2(330, 58), new Vector2(0, 1));
            m_HeartRow = new IconRow { root = hearts, sprite = HeartSprite(), size = 52f, gap = 10f };
            m_Lives = Bold(Label(stats, "Vidas", new Vector2(8, -74), new Vector2(340, 40), 31, Ice, m_BodyFont));
            m_Coins = Bold(Label(stats, "Peixes", new Vector2(8, -114), new Vector2(340, 40), 31, IceAccent, m_BodyFont));
            m_Rare = Bold(Label(stats, "Dourados", new Vector2(8, -154), new Vector2(340, 40), 31, Gold, m_BodyFont));

            // Missões (canto superior direito)
            var missions = Panel(root, "Missoes", new Vector2(1, 1), new Vector2(-40, -34), new Vector2(560, 280), look: Look.Soft);
            Label(missions, "Titulo", new Vector2(4, -2), new Vector2(540, 52), 42, IceAccent, m_TitleFont).text = "MISSÕES";
            m_Missions = Bold(Label(missions, "Lista", new Vector2(6, -58), new Vector2(548, 220), 26, Ice, m_BodyFont));
            m_Missions.supportRichText = true;
            m_Missions.lineSpacing = 1.15f;
            m_Missions.verticalOverflow = VerticalWrapMode.Overflow;

            // Aviso (centro superior)
            var toastGo = new GameObject("Aviso", typeof(RectTransform));
            toastGo.transform.SetParent(root, false);
            var trt = (RectTransform)toastGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -240); trt.sizeDelta = new Vector2(1100, 70);
            m_ToastGroup = toastGo.AddComponent<CanvasGroup>(); m_ToastGroup.alpha = 0f;
            m_Toast = Bold(Label(toastGo.transform, "Texto", Vector2.zero, new Vector2(1100, 70), 38, Ice, m_BodyFont, TextAnchor.MiddleCenter));
            Stretch(m_Toast.rectTransform);

            // Nome do lugar ao entrar num bioma (centro)
            var areaGo = new GameObject("NomeDoLugar", typeof(RectTransform));
            areaGo.transform.SetParent(root, false);
            var art = (RectTransform)areaGo.transform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.68f); art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(1400, 150);
            m_AreaGroup = areaGo.AddComponent<CanvasGroup>(); m_AreaGroup.alpha = 0f;
            m_AreaTitle = Label(areaGo.transform, "Texto", Vector2.zero, new Vector2(1400, 150), 104, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Stretch(m_AreaTitle.rectTransform);
            var aOutline = m_AreaTitle.GetComponent<Outline>(); aOutline.effectColor = new Color(0.02f, 0.1f, 0.2f, 0.95f); aOutline.effectDistance = new Vector2(3, -3);

            // Barra do boss (topo, centro)
            var boss = Panel(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(900, 118), new Vector2(0.5f, 1f), Look.Soft);
            m_BossRoot = boss.gameObject;
            m_BossName = Label(boss, "Nome", new Vector2(24, -4), new Vector2(560, 60), 48, BossBlue, m_TitleFont);
            Bold(Label(boss, "Nucleos", new Vector2(560, -16), new Vector2(130, 40), 24, Ice, m_BodyFont, TextAnchor.MiddleRight)).text = "NÚCLEOS";
            var cores = Panel(boss, "Icones", new Vector2(0, 1), new Vector2(706, -16), new Vector2(190, 40), new Vector2(0, 1));
            m_BossCores = new IconRow { root = cores, sprite = null, size = 24f, gap = 14f, rotation = 45f };
            var barBack = new GameObject("BarraFundo", typeof(RectTransform), typeof(Image));
            barBack.transform.SetParent(boss, false);
            var bb = (RectTransform)barBack.transform;
            bb.anchorMin = bb.anchorMax = new Vector2(0, 1); bb.pivot = new Vector2(0, 1);
            bb.anchoredPosition = new Vector2(24, -72); bb.sizeDelta = new Vector2(852, 24);
            barBack.GetComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            var bbo = barBack.AddComponent<Outline>(); bbo.effectColor = new Color(IceAccent.r, IceAccent.g, IceAccent.b, 0.5f); bbo.useGraphicAlpha = false;
            var fill = new GameObject("Travamento", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barBack.transform, false);
            Stretch((RectTransform)fill.transform, 3f);
            m_BossStagger = fill.GetComponent<Image>();
            m_BossStagger.color = new Color32(0, 120, 215, 255); // azul da "tela azul"
            var fillRt = (RectTransform)fill.transform;
            fillRt.pivot = new Vector2(0f, 0.5f);
            m_BossRoot.SetActive(false);

            // Retícula do alvo marcado (cantos) + vida do inimigo
            m_Reticle = new GameObject("Mira", typeof(RectTransform)).GetComponent<RectTransform>();
            m_Reticle.SetParent(root, false);
            m_Reticle.anchorMin = m_Reticle.anchorMax = Vector2.zero;
            m_Reticle.sizeDelta = new Vector2(110, 110);
            m_ReticleParts = new List<Image>();
            for (int c = 0; c < 4; c++) {
                float sx = c % 2 == 0 ? -1 : 1, sy = c < 2 ? 1 : -1;
                for (int bar = 0; bar < 2; bar++) {
                    var img = new GameObject("Canto", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    img.transform.SetParent(m_Reticle, false);
                    var rt = img.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = bar == 0 ? new Vector2(26, 6) : new Vector2(6, 26);
                    rt.anchoredPosition = new Vector2(sx * (55 - (bar == 0 ? 13 : 3)), sy * (55 - (bar == 0 ? 3 : 13)));
                    img.raycastTarget = false;
                    var o = img.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.7f);
                    m_ReticleParts.Add(img);
                }
            }
            var rh = new GameObject("Vida", typeof(RectTransform)).GetComponent<RectTransform>();
            rh.SetParent(m_Reticle, false);
            Center(rh, new Vector2(0, -80)); rh.sizeDelta = new Vector2(220, 34);
            m_ReticleHearts = new IconRow { root = rh, sprite = HeartSprite(), size = 28f, gap = 6f, centered = true };
            m_ReticleHint = Bold(Label(m_Reticle, "Dica", Vector2.zero, new Vector2(360, 32), 22, Ice, m_BodyFont, TextAnchor.MiddleCenter));
            Center(m_ReticleHint.rectTransform, new Vector2(0, -112));
            m_Reticle.gameObject.SetActive(false);

            // Prompt de interação (centro inferior)
            m_PromptRoot = Panel(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0, 240), new Vector2(620, 64), new Vector2(0.5f, 0f), Look.Soft).gameObject;
            m_Prompt = Bold(Label(m_PromptRoot.transform, "Texto", Vector2.zero, new Vector2(620, 64), 30, Ice, m_BodyFont, TextAnchor.MiddleCenter));
            Stretch(m_Prompt.rectTransform);
            m_PromptRoot.SetActive(false);

            // Diálogo (inferior): faixa com degradê que some para cima, estilo legenda de cinema — sem caixa.
            var dialog = new GameObject("Dialogo", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            dialog.SetParent(root, false);
            dialog.anchorMin = new Vector2(0f, 0f); dialog.anchorMax = new Vector2(1f, 0f); dialog.pivot = new Vector2(0.5f, 0f);
            dialog.offsetMin = Vector2.zero; dialog.offsetMax = new Vector2(0f, 380f);
            var band = dialog.GetComponent<Image>();
            band.sprite = FadeSprite(); band.color = new Color(Shade.r, Shade.g, Shade.b, 0.72f); band.raycastTarget = false;
            m_DialogRoot = dialog.gameObject;
            var content = Panel(dialog, "Conteudo", new Vector2(0.5f, 0f), new Vector2(0, 34), new Vector2(1400, 270), new Vector2(0.5f, 0f));
            m_DialogName = Label(content, "Nome", new Vector2(0, -4), new Vector2(900, 60), 50, Gold, m_TitleFont);
            var line = new GameObject("Linha", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            line.transform.SetParent(content, false);
            var lrt = line.rectTransform; lrt.anchorMin = lrt.anchorMax = new Vector2(0, 1); lrt.pivot = new Vector2(0, 1);
            lrt.anchoredPosition = new Vector2(2, -66); lrt.sizeDelta = new Vector2(320, 4);
            line.sprite = FadeSprite(horizontal: true); line.color = new Color(Gold.r, Gold.g, Gold.b, 0.9f); line.raycastTarget = false;
            m_DialogText = Bold(Label(content, "Fala", new Vector2(0, -84), new Vector2(1400, 150), 36, Color.white, m_BodyFont));
            m_DialogText.lineSpacing = 1.15f;
            m_DialogHint = Bold(Label(content, "Dica", new Vector2(1000, -232), new Vector2(400, 36), 25, IceAccent, m_BodyFont, TextAnchor.MiddleRight));
            m_DialogHint.text = "[E] continuar  »";
            m_DialogRoot.SetActive(false);

            // Game Over
            var go = FullPanel(root, "GameOver", "FIM DE JOGO", "O BSOD congelou o reino... por enquanto.");
            m_GameOverRoot = go.gameObject;
            Button(go, "Tentar de novo", new Vector2(-170, -120), () => m_Session.Restart());
            Button(go, "Menu principal", new Vector2(170, -120), () => m_Session.GoToMainMenu());
            m_GameOverRoot.SetActive(false);

            // Fim de fase
            var done = FullPanel(root, "FaseCompleta", "FASE COMPLETA!", "");
            m_CompleteRoot = done.gameObject;
            m_CompleteSummary = done.Find("Subtitulo").GetComponent<Text>();
            m_CompleteSummary.rectTransform.sizeDelta = new Vector2(760, 220);
            m_CompleteSummary.rectTransform.anchoredPosition = new Vector2(0, 20);
            if (!string.IsNullOrEmpty(m_Session.NextScene)) {
                Button(done, "Próxima fase", new Vector2(0, -170), () => m_Session.GoToNextScene());
                Button(done, "Jogar de novo", new Vector2(-320, -170), () => m_Session.Restart());
                Button(done, "Menu principal", new Vector2(320, -170), () => m_Session.GoToMainMenu());
            } else {
                Button(done, "Jogar de novo", new Vector2(-170, -170), () => m_Session.Restart());
                Button(done, "Menu principal", new Vector2(170, -170), () => m_Session.GoToMainMenu());
            }
            m_CompleteRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ atualização

        private void RefreshStats()
        {
            var p = m_Session.Player;
            int hp = 0, max = 3;
            if (p != null && p.CharacterDamageable != null) { hp = p.CharacterDamageable.CurrentHp; max = p.CharacterDamageable.MaxHp; }
            bool lost = m_HeartRow.lastValue > hp;
            SetRow(m_HeartRow, hp, max, Heart, HeartEmpty);
            if (lost && isActiveAndEnabled) { StartCoroutine(Pop(m_HeartRow.root)); }
            m_Lives.text = $"Vidas  x{m_Session.Lives}";
            m_Coins.text = $"Peixes  {m_Session.Coins}";
            m_Rare.text = $"Dourados  {m_Session.RareFish}/{m_Session.TotalRare}";
        }

        private static IEnumerator Pop(RectTransform rt)
        {
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) {
                rt.localScale = Vector3.one * (1f + Mathf.Sin(t / 0.3f * Mathf.PI) * 0.18f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private void RefreshMissions()
        {
            var sb = new StringBuilder();
            foreach (var m in m_Session.Missions) {
                if (!m.active) { continue; }
                sb.AppendLine(MissionLine(m.main, m.done, m.title, m.ProgressText));
            }
            m_Missions.text = sb.Length > 0 ? sb.ToString() : "Fale com os pinguins da vila.";
        }

        private static string MissionLine(bool main, bool done, string title, string progress)
        {
            string prog = !done && progress.Length > 0 ? $"  <color=#8CE0FF>{progress}</color>" : "";
            if (done) { return $"<color=#8CE696>√  {title}</color>"; }
            if (main) { return $"<color=#FFCD3C>»  {title}</color>{prog}"; }
            return $"•  {title}{prog}";
        }

        // ------------------------------------------------------------------ avisos

        public void Toast(string message)
        {
            m_ToastQueue.Enqueue(message);
            if (!m_ToastRunning) { StartCoroutine(ToastRoutine()); }
        }

        private IEnumerator ToastRoutine()
        {
            m_ToastRunning = true;
            while (m_ToastQueue.Count > 0) {
                m_Toast.text = m_ToastQueue.Dequeue();
                for (float t = 0; t < 0.2f; t += Time.unscaledDeltaTime) { m_ToastGroup.alpha = t / 0.2f; yield return null; }
                m_ToastGroup.alpha = 1f;
                yield return new WaitForSecondsRealtime(m_ToastQueue.Count > 0 ? 1.1f : 1.8f);
                for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime) { m_ToastGroup.alpha = 1f - t / 0.3f; yield return null; }
                m_ToastGroup.alpha = 0f;
            }
            m_ToastRunning = false;
        }

        // ------------------------------------------------------------------ biomas e boss

        public void ShowAreaTitle(string area)
        {
            if (m_AreaRoutine != null) { StopCoroutine(m_AreaRoutine); }
            m_AreaRoutine = StartCoroutine(AreaRoutine(area));
        }

        private IEnumerator AreaRoutine(string area)
        {
            m_AreaTitle.text = area;
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime) { m_AreaGroup.alpha = t / 0.5f; yield return null; }
            m_AreaGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(2.2f);
            for (float t = 0; t < 0.8f; t += Time.unscaledDeltaTime) { m_AreaGroup.alpha = 1f - t / 0.8f; yield return null; }
            m_AreaGroup.alpha = 0f;
            m_AreaRoutine = null;
        }

        /// <summary>Barra do boss: núcleos restantes e barra de travamento (cheia = BSOD trava).</summary>
        public void SetBossBar(string bossName, int cores, int maxCores, float stagger, bool frozen)
        {
            m_BossRoot.SetActive(true);
            m_BossName.text = frozen ? bossName + "  —  TRAVADO! ATAQUE!" : bossName;
            SetRow(m_BossCores, cores, maxCores, IceAccent, HeartEmpty);
            var rt = m_BossStagger.rectTransform;
            float v = frozen ? 1f : stagger;
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1f);
            m_BossStagger.color = frozen ? new Color32(120, 200, 255, 255) : new Color32(0, 120, 215, 255);
        }

        public void HideBossBar() { m_BossRoot.SetActive(false); }

        // ------------------------------------------------------------------ sequência de teclas (chefe travado)

        private GameObject m_QteRoot;
        private readonly List<Text> m_QteKeys = new List<Text>();
        private readonly List<Image> m_QteBoxes = new List<Image>();
        private Image m_QteTimer;
        private Text m_QteTitle;

        private static readonly Color QteIdle = new Color32(24, 64, 96, 200);
        private static readonly Color QteCurrent = new Color32(60, 120, 185, 230);
        private static readonly Color QteHit = new Color32(40, 150, 80, 230);
        private static readonly Color QteMiss = new Color32(180, 40, 50, 235);

        /// <summary>Mostra a sequência (ex.: W D W W A). Acertar tudo antes do tempo = true; errar uma tecla ou acabar o tempo = false.</summary>
        public void StartQte(char[] keys, float seconds, Action<bool> done)
        {
            BuildQte(keys.Length);
            StartCoroutine(QteRoutine(keys, seconds, done));
        }

        private void BuildQte(int count)
        {
            if (m_QteRoot != null) { Destroy(m_QteRoot); }
            m_QteKeys.Clear(); m_QteBoxes.Clear();
            float width = Mathf.Max(560f, count * 120f + 100f);
            var panel = Panel(m_Canvas.transform, "Sequencia", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(width, 240), look: Look.Card);
            m_QteRoot = panel.gameObject;
            m_QteTitle = Bold(Label(panel, "Titulo", new Vector2(0, -10), new Vector2(width, 58), 38, Gold, m_BodyFont, TextAnchor.MiddleCenter));
            m_QteTitle.text = "TRAVOU! Digite a sequência:";
            float start = -(count - 1) * 60f;
            for (int i = 0; i < count; i++) {
                var box = new GameObject("Tecla", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                box.transform.SetParent(panel, false);
                Center(box.rectTransform, new Vector2(start + i * 120f, -5f));
                box.rectTransform.sizeDelta = new Vector2(96, 96);
                box.color = QteIdle;
                var o = box.gameObject.AddComponent<Outline>(); o.effectColor = IceAccent; o.effectDistance = new Vector2(3, -3); o.useGraphicAlpha = false;
                var t = Bold(Label(box.transform, "Letra", Vector2.zero, new Vector2(96, 96), 60, Ice, m_BodyFont, TextAnchor.MiddleCenter));
                Stretch(t.rectTransform);
                m_QteBoxes.Add(box); m_QteKeys.Add(t);
            }
            var barBack = new GameObject("Tempo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            barBack.transform.SetParent(panel, false);
            barBack.rectTransform.anchorMin = new Vector2(0, 0); barBack.rectTransform.anchorMax = new Vector2(1, 0);
            barBack.rectTransform.pivot = new Vector2(0.5f, 0f);
            barBack.rectTransform.offsetMin = new Vector2(30, 22); barBack.rectTransform.offsetMax = new Vector2(-30, 40);
            barBack.color = new Color(0, 0, 0, 0.45f);
            m_QteTimer = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            m_QteTimer.transform.SetParent(barBack.transform, false);
            Stretch(m_QteTimer.rectTransform, 2f);
            m_QteTimer.color = Gold;
        }

        private IEnumerator QteRoutine(char[] keys, float seconds, Action<bool> done)
        {
            for (int i = 0; i < keys.Length; i++) { m_QteKeys[i].text = keys[i].ToString(); }
            int index = 0;
            float end = Time.unscaledTime + seconds;
            yield return null;
            bool ok = false;
            while (true) {
                float left = end - Time.unscaledTime;
                m_QteTimer.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(left / seconds), 1f);
                m_QteTimer.color = left < seconds * 0.3f ? Heart : Gold;
                for (int i = 0; i < m_QteBoxes.Count; i++) {
                    m_QteBoxes[i].color = i < index ? QteHit : i == index ? QteCurrent : QteIdle;
                    m_QteBoxes[i].rectTransform.localScale = Vector3.one * (i == index ? 1.12f + Mathf.Sin(Time.unscaledTime * 12f) * 0.04f : 1f);
                }
                if (left <= 0f) { m_QteTitle.text = "Tempo esgotado!"; break; }
                char pressed = ReadQteKey();
                if (pressed != '\0') {
                    if (pressed == keys[index]) {
                        Feedback.QteKey(true);
                        index++;
                        if (index >= keys.Length) { ok = true; m_QteTitle.text = "GOLPE DECISIVO!"; break; }
                    } else {
                        Feedback.QteKey(false);
                        m_QteBoxes[index].color = QteMiss;
                        m_QteTitle.text = "Errou!";
                        break;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < m_QteBoxes.Count; i++) { m_QteBoxes[i].rectTransform.localScale = Vector3.one; }
            if (ok) { foreach (var b in m_QteBoxes) { b.color = QteHit; } }
            yield return new WaitForSecondsRealtime(0.45f);
            if (m_QteRoot != null) { Destroy(m_QteRoot); }
            done?.Invoke(ok);
        }

        private static char ReadQteKey()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { return 'W'; }
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) { return 'A'; }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { return 'S'; }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { return 'D'; }
            return '\0';
        }

        // ------------------------------------------------------------------ prompt

        public void ShowPrompt(object owner, string text)
        {
            m_PromptOwner = owner;
            m_Prompt.text = text;
            m_PromptRoot.SetActive(!IsDialogOpen);
        }

        public void HidePrompt(object owner)
        {
            if (m_PromptOwner != owner) { return; }
            m_PromptOwner = null;
            m_PromptRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ diálogo

        public void ShowDialog(DialogueLine[] lines, string defaultSpeaker, Action onFinished)
        {
            StartCoroutine(DialogRoutine(lines, defaultSpeaker, onFinished));
        }

        private IEnumerator DialogRoutine(DialogueLine[] lines, string defaultSpeaker, Action onFinished)
        {
            m_PromptRoot.SetActive(false);
            m_DialogRoot.SetActive(true);
            yield return null; // ignora a tecla que abriu o diálogo
            foreach (var line in lines) {
                m_DialogName.text = string.IsNullOrEmpty(line.speaker) ? defaultSpeaker : line.speaker;
                string full = line.text ?? "";
                // Efeito máquina de escrever; apertar durante a digitação mostra tudo.
                int shown = 0;
                float acc = 0f;
                m_DialogText.text = "";
                while (shown < full.Length) {
                    if (AdvancePressed()) { shown = full.Length; break; }
                    acc += Time.unscaledDeltaTime * 55f;
                    int add = Mathf.FloorToInt(acc);
                    if (add > 0) {
                        int before = shown;
                        acc -= add; shown = Mathf.Min(full.Length, shown + add); m_DialogText.text = full.Substring(0, shown);
                        if (before / 3 != shown / 3) { Feedback.DialogBlip(); }
                    }
                    yield return null;
                }
                m_DialogText.text = full;
                yield return null;
                while (!AdvancePressed()) { yield return null; }
                yield return null;
            }
            m_DialogRoot.SetActive(false);
            onFinished?.Invoke();
        }

        private static bool AdvancePressed() =>
            Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);

        // ------------------------------------------------------------------ fim de jogo / fase

        public void ShowGameOver()
        {
            m_DialogRoot.SetActive(false);
            m_PromptRoot.SetActive(false);
            StartCoroutine(ShowAfter(m_GameOverRoot, 1.2f));
        }

        public void ShowLevelComplete()
        {
            int missionsDone = 0, missionsTotal = 0;
            foreach (var m in m_Session.Missions) { missionsTotal++; if (m.done) { missionsDone++; } }
            m_CompleteSummary.text =
                $"Peixes: {m_Session.Coins}\n" +
                $"Peixes dourados: {m_Session.RareFish}/{m_Session.TotalRare}\n" +
                $"Capangas do BSOD derrotados: {m_Session.Kills}/{m_Session.TotalEnemies}\n" +
                $"Missões: {missionsDone}/{missionsTotal}\n\n" +
                "O BSOD foi derrotado. O Reino Congelado de Kernel está livre!";
            StartCoroutine(ShowAfter(m_CompleteRoot, 0.4f));
        }

        private IEnumerator ShowAfter(GameObject panel, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var group = panel.GetComponent<CanvasGroup>();
            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.35f; yield return null; }
            group.alpha = 1f;
        }

        // ------------------------------------------------------------------ helpers de UI

        /// <summary>
        /// Área do HUD. Look.Soft = sombreado de bordas esfumadas que sai além da área (só para dar contraste ao texto
        /// sobre a neve); Look.Card = o mesmo, um pouco mais forte, para painéis que pedem foco (sequência do chefe).
        /// </summary>
        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null, Look look = Look.None)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            if (look == Look.None) { return rt; }

            var back = new GameObject("Sombreado", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(go.transform, false);
            Stretch((RectTransform)back.transform, look == Look.Card ? -36f : -48f);
            var bimg = back.GetComponent<Image>();
            bimg.sprite = SoftSprite(); bimg.type = Image.Type.Sliced; bimg.pixelsPerUnitMultiplier = 0.55f;
            bimg.color = new Color(Shade.r, Shade.g, Shade.b, look == Look.Card ? 0.7f : 0.38f);
            bimg.raycastTarget = false;
            return rt;
        }

        private RectTransform FullPanel(Transform parent, string name, string title, string subtitle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            go.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.12f, 0.55f);
            go.GetComponent<CanvasGroup>().alpha = 0f;
            var card = Panel(go.transform, "Cartao", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 620), look: Look.Card);
            var t = Label(go.transform, "Titulo", new Vector2(0, 230), new Vector2(880, 120), 96, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Center(t.rectTransform, new Vector2(0, 220));
            var s = Bold(Label(go.transform, "Subtitulo", Vector2.zero, new Vector2(820, 120), 32, IceAccent, m_BodyFont, TextAnchor.MiddleCenter));
            Center(s.rectTransform, new Vector2(0, 60));
            t.text = title; s.text = subtitle;
            card.SetAsFirstSibling();
            return rt;
        }

        private void Button(RectTransform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject("Botao " + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Center(rt, pos);
            rt.sizeDelta = new Vector2(300, 84);
            var img = go.GetComponent<Image>();
            if (m_ButtonSprite != null) { img.sprite = m_ButtonSprite; img.color = Color.white; } else { img.color = new Color32(24, 64, 96, 240); }
            var btn = go.GetComponent<Button>();
            var colors = btn.colors; colors.highlightedColor = new Color32(180, 236, 255, 255); colors.pressedColor = new Color32(90, 180, 220, 255); btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
            var text = Label(go.transform, "Texto", Vector2.zero, rt.sizeDelta, 38, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = label;
        }

        /// <summary>Texto do HUD: sempre com contorno escuro + sombra projetada, legível sobre neve, céu ou rocha.</summary>
        private Text Label(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color color, Font font,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.02f, 0.06f, 0.6f); shadow.effectDistance = new Vector2(2f, -4f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.03f, 0.08f, 0.9f); outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>Negrito só quando a fonte não é bold de verdade (evita o "negrito falso" borrado em cima de Roboto Bold).</summary>
        private static Text Bold(Text t)
        {
            if (t.font == null || !t.font.name.Contains("Bold")) { t.fontStyle = FontStyle.Bold; }
            return t;
        }

        private static void SetRow(IconRow row, int value, int max, Color on, Color off)
        {
            while (row.icons.Count < max) {
                var img = new GameObject("Icone", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(row.root, false);
                img.sprite = row.sprite; img.raycastTarget = false; img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.sizeDelta = Vector2.one * row.size;
                rt.localRotation = Quaternion.Euler(0, 0, row.rotation);
                rt.anchorMin = rt.anchorMax = new Vector2(row.centered ? 0.5f : 0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var o = img.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0.03f, 0.08f, 0.85f); o.effectDistance = new Vector2(2f, -2f); o.useGraphicAlpha = false;
                var s = img.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0f, 0.02f, 0.06f, 0.5f); s.effectDistance = new Vector2(2f, -4f);
                row.icons.Add(img);
            }
            float total = max * row.size + Mathf.Max(0, max - 1) * row.gap;
            for (int i = 0; i < row.icons.Count; i++) {
                var img = row.icons[i];
                bool show = i < max;
                if (img.gameObject.activeSelf != show) { img.gameObject.SetActive(show); }
                if (!show) { continue; }
                float x = i * (row.size + row.gap) + row.size * 0.5f - (row.centered ? total * 0.5f : 0f);
                img.rectTransform.anchoredPosition = new Vector2(x, 0f);
                img.color = i < value ? on : off;
            }
            row.lastValue = value;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Center(RectTransform rt, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
        }

        // ------------------------------------------------------------------ sprites gerados (sem arquivos de imagem)

        private static Sprite s_Heart, s_Soft, s_FadeV, s_FadeH;

        private static Texture2D NewTex(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };

        /// <summary>Coração (curva (x²+y²-1)³ = x²y³) com antisserrilhado 4x4.</summary>
        private static Sprite HeartSprite()
        {
            if (s_Heart != null) { return s_Heart; }
            const int N = 64;
            var tex = NewTex(N, N);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) {
                for (int x = 0; x < N; x++) {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++) {
                        for (int sx = 0; sx < 4; sx++) {
                            float u = (x + (sx + 0.5f) / 4f) / N * 2.6f - 1.3f;
                            float v = (y + (sy + 0.5f) / 4f) / N * 2.6f - 1.25f;
                            float a = u * u + v * v - 1f;
                            if (a * a * a - u * u * v * v * v <= 0f) { hit++; }
                        }
                    }
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 16));
                }
            }
            tex.SetPixels32(px); tex.Apply();
            s_Heart = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            s_Heart.hideFlags = HideFlags.DontSave;
            return s_Heart;
        }

        /// <summary>Retângulo de bordas totalmente esfumadas (9-slice): sombreado que não forma "caixa".</summary>
        private static Sprite SoftSprite()
        {
            if (s_Soft != null) { return s_Soft; }
            const int N = 64; const float B = 30f;
            var tex = NewTex(N, N);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++) {
                for (int x = 0; x < N; x++) {
                    float rx = Mathf.Clamp01(Mathf.Min(x, N - 1 - x) / B);
                    float ry = Mathf.Clamp01(Mathf.Min(y, N - 1 - y) / B);
                    float a = Mathf.SmoothStep(0f, 1f, rx) * Mathf.SmoothStep(0f, 1f, ry);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px); tex.Apply();
            s_Soft = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(B, B, B, B));
            s_Soft.hideFlags = HideFlags.DontSave;
            return s_Soft;
        }

        /// <summary>Degradê: vertical = forte embaixo e some para cima; horizontal = forte à esquerda e some para a direita.</summary>
        private static Sprite FadeSprite(bool horizontal = false)
        {
            if (!horizontal && s_FadeV != null) { return s_FadeV; }
            if (horizontal && s_FadeH != null) { return s_FadeH; }
            const int N = 64;
            var tex = horizontal ? NewTex(N, 2) : NewTex(2, N);
            for (int i = 0; i < N; i++) {
                float t = i / (N - 1f);
                float a = horizontal ? 1f - Mathf.SmoothStep(0f, 1f, t) : Mathf.Pow(1f - t, 1.4f);
                var c = new Color(1f, 1f, 1f, a);
                if (horizontal) { tex.SetPixel(i, 0, c); tex.SetPixel(i, 1, c); } else { tex.SetPixel(0, i, c); tex.SetPixel(1, i, c); }
            }
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            if (horizontal) { s_FadeH = sprite; } else { s_FadeV = sprite; }
            return sprite;
        }
    }
}
