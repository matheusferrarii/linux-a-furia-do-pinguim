using System.IO;
using LinuxFuriaPinguim.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.EditorTools
{
    public static class MenuBuilder
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string MountainScenePath = "Assets/Scenes/Mountain.unity";
        private const string MenuBackgroundPath = "Assets/UI/Sprites/MenuBackground.jpg";
        private const string IndividualButtonSpritesDir = "Assets/UI/Sprites/Buttons";

        // ---------- Ice theme palette ----------
        private static readonly Color IceDark = new Color32(8, 20, 36, 235);
        private static readonly Color IceMid = new Color32(24, 64, 96, 230);
        private static readonly Color IceLight = new Color32(214, 241, 255, 255);
        private static readonly Color IceAccent = new Color32(140, 224, 255, 255);
        private static readonly Color IceOutline = new Color32(10, 40, 64, 255);
        private static readonly Color FrostedNormal = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color FrostedHighlighted = new Color32(180, 236, 255, 110);
        private static readonly Color FrostedPressed = new Color32(90, 180, 220, 200);
        private static readonly Color FrostedSelected = new Color32(150, 220, 255, 140);

        // ---------- Wood theme palette (main menu action buttons) ----------
        private static readonly Color WoodNormal = new Color32(140, 94, 48, 255);
        private static readonly Color WoodHighlighted = new Color32(172, 122, 70, 255);
        private static readonly Color WoodPressed = new Color32(100, 68, 34, 255);
        private static readonly Color SnowCap = new Color32(238, 248, 252, 235);

        [MenuItem("Tools/Furia Pinguim/Build Menus")]
        public static void BuildMenus()
        {
            BuildMainMenuScene();
            BuildPauseMenuInMountainScene();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Nota: isto so vale para esta sessao do Editor - "Play Mode Start Scene" vive em
            // Library/ (cache local, fora do controle de versao) e o Unity nao persiste esse
            // valor entre processos batchmode. Configure uma vez por maquina via Window >
            // Play Mode Start Scene (ou o icone de cena no toolbar) apontando para MainMenu.unity.
            SceneAsset mainMenuSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
            if (mainMenuSceneAsset != null) {
                EditorSceneManager.playModeStartScene = mainMenuSceneAsset;
            }

            Debug.Log("MenuBuilder: menus criados com sucesso.");
        }

        // ---------- Main Menu scene ----------

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";
            cameraGO.transform.position = new Vector3(0f, 1f, -10f);
            Camera camera = cameraGO.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = IceDark;

            CreateSnowfall();

            GameObject audioManagerGO = new GameObject("AudioManager", typeof(AudioManager));

            EnsureEventSystem();

            GameObject canvasGO = CreateCanvas("MainMenuCanvas");
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasGO.AddComponent<CanvasGroup>();
            UIFadeIn fadeIn = canvasGO.AddComponent<UIFadeIn>();
            SerializedObject fadeSo = new SerializedObject(fadeIn);
            fadeSo.FindProperty("duration").floatValue = 0.6f;
            fadeSo.ApplyModifiedProperties();

            Sprite backgroundSprite = LoadAsSprite(MenuBackgroundPath);
            Image backgroundImage = CreateFullStretchImage(canvasRect, "Background", Color.white);
            bool hasCustomBackground = backgroundSprite != null;
            if (hasCustomBackground) {
                backgroundImage.sprite = backgroundSprite;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.preserveAspect = false;
            } else {
                backgroundImage.color = IceDark;
                AddGradient(backgroundImage, IceMid, IceDark);
            }

            BuildCharacterContainer(canvasRect);
            BuildTitle(canvasRect);

            GameObject buttonsPanel = CreateVerticalGroup(canvasRect, "ButtonsPanel", 20f, 420f, controlChildWidth: false);
            RectTransform buttonsRect = buttonsPanel.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(1f, 0.5f);
            buttonsRect.anchorMax = new Vector2(1f, 0.5f);
            buttonsRect.pivot = new Vector2(1f, 0.5f);
            buttonsRect.anchoredPosition = new Vector2(-140f, 0f);

            GameObject optionsPanel = BuildSettingsPanel(canvasRect, out SettingsPanelController settingsController);
            GameObject extrasPanel = BuildExtrasPanel(canvasRect);
            GameObject quitConfirmPanel = BuildQuitConfirmPanel(canvasRect);
            GameObject loadingOverlay = BuildLoadingOverlay(canvasRect, out CanvasGroup loadingGroup, out Slider loadingBar);

            if (!hasCustomBackground) {
                BuildIceFrame(canvasRect);
            }

            GameObject sceneLoaderGO = new GameObject("SceneLoader");
            SceneLoader sceneLoader = sceneLoaderGO.AddComponent<SceneLoader>();
            SerializedObject loaderSo = new SerializedObject(sceneLoader);
            loaderSo.FindProperty("fadeOverlay").objectReferenceValue = loadingGroup;
            loaderSo.FindProperty("progressBar").objectReferenceValue = loadingBar;
            loaderSo.ApplyModifiedProperties();

            GameObject controllerGO = new GameObject("MainMenuController");
            MainMenuController mainMenuController = controllerGO.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(mainMenuController);
            so.FindProperty("gameSceneName").stringValue = "Mountain";
            so.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            so.FindProperty("extrasPanel").objectReferenceValue = extrasPanel;
            so.FindProperty("quitConfirmPanel").objectReferenceValue = quitConfirmPanel;
            so.FindProperty("sceneLoader").objectReferenceValue = sceneLoader;
            so.ApplyModifiedProperties();

            Button playButton = CreateWoodButton(buttonsRect, "PlayButton", "JOGAR", WoodNormal, "Btn_Play");
            UnityEventTools.AddVoidPersistentListener(playButton.onClick, mainMenuController.OnPlayClicked);

            Button optionsButton = CreateWoodButton(buttonsRect, "OptionsButton", "OPCOES", WoodNormal, "Btn_Options");
            UnityEventTools.AddVoidPersistentListener(optionsButton.onClick, mainMenuController.OnOptionsClicked);

            Button extrasButton = CreateWoodButton(buttonsRect, "ExtrasButton", "EXTRAS", WoodNormal, "Btn_Extras");
            UnityEventTools.AddVoidPersistentListener(extrasButton.onClick, mainMenuController.OnExtrasClicked);

            Button quitButton = CreateWoodButton(buttonsRect, "QuitButton", "SAIR", WoodNormal, "Btn_Quit");
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, mainMenuController.OnQuitClicked);

            Button closeOptionsButton = optionsPanel.transform.Find("Card/CloseButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(closeOptionsButton.onClick, settingsController.Close);

            QuitConfirmationController quitController = quitConfirmPanel.GetComponent<QuitConfirmationController>();
            Button confirmYes = quitConfirmPanel.transform.Find("Card/YesButton").GetComponent<Button>();
            Button confirmNo = quitConfirmPanel.transform.Find("Card/NoButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(confirmYes.onClick, quitController.ConfirmQuit);
            UnityEventTools.AddVoidPersistentListener(confirmNo.onClick, quitController.CancelQuit);

            Button closeExtrasButton = extrasPanel.transform.Find("Card/CloseButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(closeExtrasButton.onClick, extrasPanel.GetComponent<UIPanelFader>().Hide);

            optionsPanel.GetComponent<UIPanelFader>().SetInstant(false);
            extrasPanel.GetComponent<UIPanelFader>().SetInstant(false);
            quitConfirmPanel.GetComponent<UIPanelFader>().SetInstant(false);
            loadingOverlay.SetActive(true);
            loadingGroup.alpha = 0f;
            loadingGroup.blocksRaycasts = false;
            loadingGroup.interactable = false;

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private const string PenguinPrefabPath = "Assets/Dypsloom/DypThePenguin/Demo/Characters/Prefab/Dyp.prefab";
        private const int PenguinPreviewLayer = 11;

        private static void BuildCharacterContainer(Transform canvasParent)
        {
            // Posicionado sobre o bloco de gelo livre (sem pinguim desenhado) da arte de fundo -
            // estimativa visual, ajuste fino rapido no Editor se precisar (so mover este objeto).
            GameObject container = new GameObject("CharacterContainer", typeof(RectTransform));
            container.transform.SetParent(canvasParent, false);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.zero;
            containerRect.pivot = new Vector2(0.5f, 0f);
            containerRect.anchoredPosition = new Vector2(700f, 90f);
            containerRect.sizeDelta = new Vector2(440f, 560f);

            RenderTexture previewTexture = BuildPenguinPreviewRig();

            GameObject previewGO = new GameObject("PenguinPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewGO.transform.SetParent(containerRect, false);
            RectTransform previewRect = previewGO.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0.5f, 0f);
            previewRect.anchorMax = new Vector2(0.5f, 0f);
            previewRect.pivot = new Vector2(0.5f, 0f);
            previewRect.anchoredPosition = Vector2.zero;
            previewRect.sizeDelta = new Vector2(440f, 560f);

            RawImage previewImage = previewGO.GetComponent<RawImage>();
            previewImage.texture = previewTexture;
            previewImage.raycastTarget = false;
        }

        /// <summary>
        /// Instancia o Dyp.prefab (asset ja existente do personagem) numa camada isolada,
        /// remove os scripts/fisica de gameplay (nao sao necessarios numa pose de menu) e
        /// renderiza so essa camada numa RenderTexture via uma camera dedicada.
        /// </summary>
        private static RenderTexture BuildPenguinPreviewRig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PenguinPrefabPath);

            GameObject rig = new GameObject("PenguinPreviewRig");
            rig.transform.position = new Vector3(60f, 0f, 60f);

            Bounds? penguinBounds = null;

            if (prefab != null) {
                GameObject penguin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig.transform);
                penguin.transform.localPosition = Vector3.zero;
                penguin.transform.localRotation = Quaternion.Euler(0f, 160f, 0f);

                // "Capsule" e so um proxy de colisao (MeshRenderer de uma capsula, sem skin) -
                // remove o objeto inteiro. "Sphere" e o objeto principal do personagem: tem o
                // SkinnedMeshRenderer real (o pinguim) E TAMBEM um MeshRenderer/MeshFilter cru
                // (provavelmente visualizacao de colisao) ligados ao mesmo GameObject - sem o
                // script do personagem pra escondê-lo, os dois aparecem juntos, sobrepostos,
                // criando aquela mancha cinza. Remove so o MeshRenderer/MeshFilter cru, mantendo
                // o SkinnedMeshRenderer (o modelo visivel de verdade).
                foreach (Transform t in penguin.GetComponentsInChildren<Transform>(true)) {
                    if (t == null) { continue; }
                    if (t.name == "Capsule") {
                        Object.DestroyImmediate(t.gameObject);
                    } else if (t.name == "Sphere") {
                        MeshRenderer plainRenderer = t.GetComponent<MeshRenderer>();
                        if (plainRenderer != null) { Object.DestroyImmediate(plainRenderer); }
                        MeshFilter meshFilter = t.GetComponent<MeshFilter>();
                        if (meshFilter != null) { Object.DestroyImmediate(meshFilter); }
                    }
                }

                // Remove toda logica de gameplay (movimento, inventario, dano, fisica) -
                // a pose de menu so precisa do modelo + Animator.
                foreach (MonoBehaviour behaviour in penguin.GetComponentsInChildren<MonoBehaviour>(true)) {
                    Object.DestroyImmediate(behaviour);
                }
                foreach (Rigidbody rb in penguin.GetComponentsInChildren<Rigidbody>(true)) {
                    Object.DestroyImmediate(rb);
                }
                foreach (CharacterController cc in penguin.GetComponentsInChildren<CharacterController>(true)) {
                    Object.DestroyImmediate(cc);
                }
                foreach (Collider col in penguin.GetComponentsInChildren<Collider>(true)) {
                    Object.DestroyImmediate(col);
                }

                // Efeitos de gameplay aninhados (puff de poeira, estrelas de atordoado, cura,
                // rastro) tem bounds proprios enormes/deslocados mesmo sem nenhuma particula
                // viva, o que bagunçava o enquadramento automatico da camera abaixo. Nao fazem
                // sentido numa pose estatica de menu de qualquer forma - remove os objetos inteiros.
                foreach (ParticleSystem effect in penguin.GetComponentsInChildren<ParticleSystem>(true)) {
                    Object.DestroyImmediate(effect.gameObject);
                }
                foreach (TrailRenderer trail in penguin.GetComponentsInChildren<TrailRenderer>(true)) {
                    Object.DestroyImmediate(trail.gameObject);
                }

                SetLayerRecursively(penguin, PenguinPreviewLayer);

                // O Animator original usa "Cull Update Transforms" (so atualiza os ossos quando
                // visivel por uma camera "normal"). A camera de preview isolada pode nao marcar
                // o SkinnedMeshRenderer como visivel pra esse calculo, travando o modelo na bind
                // pose (pose quebrada / T-pose). Forca sempre animar.
                foreach (Animator animator in penguin.GetComponentsInChildren<Animator>(true)) {
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                IdleFloatAnimation idleFloat = penguin.AddComponent<IdleFloatAnimation>();
                SerializedObject idleSo = new SerializedObject(idleFloat);
                idleSo.FindProperty("amplitude").floatValue = 0.05f;
                idleSo.FindProperty("speed").floatValue = 1.4f;
                idleSo.ApplyModifiedProperties();

                // A mancha que aparecia era sempre identica em todas as tentativas anteriores
                // (so mexi em scripts/componentes, nunca na camera) - sinal forte de que a
                // camera estava simplesmente mal posicionada pro tamanho real do modelo (perto
                // demais, cortando). Em vez de chutar distancia/altura fixas, calcula os bounds
                // reais dos renderers e enquadra a camera a partir disso.
                Renderer[] renderers = penguin.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0) {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                    penguinBounds = bounds;
                }
            } else {
                Debug.LogWarning("MenuBuilder: Dyp.prefab nao encontrado em " + PenguinPrefabPath + " - preview do pinguim ficara vazio.");
            }

            // Sem plataforma de gelo propria - o pinguim ja fica posicionado em cima do iceberg
            // desenhado na arte de fundo, um disco 3D extra aqui ficava redundante (e foi o que
            // renderizava rosa quando o shader nao resolvia certo).
            Vector3 focusPoint = penguinBounds.HasValue
                ? penguinBounds.Value.center
                : rig.transform.position + Vector3.up * 0.5f;
            float extent = penguinBounds.HasValue ? penguinBounds.Value.extents.magnitude : 1f;

            GameObject lightGO = new GameObject("PreviewLight", typeof(Light));
            lightGO.transform.SetParent(rig.transform, false);
            lightGO.transform.position = focusPoint + new Vector3(extent, extent * 1.5f, -extent);
            lightGO.transform.LookAt(focusPoint);
            Light light = lightGO.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = Color.white;
            light.cullingMask = 1 << PenguinPreviewLayer;
            light.shadows = LightShadows.None;

            GameObject fillLightGO = new GameObject("PreviewFillLight", typeof(Light));
            fillLightGO.transform.SetParent(rig.transform, false);
            fillLightGO.transform.position = focusPoint + new Vector3(-extent, extent * 0.5f, extent);
            fillLightGO.transform.LookAt(focusPoint);
            Light fillLight = fillLightGO.GetComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.5f;
            fillLight.color = Color.white;
            fillLight.cullingMask = 1 << PenguinPreviewLayer;
            fillLight.shadows = LightShadows.None;

            RenderTexture renderTexture = GetOrCreatePreviewRenderTexture();

            float fieldOfView = 30f;
            // Distancia que garante o bounding sphere inteiro dentro do frustum, com folga minima
            // (pedido pra ficar maior/mais perto do gelo).
            float cameraDistance = (extent / Mathf.Sin(fieldOfView * 0.5f * Mathf.Deg2Rad)) * 0.95f;

            GameObject cameraGO = new GameObject("PreviewCamera", typeof(Camera));
            cameraGO.transform.SetParent(rig.transform, false);
            cameraGO.transform.position = focusPoint + new Vector3(0f, 0f, -cameraDistance);
            cameraGO.transform.LookAt(focusPoint);

            Camera previewCamera = cameraGO.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << PenguinPreviewLayer;
            previewCamera.orthographic = false;
            previewCamera.fieldOfView = fieldOfView;
            previewCamera.nearClipPlane = 0.05f;
            previewCamera.farClipPlane = cameraDistance + extent * 3f;
            previewCamera.targetTexture = renderTexture;

            return renderTexture;
        }

        private const string PenguinPreviewRTPath = "Assets/scripts/UI/PenguinPreviewRT.renderTexture";

        private static RenderTexture GetOrCreatePreviewRenderTexture()
        {
            RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(PenguinPreviewRTPath);
            if (existing != null) {
                return existing;
            }

            RenderTexture renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) {
                name = "PenguinPreviewRT",
                antiAliasing = 2,
            };
            AssetDatabase.CreateAsset(renderTexture, PenguinPreviewRTPath);
            return renderTexture;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private const string TitleFontPath = "Assets/UI/Fonts/SantasAirMail.ttf";

        private static void BuildTitle(Transform canvasParent)
        {
            Font titleFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);

            Text title = CreateLabel(canvasParent, "TitleText", "FURIA DO PINGUIM", 58);
            StyleTitle(title);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -90f);
            titleRect.sizeDelta = new Vector2(1300f, 180f);

            Text subtitle = CreateLabel(canvasParent, "SubtitleText", "UMA AVENTURA CONGELANTE", 22);
            subtitle.fontStyle = FontStyle.Italic;
            RectTransform subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0.5f, 1f);
            subtitleRect.anchorMax = new Vector2(0.5f, 1f);
            subtitleRect.pivot = new Vector2(0.5f, 1f);
            subtitleRect.anchoredPosition = new Vector2(0f, -255f);
            subtitleRect.sizeDelta = new Vector2(900f, 70f);
            subtitle.color = IceLight;

            // Contorno/sombra mais finos que o titulo (proporcional ao tamanho bem menor do
            // texto) - usar o mesmo contorno do titulo ficava grosso/pesado demais aqui,
            // dando a impressao de ser uma fonte diferente.
            Outline subtitleOutline = subtitle.gameObject.AddComponent<Outline>();
            subtitleOutline.effectColor = IceOutline;
            subtitleOutline.effectDistance = new Vector2(1f, -1f);
            Shadow subtitleShadow = subtitle.gameObject.AddComponent<Shadow>();
            subtitleShadow.effectColor = new Color(0f, 0.6f, 0.9f, 0.5f);
            subtitleShadow.effectDistance = new Vector2(0f, -1.5f);

            if (titleFont != null) {
                title.font = titleFont;
                title.fontSize = 64;
                title.horizontalOverflow = HorizontalWrapMode.Overflow;
                title.verticalOverflow = VerticalWrapMode.Overflow;
                subtitle.font = titleFont;
                subtitle.fontStyle = FontStyle.Normal;
                subtitle.fontSize = 36;
                subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
                subtitle.verticalOverflow = VerticalWrapMode.Overflow;
            } else {
                Debug.LogWarning("MenuBuilder: fonte " + TitleFontPath + " nao encontrada - usando a fonte padrao no titulo.");
            }
        }

        private static void BuildIceFrame(Transform canvasParent)
        {
            GameObject frame = new GameObject("IceFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frame.transform.SetParent(canvasParent, false);
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            Image frameImage = frame.GetComponent<Image>();
            frameImage.sprite = GetSprite("Background.psd");
            frameImage.type = Image.Type.Sliced;
            frameImage.fillCenter = false;
            frameImage.color = IceAccent;
            frameImage.raycastTarget = false;
            frameImage.pixelsPerUnitMultiplier = 0.25f;
        }

        private static GameObject BuildExtrasPanel(Transform canvasParent)
        {
            GameObject panel = CreateFullStretchImage(canvasParent, "PopupExtras", new Color(0f, 0f, 0f, 0.55f)).gameObject;
            panel.AddComponent<CanvasGroup>();
            panel.AddComponent<UIPanelFader>();
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            GameObject card = BuildCard(panelRect, new Vector2(620f, 420f));

            Text title = CreateLabel(card.transform, "ExtrasTitle", "EXTRAS", 40);
            StyleTitle(title);
            PositionTop(title.rectTransform, -30f, 600f, 70f);

            Text body = CreateLabel(card.transform, "ExtrasBody", "Creditos, galeria e conquistas\nem breve.", 22);
            body.color = IceLight;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 10f);
            bodyRect.sizeDelta = new Vector2(540f, 140f);

            Button closeButton = CreateFrostedButton(card.transform, "CloseButton", "VOLTAR");
            PositionBottomCenter(closeButton.GetComponent<RectTransform>(), 24f, 240f, 50f);

            return panel;
        }

        private static GameObject BuildQuitConfirmPanel(Transform canvasParent)
        {
            GameObject panel = CreateFullStretchImage(canvasParent, "PopupQuitConfirm", new Color(0f, 0f, 0f, 0.6f)).gameObject;
            panel.AddComponent<CanvasGroup>();
            panel.AddComponent<UIPanelFader>();
            panel.AddComponent<QuitConfirmationController>();
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            GameObject card = BuildCard(panelRect, new Vector2(560f, 300f));

            Text title = CreateLabel(card.transform, "QuitTitle", "SAIR DO JOGO?", 34);
            StyleTitle(title);
            PositionTop(title.rectTransform, -40f, 500f, 60f);

            Text body = CreateLabel(card.transform, "QuitBody", "Tem certeza que deseja sair?", 22);
            body.color = IceLight;
            RectTransform bodyRect = body.rectTransform;
            bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
            bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = new Vector2(0f, 10f);
            bodyRect.sizeDelta = new Vector2(480f, 50f);

            Button yesButton = CreateFrostedButton(card.transform, "YesButton", "SIM");
            RectTransform yesRect = yesButton.GetComponent<RectTransform>();
            yesRect.anchorMin = new Vector2(0.5f, 0f);
            yesRect.anchorMax = new Vector2(0.5f, 0f);
            yesRect.pivot = new Vector2(1f, 0f);
            yesRect.anchoredPosition = new Vector2(-16f, 28f);
            yesRect.sizeDelta = new Vector2(200f, 54f);

            Button noButton = CreateFrostedButton(card.transform, "NoButton", "NAO");
            RectTransform noRect = noButton.GetComponent<RectTransform>();
            noRect.anchorMin = new Vector2(0.5f, 0f);
            noRect.anchorMax = new Vector2(0.5f, 0f);
            noRect.pivot = new Vector2(0f, 0f);
            noRect.anchoredPosition = new Vector2(16f, 28f);
            noRect.sizeDelta = new Vector2(200f, 54f);

            return panel;
        }

        private static GameObject BuildLoadingOverlay(Transform canvasParent, out CanvasGroup canvasGroup, out Slider progressBar)
        {
            Image overlayImage = CreateFullStretchImage(canvasParent, "LoadingOverlay", Color.black);
            GameObject overlay = overlayImage.gameObject;
            canvasGroup = overlay.AddComponent<CanvasGroup>();

            Text loadingText = CreateLabel(overlay.transform, "LoadingText", "CARREGANDO...", 32);
            loadingText.color = IceLight;
            RectTransform loadingRect = loadingText.rectTransform;
            loadingRect.anchorMin = new Vector2(0.5f, 0.5f);
            loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
            loadingRect.pivot = new Vector2(0.5f, 0.5f);
            loadingRect.anchoredPosition = new Vector2(0f, 40f);
            loadingRect.sizeDelta = new Vector2(600f, 60f);

            DefaultControls.Resources resources = GetStandardResources();
            GameObject sliderGO = DefaultControls.CreateSlider(resources);
            sliderGO.name = "LoadingBar";
            sliderGO.transform.SetParent(overlay.transform, false);
            RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, -20f);
            sliderRect.sizeDelta = new Vector2(520f, 24f);

            progressBar = sliderGO.GetComponent<Slider>();
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            progressBar.interactable = false;
            Transform fillTransform = sliderGO.transform.Find("Fill Area/Fill");
            if (fillTransform != null) {
                Image fillImage = fillTransform.GetComponent<Image>();
                if (fillImage != null) { fillImage.color = IceAccent; }
            }
            Object.DestroyImmediate(sliderGO.transform.Find("Handle Slide Area").gameObject);

            return overlay;
        }

        // ---------- Pause menu added to the Mountain scene ----------

        private static void BuildPauseMenuInMountainScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MountainScenePath, OpenSceneMode.Single);

            RemoveExistingMenuObjects(scene);
            EnsureEventSystem();

            GameObject canvasGO = CreateCanvas("PauseMenuCanvas");
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

            GameObject pausePanel = CreateFullStretchImage(canvasRect, "PausePanel", IceDark).gameObject;
            AddGradient(pausePanel.GetComponent<Image>(), IceMid, IceDark);
            RectTransform pausePanelRect = pausePanel.GetComponent<RectTransform>();
            pausePanel.AddComponent<CanvasGroup>();
            UIPanelFader pauseFader = pausePanel.AddComponent<UIPanelFader>();

            Text title = CreateLabel(pausePanelRect, "TitleText", "PAUSADO", 58);
            StyleTitle(title);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(900f, 100f);

            GameObject buttonsPanel = CreateVerticalGroup(pausePanelRect, "PauseButtons", 18f, 420f);
            RectTransform buttonsRect = buttonsPanel.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonsRect.pivot = new Vector2(0.5f, 0.5f);
            buttonsRect.anchoredPosition = Vector2.zero;

            GameObject settingsPanel = BuildSettingsPanel(canvasRect, out SettingsPanelController settingsController);

            GameObject controllerGO = new GameObject("PauseMenuController");
            PauseMenuController pauseMenuController = controllerGO.AddComponent<PauseMenuController>();
            SerializedObject so = new SerializedObject(pauseMenuController);
            so.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
            so.ApplyModifiedProperties();

            Button resumeButton = CreateFrostedButton(buttonsRect, "ResumeButton", "CONTINUAR");
            UnityEventTools.AddVoidPersistentListener(resumeButton.onClick, pauseMenuController.Resume);

            Button restartButton = CreateFrostedButton(buttonsRect, "RestartButton", "RESTART");
            UnityEventTools.AddVoidPersistentListener(restartButton.onClick, pauseMenuController.Restart);

            Button settingsButton = CreateFrostedButton(buttonsRect, "SettingsButton", "CONFIGURACOES");
            UnityEventTools.AddVoidPersistentListener(settingsButton.onClick, pauseMenuController.OpenSettings);

            Button mainMenuButton = CreateFrostedButton(buttonsRect, "MainMenuButton", "MENU PRINCIPAL");
            UnityEventTools.AddVoidPersistentListener(mainMenuButton.onClick, pauseMenuController.GoToMainMenu);

            Button quitButton = CreateFrostedButton(buttonsRect, "QuitButton", "SAIR");
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, pauseMenuController.QuitGame);

            Button closeSettingsButton = settingsPanel.transform.Find("Card/CloseButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(closeSettingsButton.onClick, settingsController.Close);

            pauseFader.SetInstant(false);
            settingsPanel.GetComponent<UIPanelFader>().SetInstant(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void RemoveExistingMenuObjects(Scene scene)
        {
            string[] namesToRemove = { "PauseMenuCanvas", "PauseMenuController" };
            foreach (GameObject root in scene.GetRootGameObjects()) {
                foreach (string name in namesToRemove) {
                    if (root.name == name) {
                        Object.DestroyImmediate(root);
                        break;
                    }
                }
            }
        }

        // ---------- Build settings ----------

        private static void SetupBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MountainScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }

        // ---------- Snow ambience ----------

        private static void CreateSnowfall()
        {
            GameObject snowGO = new GameObject("Snowfall", typeof(ParticleSystem));
            snowGO.transform.position = new Vector3(0f, 6f, 4f);

            ParticleSystem ps = snowGO.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 35f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 0.1f, 4f);

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.8f, -0.4f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer renderer = snowGO.GetComponent<ParticleSystemRenderer>();
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }

        // ---------- Shared UI construction helpers ----------

        private static DefaultControls.Resources GetStandardResources()
        {
            DefaultControls.Resources resources = new DefaultControls.Resources();
            resources.standard = GetSprite("UISprite.psd");
            resources.background = GetSprite("Background.psd");
            resources.inputField = GetSprite("InputFieldBackground.psd");
            resources.knob = GetSprite("Knob.psd");
            resources.checkmark = GetSprite("Checkmark.psd");
            resources.dropdown = GetSprite("DropdownArrow.psd");
            resources.mask = GetSprite("UIMask.psd");
            return resources;
        }

        private static Sprite GetSprite(string fileName)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/" + fileName);
        }

        /// <summary>
        /// Carrega uma imagem de arte (fundo, atlas de botoes, etc.) como Sprite, reconfigurando
        /// a importacao para Texture Type = Sprite se ainda nao estiver assim. Retorna null se o
        /// arquivo nao existir - quem chama deve ter um fallback (placeholder procedural).
        /// </summary>
        private static Sprite LoadAsSprite(string path)
        {
            if (!File.Exists(path)) {
                return null;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite) {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) {
                return;
            }
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static GameObject CreateCanvas(string name)
        {
            GameObject canvasGO = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGO;
        }

        private static Image CreateFullStretchImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static void AddGradient(Image image, Color top, Color bottom)
        {
            UIGradient gradient = image.gameObject.AddComponent<UIGradient>();
            SerializedObject so = new SerializedObject(gradient);
            so.FindProperty("topColor").colorValue = top;
            so.FindProperty("bottomColor").colorValue = bottom;
            so.ApplyModifiedProperties();
        }

        private static Text CreateLabel(Transform parent, string name, string text, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return label;
        }

        private static void StyleTitle(Text title)
        {
            title.color = IceLight;
            title.fontStyle = FontStyle.Bold;

            Outline outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = IceOutline;
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.6f, 0.9f, 0.5f);
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        private static void StyleButtonText(Text buttonText, Color textColor)
        {
            buttonText.color = textColor;
            buttonText.fontStyle = FontStyle.Bold;

            Outline outline = buttonText.gameObject.AddComponent<Outline>();
            outline.effectColor = IceOutline;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private static GameObject CreateVerticalGroup(Transform parent, string name, float spacing, float width, bool controlChildWidth = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 0f);

            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = controlChildWidth ? TextAnchor.UpperCenter : TextAnchor.UpperRight;
            layout.childControlWidth = controlChildWidth;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = controlChildWidth;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        /// <summary>
        /// Generic button used for popups (frosted-glass ice styling): no icon slot.
        /// </summary>
        private static Button CreateFrostedButton(Transform parent, string name, string label)
        {
            Button button = CreateBaseButton(parent, name, label, 54f, FrostedNormal, FrostedHighlighted, FrostedPressed, FrostedSelected, IceLight);
            return button;
        }

        /// <summary>
        /// Main-menu action button: wood plank styling with a snow cap and a left-side icon slot
        /// (placeholder Image - swap the sprite for the real icon art).
        /// </summary>
        private static Button CreateWoodButton(Transform parent, string name, string label, Color baseColor, string spriteName)
        {
            Sprite woodSprite = LoadAsSprite(IndividualButtonSpritesDir + "/" + spriteName + ".png");
            if (woodSprite != null) {
                return CreateArtButton(parent, name, woodSprite, label);
            }

            Color highlighted = baseColor == WoodNormal ? WoodHighlighted : FrostedHighlighted;
            Color pressed = baseColor == WoodNormal ? WoodPressed : FrostedPressed;
            Button button = CreateBaseButton(parent, name, label, 72f, baseColor, highlighted, pressed, highlighted, IceLight);
            GameObject go = button.gameObject;

            // Snow cap placeholder on top of the plank - troque pelos sprites "Btn_Play" /
            // "Btn_Options" / "Btn_Extras" / "Btn_Quit" do atlas (Tools > Furia Pinguim > 1 e 2).
            GameObject snowCap = new GameObject("SnowCap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            snowCap.transform.SetParent(go.transform, false);
            RectTransform snowRect = snowCap.GetComponent<RectTransform>();
            snowRect.anchorMin = new Vector2(0f, 1f);
            snowRect.anchorMax = new Vector2(1f, 1f);
            snowRect.pivot = new Vector2(0.5f, 1f);
            snowRect.anchoredPosition = new Vector2(0f, 4f);
            snowRect.sizeDelta = new Vector2(-10f, 14f);
            Image snowImage = snowCap.GetComponent<Image>();
            snowImage.sprite = GetSprite("UISprite.psd");
            snowImage.type = Image.Type.Sliced;
            snowImage.color = SnowCap;
            snowImage.raycastTarget = false;

            // Icon slot placeholder - swap the sprite for the real icon (play/gear/star/door).
            GameObject iconSlot = new GameObject("IconSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconSlot.transform.SetParent(go.transform, false);
            RectTransform iconRect = iconSlot.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(22f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            Image iconImage = iconSlot.GetComponent<Image>();
            iconImage.sprite = GetSprite("Knob.psd");
            iconImage.color = IceAccent;
            iconImage.raycastTarget = false;

            Text buttonText = go.GetComponentInChildren<Text>();
            RectTransform textRect = buttonText.rectTransform;
            textRect.offsetMin = new Vector2(78f, textRect.offsetMin.y);

            return button;
        }

        /// <summary>
        /// Botao usando a arte real da prancha de madeira (ja vem com neve e icone desenhados -
        /// nao precisa de SnowCap/IconSlot separados). Adiciona o rotulo de texto por cima,
        /// deslocado pra direita do icone que ja esta desenhado na arte.
        /// </summary>
        private static Button CreateArtButton(Transform parent, string name, Sprite sprite, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 3f;
            float height = 84f;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(height * aspect, height);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            layoutElement.preferredWidth = height * aspect;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            // Simple (nao Sliced): o RectTransform ja usa exatamente a proporcao nativa do
            // sprite (linha acima), entao nao ha distorcao a corrigir - e o 9-slice com bordas
            // calculadas pra resolucao da arte ficava gigante demais pros 84px de altura do
            // botao, cortando a prancha.
            image.type = Image.Type.Simple;

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            go.AddComponent<UIButtonFeedback>();

            GameObject textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(go.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.38f, 0.1f);
            textRect.anchorMax = new Vector2(0.95f, 0.72f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text buttonLabel = textGO.GetComponent<Text>();
            buttonLabel.text = label;
            buttonLabel.alignment = TextAnchor.MiddleLeft;
            buttonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            buttonLabel.verticalOverflow = VerticalWrapMode.Truncate;
            buttonLabel.raycastTarget = false;

            Font customFont = AssetDatabase.LoadAssetAtPath<Font>(TitleFontPath);
            buttonLabel.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonLabel.color = IceLight;
            buttonLabel.fontStyle = customFont != null ? FontStyle.Normal : FontStyle.Bold;
            // Best Fit encolhe a fonte pra sempre caber dentro da prancha, independente do
            // tamanho da palavra (JOGAR vs OPCOES/EXTRAS) - evita o texto vazar pra fora.
            buttonLabel.resizeTextForBestFit = true;
            buttonLabel.resizeTextMinSize = 10;
            buttonLabel.resizeTextMaxSize = customFont != null ? 30 : 24;

            Outline labelOutline = textGO.AddComponent<Outline>();
            labelOutline.effectColor = IceOutline;
            labelOutline.effectDistance = new Vector2(1.5f, -1.5f);

            return button;
        }

        private static Button CreateBaseButton(Transform parent, string name, string label, float height,
            Color normal, Color highlighted, Color pressed, Color selected, Color textColor)
        {
            DefaultControls.Resources resources = GetStandardResources();
            GameObject go = DefaultControls.CreateButton(resources);
            go.name = name;
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            Image buttonImage = go.GetComponent<Image>();
            buttonImage.color = normal;

            Button button = go.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = selected;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.05f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            go.AddComponent<UIButtonFeedback>();

            Text buttonText = go.GetComponentInChildren<Text>();
            buttonText.text = label;
            buttonText.fontSize = 28;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            StyleButtonText(buttonText, textColor);

            return button;
        }

        private static GameObject BuildCard(Transform parent, Vector2 size)
        {
            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(parent, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = size;
            Image cardImage = card.GetComponent<Image>();
            cardImage.color = IceMid;
            AddGradient(cardImage, IceMid, IceDark);

            Outline cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = IceAccent;
            cardOutline.effectDistance = new Vector2(2f, -2f);

            return card;
        }

        private static void PositionTop(RectTransform rect, float yOffset, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, yOffset);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void PositionBottomCenter(RectTransform rect, float yOffset, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, yOffset);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateSettingsRow(Transform parent, string name, string labelText)
        {
            GameObject row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 50f;

            GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            labelGO.transform.SetParent(row.transform, false);
            Text label = labelGO.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = labelText;
            label.fontSize = 22;
            label.color = IceLight;
            label.alignment = TextAnchor.MiddleLeft;
            LayoutElement labelLayout = labelGO.GetComponent<LayoutElement>();
            labelLayout.preferredWidth = 260f;
            labelLayout.minWidth = 260f;

            return row.GetComponent<RectTransform>();
        }

        private static Slider AddSliderControl(RectTransform row, string name, float min, float max, float value)
        {
            DefaultControls.Resources resources = GetStandardResources();
            GameObject go = DefaultControls.CreateSlider(resources);
            go.name = name;
            go.transform.SetParent(row, false);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 20f;

            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            Transform fillTransform = go.transform.Find("Fill Area/Fill");
            if (fillTransform != null) {
                Image fillImage = fillTransform.GetComponent<Image>();
                if (fillImage != null) { fillImage.color = IceAccent; }
            }
            Transform handleTransform = go.transform.Find("Handle Slide Area/Handle");
            if (handleTransform != null) {
                Image handleImage = handleTransform.GetComponent<Image>();
                if (handleImage != null) { handleImage.color = IceLight; }
            }

            return slider;
        }

        private static Dropdown AddDropdownControl(RectTransform row, string name)
        {
            DefaultControls.Resources resources = GetStandardResources();
            GameObject go = DefaultControls.CreateDropdown(resources);
            go.name = name;
            go.transform.SetParent(row, false);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.preferredHeight = 40f;
            return go.GetComponent<Dropdown>();
        }

        private static Toggle AddToggleControl(RectTransform row, string name)
        {
            DefaultControls.Resources resources = GetStandardResources();
            GameObject go = DefaultControls.CreateToggle(resources);
            go.name = name;
            go.transform.SetParent(row, false);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 40f;
            layoutElement.preferredHeight = 40f;

            Text toggleLabel = go.GetComponentInChildren<Text>();
            if (toggleLabel != null) {
                Object.DestroyImmediate(toggleLabel.gameObject);
            }

            Toggle toggle = go.GetComponent<Toggle>();
            Transform checkmarkTransform = go.transform.Find("Background/Checkmark");
            if (checkmarkTransform != null) {
                Image checkmarkImage = checkmarkTransform.GetComponent<Image>();
                if (checkmarkImage != null) { checkmarkImage.color = IceAccent; }
            }

            return toggle;
        }

        private static GameObject BuildSettingsPanel(Transform canvasParent, out SettingsPanelController controller)
        {
            GameObject panel = CreateFullStretchImage(canvasParent, "PopupOptions", new Color(0f, 0f, 0f, 0.55f)).gameObject;
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panel.AddComponent<CanvasGroup>();
            panel.AddComponent<UIPanelFader>();

            GameObject card = BuildCard(panelRect, new Vector2(760f, 760f));
            RectTransform cardRect = card.GetComponent<RectTransform>();

            Text title = CreateLabel(cardRect, "SettingsTitle", "CONFIGURACOES", 40);
            StyleTitle(title);
            PositionTop(title.rectTransform, -30f, 600f, 70f);

            GameObject rowsContainer = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsContainer.transform.SetParent(cardRect, false);
            RectTransform rowsRect = rowsContainer.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.5f, 1f);
            rowsRect.anchorMax = new Vector2(0.5f, 1f);
            rowsRect.pivot = new Vector2(0.5f, 1f);
            rowsRect.anchoredPosition = new Vector2(0f, -110f);
            rowsRect.sizeDelta = new Vector2(680f, 540f);

            VerticalLayoutGroup rowsLayout = rowsContainer.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 16f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            RectTransform masterRow = CreateSettingsRow(rowsRect, "MasterVolumeRow", "Volume Geral");
            Slider masterSlider = AddSliderControl(masterRow, "MasterVolumeSlider", 0f, 1f, 1f);

            RectTransform musicRow = CreateSettingsRow(rowsRect, "MusicVolumeRow", "Musica");
            Slider musicSlider = AddSliderControl(musicRow, "MusicVolumeSlider", 0f, 1f, 1f);

            RectTransform sfxRow = CreateSettingsRow(rowsRect, "SfxVolumeRow", "Efeitos");
            Slider sfxSlider = AddSliderControl(sfxRow, "SfxVolumeSlider", 0f, 1f, 1f);

            RectTransform sensitivityRow = CreateSettingsRow(rowsRect, "SensitivityRow", "Sensibilidade do Mouse");
            Slider sensitivitySlider = AddSliderControl(sensitivityRow, "SensitivitySlider", 0.1f, 3f, 1f);

            RectTransform qualityRow = CreateSettingsRow(rowsRect, "QualityRow", "Qualidade Grafica");
            Dropdown qualityDropdown = AddDropdownControl(qualityRow, "QualityDropdown");

            RectTransform fullscreenRow = CreateSettingsRow(rowsRect, "FullscreenRow", "Tela Cheia");
            Toggle fullscreenToggle = AddToggleControl(fullscreenRow, "FullscreenToggle");

            RectTransform resolutionRow = CreateSettingsRow(rowsRect, "ResolutionRow", "Resolucao");
            Dropdown resolutionDropdown = AddDropdownControl(resolutionRow, "ResolutionDropdown");

            Button closeButton = CreateFrostedButton(cardRect, "CloseButton", "VOLTAR");
            PositionBottomCenter(closeButton.GetComponent<RectTransform>(), 24f, 240f, 50f);
            Object.DestroyImmediate(closeButton.GetComponent<LayoutElement>());

            controller = panel.AddComponent<SettingsPanelController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("sensitivitySlider").objectReferenceValue = sensitivitySlider;
            so.FindProperty("qualityDropdown").objectReferenceValue = qualityDropdown;
            so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
            so.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
            so.ApplyModifiedProperties();

            UnityEventTools.AddPersistentListener(masterSlider.onValueChanged, controller.OnMasterVolumeChanged);
            UnityEventTools.AddPersistentListener(musicSlider.onValueChanged, controller.OnMusicVolumeChanged);
            UnityEventTools.AddPersistentListener(sfxSlider.onValueChanged, controller.OnSfxVolumeChanged);
            UnityEventTools.AddPersistentListener(sensitivitySlider.onValueChanged, controller.OnSensitivityChanged);
            UnityEventTools.AddPersistentListener(qualityDropdown.onValueChanged, controller.OnQualityChanged);
            UnityEventTools.AddPersistentListener(fullscreenToggle.onValueChanged, controller.OnFullscreenChanged);
            UnityEventTools.AddPersistentListener(resolutionDropdown.onValueChanged, controller.OnResolutionChanged);

            return panel;
        }
    }
}
