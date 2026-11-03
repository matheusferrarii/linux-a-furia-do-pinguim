using System.Collections.Generic;
using LinuxFuriaPinguim.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.EditorTools
{
    public static class MenuBuilder
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string MountainScenePath = "Assets/Scenes/Mountain.unity";

        private static Color BackgroundColor => new Color(0.05f, 0.08f, 0.12f, 0.92f);
        private static Color PanelColor => new Color(0.1f, 0.13f, 0.18f, 0.96f);

        [MenuItem("Tools/Furia Pinguim/Build Menus")]
        public static void BuildMenus()
        {
            BuildMainMenuScene();
            BuildPauseMenuInMountainScene();
            SetupBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("MenuBuilder: menus criados com sucesso.");
        }

        // ---------- Main Menu scene ----------

        private static void BuildMainMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";
            cameraGO.transform.position = new Vector3(0f, 1f, -10f);

            EnsureEventSystem();

            GameObject canvasGO = CreateCanvas("MainMenuCanvas");
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

            CreateFullStretchImage(canvasRect, "Background", BackgroundColor);

            Text title = CreateLabel(canvasRect, "TitleText", "FURIA DO PINGUIM", 56);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(900f, 100f);

            GameObject buttonsPanel = CreateVerticalGroup(canvasRect, "MainButtons", 20f, 320f);
            RectTransform buttonsRect = buttonsPanel.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonsRect.pivot = new Vector2(0.5f, 0.5f);
            buttonsRect.anchoredPosition = Vector2.zero;

            GameObject settingsPanel = BuildSettingsPanel(canvasRect, out SettingsPanelController settingsController);

            GameObject controllerGO = new GameObject("MainMenuController");
            MainMenuController mainMenuController = controllerGO.AddComponent<MainMenuController>();
            SerializedObject so = new SerializedObject(mainMenuController);
            so.FindProperty("gameSceneName").stringValue = "Mountain";
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.ApplyModifiedProperties();

            Button startButton = CreateButton(buttonsRect, "StartButton", "INICIAR", 60f);
            UnityEventTools.AddVoidPersistentListener(startButton.onClick, mainMenuController.StartGame);

            Button settingsButton = CreateButton(buttonsRect, "SettingsButton", "CONFIGURACOES", 60f);
            UnityEventTools.AddVoidPersistentListener(settingsButton.onClick, mainMenuController.OpenSettings);

            Button quitButton = CreateButton(buttonsRect, "QuitButton", "SAIR", 60f);
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, mainMenuController.QuitGame);

            Button closeSettingsButton = settingsPanel.transform.Find("Card/CloseButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(closeSettingsButton.onClick, settingsController.Close);

            settingsPanel.SetActive(false);

            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        // ---------- Pause menu added to the Mountain scene ----------

        private static void BuildPauseMenuInMountainScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MountainScenePath, OpenSceneMode.Single);

            EnsureEventSystem();

            GameObject canvasGO = CreateCanvas("PauseMenuCanvas");
            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();

            GameObject pausePanel = CreateFullStretchImage(canvasRect, "PausePanel", BackgroundColor).gameObject;
            RectTransform pausePanelRect = pausePanel.GetComponent<RectTransform>();

            Text title = CreateLabel(pausePanelRect, "TitleText", "PAUSADO", 56);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -80f);
            titleRect.sizeDelta = new Vector2(900f, 100f);

            GameObject buttonsPanel = CreateVerticalGroup(pausePanelRect, "PauseButtons", 16f, 400f);
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

            Button resumeButton = CreateButton(buttonsRect, "ResumeButton", "CONTINUAR", 56f);
            UnityEventTools.AddVoidPersistentListener(resumeButton.onClick, pauseMenuController.Resume);

            Button restartButton = CreateButton(buttonsRect, "RestartButton", "RESTART", 56f);
            UnityEventTools.AddVoidPersistentListener(restartButton.onClick, pauseMenuController.Restart);

            Button settingsButton = CreateButton(buttonsRect, "SettingsButton", "CONFIGURACOES", 56f);
            UnityEventTools.AddVoidPersistentListener(settingsButton.onClick, pauseMenuController.OpenSettings);

            Button mainMenuButton = CreateButton(buttonsRect, "MainMenuButton", "MENU PRINCIPAL", 56f);
            UnityEventTools.AddVoidPersistentListener(mainMenuButton.onClick, pauseMenuController.GoToMainMenu);

            Button quitButton = CreateButton(buttonsRect, "QuitButton", "SAIR", 56f);
            UnityEventTools.AddVoidPersistentListener(quitButton.onClick, pauseMenuController.QuitGame);

            Button closeSettingsButton = settingsPanel.transform.Find("Card/CloseButton").GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(closeSettingsButton.onClick, settingsController.Close);

            pausePanel.SetActive(false);
            settingsPanel.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------- Build settings ----------

        private static void SetupBuildSettings()
        {
            string mainMenuGuid = AssetDatabase.AssetPathToGUID(MainMenuScenePath);
            string mountainGuid = AssetDatabase.AssetPathToGUID(MountainScenePath);

            EditorBuildSettingsScene[] scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MountainScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }

        // ---------- Shared UI construction helpers ----------

        private static DefaultControls.Resources GetStandardResources()
        {
            DefaultControls.Resources resources = new DefaultControls.Resources();
            resources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            resources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            resources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            resources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            resources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            resources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
            resources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            return resources;
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

        private static GameObject CreateVerticalGroup(Transform parent, string name, float spacing, float width)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 0f);

            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, float height)
        {
            DefaultControls.Resources resources = GetStandardResources();
            GameObject go = DefaultControls.CreateButton(resources);
            go.name = name;
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            LayoutElement layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            Text buttonText = go.GetComponentInChildren<Text>();
            buttonText.text = label;
            buttonText.fontSize = 28;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return go.GetComponent<Button>();
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
            label.color = Color.white;
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
            return go.GetComponent<Toggle>();
        }

        private static GameObject BuildSettingsPanel(Transform canvasParent, out SettingsPanelController controller)
        {
            GameObject panel = CreateFullStretchImage(canvasParent, "SettingsPanel", BackgroundColor).gameObject;
            RectTransform panelRect = panel.GetComponent<RectTransform>();

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(panelRect, false);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(760f, 620f);
            card.GetComponent<Image>().color = PanelColor;

            Text title = CreateLabel(cardRect, "SettingsTitle", "CONFIGURACOES", 40);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -30f);
            titleRect.sizeDelta = new Vector2(600f, 70f);

            GameObject rowsContainer = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsContainer.transform.SetParent(cardRect, false);
            RectTransform rowsRect = rowsContainer.GetComponent<RectTransform>();
            rowsRect.anchorMin = new Vector2(0.5f, 1f);
            rowsRect.anchorMax = new Vector2(0.5f, 1f);
            rowsRect.pivot = new Vector2(0.5f, 1f);
            rowsRect.anchoredPosition = new Vector2(0f, -110f);
            rowsRect.sizeDelta = new Vector2(680f, 440f);

            VerticalLayoutGroup rowsLayout = rowsContainer.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 16f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = false;
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

            Button closeButton = CreateButton(cardRect, "CloseButton", "VOLTAR", 50f);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 24f);
            closeRect.sizeDelta = new Vector2(240f, 50f);
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
