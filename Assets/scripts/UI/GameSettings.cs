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
        private const string ResolutionIndexKey = "Settings_ResolutionIndex";

        public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        public static float MouseSensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);

        public static void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, value);
            AudioListener.volume = value;
        }

        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicVolumeKey, value);
        }

        public static void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SfxVolumeKey, value);
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
            Screen.fullScreen = value;
        }

        public static int GetResolutionIndex()
        {
            return PlayerPrefs.GetInt(ResolutionIndexKey, -1);
        }

        public static void SetResolutionIndex(int index)
        {
            PlayerPrefs.SetInt(ResolutionIndexKey, index);
            Resolution[] resolutions = Screen.resolutions;
            if (index >= 0 && index < resolutions.Length) {
                Resolution resolution = resolutions[index];
                Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
            }
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ApplyAll()
        {
            AudioListener.volume = MasterVolume;
            QualitySettings.SetQualityLevel(GetQualityLevel(), true);
            Screen.fullScreen = GetFullscreen();

            CameraFollow cameraFollow = Object.FindAnyObjectByType<CameraFollow>();
            if (cameraFollow != null) {
                cameraFollow.SetSensitivityMultiplier(MouseSensitivity);
            }
        }
    }
}
