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
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private static readonly Color Ice = new Color32(214, 241, 255, 255);
        private static readonly Color IceAccent = new Color32(140, 224, 255, 255);
        private static readonly Color Dark = new Color32(6, 16, 30, 240);
        private static readonly Color Gold = new Color32(255, 205, 60, 255);
        private static readonly Color Heart = new Color32(255, 92, 110, 255);
        private static readonly Color Done = new Color32(140, 230, 150, 255);

        private GameSession m_Session;
        private Font m_TitleFont;
        private Font m_BodyFont;
        private Sprite m_PanelSprite;
        private Sprite m_ButtonSprite;

        private Canvas m_Canvas;
        private Text m_Hearts, m_Lives, m_Coins, m_Rare;
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
        private GameObject m_BossRoot; private Text m_BossName; private Image m_BossStagger; private Text m_BossCores;

        private RectTransform m_Reticle;
        private List<Image> m_ReticleParts;
        private Text m_ReticleHp, m_ReticleHint;
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
            m_Hearts.text = "♥♥♡"; m_Lives.text = "Vidas  x3"; m_Coins.text = "Peixes  42"; m_Rare.text = "Dourados  1/4";
            m_Missions.text = "<color=#FFCD3C>★ Chegue ao topo da Montanha Ancestral</color>\n• Colete 200 peixes  <color=#8CE0FF>42/200</color>\n<color=#8CE696>✓ Fale com o Ancião Tux</color>";
            m_Reticle.gameObject.SetActive(true);
            m_Reticle.anchoredPosition = reticleScreen;
            foreach (var img in m_ReticleParts) { img.color = Gold; }
            m_ReticleHp.text = "♥♡"; m_ReticleHint.text = "segure botão direito: travar";
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
            m_ReticleSpin += Time.unscaledDeltaTime * (locked ? 0f : 40f);
            m_Reticle.localRotation = Quaternion.Euler(0, 0, locked ? 45f : Mathf.Sin(m_ReticleSpin * 0.05f) * 6f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f;
            m_Reticle.localScale = Vector3.one * (locked ? 0.8f : pulse);
            Color c = locked ? new Color32(255, 90, 90, 255) : Gold;
            foreach (var img in m_ReticleParts) { img.color = c; }

            // corações do inimigo e dica
            m_ReticleHp.rectTransform.localRotation = Quaternion.Inverse(m_Reticle.localRotation);
            m_ReticleHint.rectTransform.localRotation = Quaternion.Inverse(m_Reticle.localRotation);
            var dmg = m_Targeting.TargetDamageable;
            var sb = new StringBuilder();
            if (dmg != null && dmg.MaxHp <= 10) { for (int i = 0; i < dmg.MaxHp; i++) { sb.Append(i < dmg.CurrentHp ? "♥" : "♡"); } }
            m_ReticleHp.text = sb.ToString();
            m_ReticleHint.text = locked ? "câmera travada" : "segure botão direito: travar";
        }

        public void Init(GameSession session, Font titleFont, Sprite panel, Sprite button)
        {
            m_Session = session;
            m_BodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_TitleFont = titleFont != null ? titleFont : m_BodyFont;
            m_PanelSprite = panel;
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

            // Status (canto superior esquerdo)
            var stats = Panel(root, "Status", new Vector2(0, 1), new Vector2(30, -30), new Vector2(380, 190));
            m_Hearts = Label(stats, "Coracoes", new Vector2(24, -14), new Vector2(340, 56), 46, Heart, m_BodyFont);
            m_Lives = Bold(Label(stats, "Vidas", new Vector2(24, -70), new Vector2(340, 36), 28, Ice, m_BodyFont));
            m_Coins = Bold(Label(stats, "Peixes", new Vector2(24, -106), new Vector2(340, 36), 28, IceAccent, m_BodyFont));
            m_Rare = Bold(Label(stats, "Dourados", new Vector2(24, -142), new Vector2(340, 36), 28, Gold, m_BodyFont));

            // Missões (canto superior direito)
            var missions = Panel(root, "Missoes", new Vector2(1, 1), new Vector2(-30, -30), new Vector2(560, 300));
            Label(missions, "Titulo", new Vector2(24, -12), new Vector2(510, 44), 34, IceAccent, m_TitleFont).text = "Missões";
            m_Missions = Label(missions, "Lista", new Vector2(24, -60), new Vector2(515, 230), 25, Ice, m_BodyFont);
            m_Missions.supportRichText = true;
            m_Missions.verticalOverflow = VerticalWrapMode.Overflow;

            // Aviso (centro superior)
            var toastGo = new GameObject("Aviso", typeof(RectTransform));
            toastGo.transform.SetParent(root, false);
            var trt = (RectTransform)toastGo.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0, -240); trt.sizeDelta = new Vector2(1100, 70);
            m_ToastGroup = toastGo.AddComponent<CanvasGroup>(); m_ToastGroup.alpha = 0f;
            m_Toast = Bold(Label(toastGo.transform, "Texto", Vector2.zero, new Vector2(1100, 70), 34, Ice, m_BodyFont, TextAnchor.MiddleCenter));
            Stretch(m_Toast.rectTransform);
            var outline = m_Toast.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, 0.8f); outline.effectDistance = new Vector2(2, -2);

            // Nome do lugar ao entrar num bioma (centro)
            var areaGo = new GameObject("NomeDoLugar", typeof(RectTransform));
            areaGo.transform.SetParent(root, false);
            var art = (RectTransform)areaGo.transform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.68f); art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(1400, 140);
            m_AreaGroup = areaGo.AddComponent<CanvasGroup>(); m_AreaGroup.alpha = 0f;
            m_AreaTitle = Label(areaGo.transform, "Texto", Vector2.zero, new Vector2(1400, 140), 92, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Stretch(m_AreaTitle.rectTransform);
            var aOutline = m_AreaTitle.gameObject.AddComponent<Outline>(); aOutline.effectColor = new Color(0.02f, 0.1f, 0.2f, 0.9f); aOutline.effectDistance = new Vector2(3, -3);

            // Barra do boss (topo, centro)
            var boss = Panel(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(900, 120), pivot: new Vector2(0.5f, 1f));
            m_BossRoot = boss.gameObject;
            m_BossName = Label(boss, "Nome", new Vector2(30, -10), new Vector2(500, 50), 42, new Color32(120, 170, 255, 255), m_TitleFont);
            m_BossCores = Label(boss, "Nucleos", new Vector2(520, -14), new Vector2(350, 46), 30, Ice, m_BodyFont, TextAnchor.UpperRight);
            var barBack = new GameObject("BarraFundo", typeof(RectTransform), typeof(Image));
            barBack.transform.SetParent(boss, false);
            var bb = (RectTransform)barBack.transform;
            bb.anchorMin = bb.anchorMax = new Vector2(0, 1); bb.pivot = new Vector2(0, 1);
            bb.anchoredPosition = new Vector2(30, -70); bb.sizeDelta = new Vector2(840, 28);
            barBack.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);
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
            m_ReticleHp = Label(m_Reticle, "Vida", Vector2.zero, new Vector2(200, 40), 30, Heart, m_BodyFont, TextAnchor.MiddleCenter);
            Center(m_ReticleHp.rectTransform, new Vector2(0, -78));
            m_ReticleHp.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            m_ReticleHint = Label(m_Reticle, "Dica", Vector2.zero, new Vector2(320, 30), 20, Ice, m_BodyFont, TextAnchor.MiddleCenter);
            Center(m_ReticleHint.rectTransform, new Vector2(0, -108));
            m_ReticleHint.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.8f);
            m_Reticle.gameObject.SetActive(false);

            // Prompt de interação (centro inferior)
            m_PromptRoot = Panel(root, "Prompt", new Vector2(0.5f, 0f), new Vector2(0, 230), new Vector2(620, 70), pivot: new Vector2(0.5f, 0f)).gameObject;
            m_Prompt = Bold(Label(m_PromptRoot.transform, "Texto", Vector2.zero, new Vector2(620, 70), 28, Ice, m_BodyFont, TextAnchor.MiddleCenter));
            Stretch(m_Prompt.rectTransform);
            m_PromptRoot.SetActive(false);

            // Diálogo (inferior)
            var dialog = Panel(root, "Dialogo", new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1400, 260), pivot: new Vector2(0.5f, 0f));
            m_DialogRoot = dialog.gameObject;
            m_DialogName = Label(dialog, "Nome", new Vector2(40, -18), new Vector2(900, 50), 38, Gold, m_TitleFont);
            m_DialogText = Label(dialog, "Fala", new Vector2(40, -74), new Vector2(1320, 150), 34, Color.white, m_BodyFont);
            m_DialogText.lineSpacing = 1.1f;
            m_DialogHint = Label(dialog, "Dica", new Vector2(1040, -212), new Vector2(330, 36), 24, IceAccent, m_BodyFont, TextAnchor.MiddleRight);
            m_DialogHint.text = "[E] continuar ▶";
            m_DialogRoot.SetActive(false);

            // Game Over
            var go = FullPanel(root, "GameOver", "Fim de jogo", "O BSOD congelou o reino... por enquanto.");
            m_GameOverRoot = go.gameObject;
            Button(go, "Tentar de novo", new Vector2(-170, -120), () => m_Session.Restart());
            Button(go, "Menu principal", new Vector2(170, -120), () => m_Session.GoToMainMenu());
            m_GameOverRoot.SetActive(false);

            // Fim de fase
            var done = FullPanel(root, "FaseCompleta", "Fase completa!", "");
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
            var sb = new StringBuilder();
            for (int i = 0; i < max; i++) { sb.Append(i < hp ? "♥" : "♡"); }
            m_Hearts.text = sb.ToString();
            m_Lives.text = $"Vidas  x{m_Session.Lives}";
            m_Coins.text = $"Peixes  {m_Session.Coins}";
            m_Rare.text = $"Dourados  {m_Session.RareFish}/{m_Session.TotalRare}";
        }

        private void RefreshMissions()
        {
            var sb = new StringBuilder();
            foreach (var m in m_Session.Missions) {
                if (!m.active) { continue; }
                string progress = m.ProgressText.Length > 0 ? $"  <color=#8CE0FF>{m.ProgressText}</color>" : "";
                if (m.done) {
                    sb.AppendLine($"<color=#8CE696>✓ {m.title}</color>");
                } else if (m.main) {
                    sb.AppendLine($"<color=#FFCD3C>★ {m.title}</color>{progress}");
                } else {
                    sb.AppendLine($"• {m.title}{progress}");
                }
            }
            m_Missions.text = sb.Length > 0 ? sb.ToString() : "Fale com os pinguins da vila.";
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
            var sb = new StringBuilder("Núcleos ");
            for (int i = 0; i < maxCores; i++) { sb.Append(i < cores ? "◆" : "◇"); }
            m_BossCores.text = sb.ToString();
            var rt = m_BossStagger.rectTransform;
            float v = frozen ? 1f : stagger;
            rt.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1f);
            m_BossStagger.color = frozen ? new Color32(120, 200, 255, 255) : new Color32(0, 120, 215, 255);
        }

        public void HideBossBar() { m_BossRoot.SetActive(false); }

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

        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Fundo escuro sólido + borda gelo fina. Sem imagem atrás do texto: leitura sempre limpa.
            var back = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(go.transform, false);
            Stretch((RectTransform)back.transform);
            var bimg = back.GetComponent<Image>(); bimg.color = Dark; bimg.raycastTarget = false;
            var border = back.AddComponent<Outline>();
            border.effectColor = new Color(IceAccent.r, IceAccent.g, IceAccent.b, 0.55f);
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = false;
            return rt;
        }

        private RectTransform FullPanel(Transform parent, string name, string title, string subtitle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            go.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.12f, 0.78f);
            go.GetComponent<CanvasGroup>().alpha = 0f;
            var card = Panel(go.transform, "Cartao", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 620));
            var t = Label(go.transform, "Titulo", new Vector2(0, 230), new Vector2(880, 110), 80, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Center(t.rectTransform, new Vector2(0, 220));
            var s = Label(go.transform, "Subtitulo", Vector2.zero, new Vector2(820, 120), 32, IceAccent, m_BodyFont, TextAnchor.MiddleCenter);
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
            var text = Label(go.transform, "Texto", Vector2.zero, rt.sizeDelta, 32, Ice, m_TitleFont, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = label;
            var o = text.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.7f);
        }

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
            return t;
        }

        private static Text Bold(Text t) { t.fontStyle = FontStyle.Bold; return t; }

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
    }
}
