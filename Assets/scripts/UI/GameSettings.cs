using System.Collections.Generic;
using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    public static class GameSettings
    {
        private const string MasterVolumeKey = "Settings_MasterVolume";
        private const string MusicVolumeKey = "Settings_MusicVolume";
        private const string SfxVolumeKey = "Settings_SfxVolume";
        private const string SensitivityKey = "Settings_MouseSensitivity";
        private const string QualityKey = "Settings_QualityLevel";
        private const string FullscreenKey = "Settings_Fullscreen";
        private const string ResolutionWidthKey = "Settings_ResolutionWidth";
        private const string ResolutionHeightKey = "Settings_ResolutionHeight";

        private const int MinListedWidth = 800;

        public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        public static float MouseSensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);

        public static void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }

        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
            AudioManager.RefreshMusicVolume();
        }

        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static void SetMouseSensitivity(float value)
        {
            PlayerPrefs.SetFloat(SensitivityKey, value);
            CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null) {
                cameraFollow.SetSensitivityMultiplier(value);
            }
        }

        public static int GetQualityLevel()
        {
            return PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        }

        public static void SetQualityLevel(int index)
        {
            PlayerPrefs.SetInt(QualityKey, index);
            QualitySettings.SetQualityLevel(index, true);
        }

        public static bool GetFullscreen()
        {
            return PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        }

        public static void SetFullscreen(bool value)
        {
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyDisplay();
        }

        /// <summary>
        /// Resolucoes unicas (a lista do Unity repete a mesma largura x altura uma vez por taxa de
        /// atualizacao), em ordem crescente. Sempre inclui a resolucao atual.
        /// </summary>
        public static List<Vector2Int> GetAvailableResolutions()
        {
            List<Vector2Int> list = new List<Vector2Int>();
            foreach (Resolution resolution in Screen.resolutions) {
                Vector2Int size = new Vector2Int(resolution.width, resolution.height);
                if (resolution.width >= MinListedWidth && !list.Contains(size)) {
                    list.Add(size);
                }
            }

            Vector2Int target = GetTargetResolution();
            if (!list.Contains(target)) {
                list.Add(target);
            }

            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        public static int GetResolutionIndex()
        {
            List<Vector2Int> list = GetAvailableResolutions();
            int index = list.IndexOf(GetTargetResolution());
            return index >= 0 ? index : list.Count - 1;
        }

        public static void SetResolutionIndex(int index)
        {
            List<Vector2Int> list = GetAvailableResolutions();
            if (index < 0 || index >= list.Count) {
                return;
            }
            PlayerPrefs.SetInt(ResolutionWidthKey, list[index].x);
            PlayerPrefs.SetInt(ResolutionHeightKey, list[index].y);
            ApplyDisplay();
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            AudioListener.volume = MasterVolume;
            AudioManager.RefreshMusicVolume();
            QualitySettings.SetQualityLevel(GetQualityLevel(), true);
            ApplyDisplay();

            CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null) {
                cameraFollow.SetSensitivityMultiplier(MouseSensitivity);
            }
        }

        private static Vector2Int GetTargetResolution()
        {
            if (PlayerPrefs.HasKey(ResolutionWidthKey) && PlayerPrefs.HasKey(ResolutionHeightKey)) {
                return new Vector2Int(PlayerPrefs.GetInt(ResolutionWidthKey), PlayerPrefs.GetInt(ResolutionHeightKey));
            }
            if (GetFullscreen()) {
                Resolution desktop = Screen.currentResolution;
                return new Vector2Int(desktop.width, desktop.height);
            }
            return new Vector2Int(Screen.width, Screen.height);
        }

        /// <summary>
        /// Tela cheia usa o modo exclusivo (e o que realmente troca a resolucao do monitor); janela
        /// usa o tamanho escolhido. So mexe na tela se o jogador ja salvou alguma escolha, e so
        /// quando algo realmente mudou (evita piscar a cada troca de cena). Obs: no Editor do Unity
        /// a janela Game nao muda de tamanho - isso so aparece num build do jogo.
        /// </summary>
        private static void ApplyDisplay()
        {
            if (!PlayerPrefs.HasKey(FullscreenKey) && !PlayerPrefs.HasKey(ResolutionWidthKey)) {
                return;
            }

            Vector2Int target = GetTargetResolution();
            FullScreenMode mode = GetFullscreen() ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;

            if (Screen.width == target.x && Screen.height == target.y && Screen.fullScreenMode == mode) {
                return;
            }
            Screen.SetResolution(target.x, target.y, mode);
        }
    }
}
