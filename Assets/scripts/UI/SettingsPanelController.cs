using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LinuxFuriaPinguim.UI
{
    public class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private Dropdown qualityDropdown;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Dropdown resolutionDropdown;

        private Resolution[] m_resolutions;

        private void OnEnable()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (masterVolumeSlider != null) { masterVolumeSlider.SetValueWithoutNotify(GameSettings.MasterVolume); }
            if (musicVolumeSlider != null) { musicVolumeSlider.SetValueWithoutNotify(GameSettings.MusicVolume); }
            if (sfxVolumeSlider != null) { sfxVolumeSlider.SetValueWithoutNotify(GameSettings.SfxVolume); }
            if (sensitivitySlider != null) { sensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity); }

            if (qualityDropdown != null) {
                qualityDropdown.ClearOptions();
                qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
                qualityDropdown.SetValueWithoutNotify(GameSettings.GetQualityLevel());
                qualityDropdown.RefreshShownValue();
            }

            if (fullscreenToggle != null) { fullscreenToggle.SetIsOnWithoutNotify(GameSettings.GetFullscreen()); }

            if (resolutionDropdown != null) {
                m_resolutions = Screen.resolutions;
                List<string> options = new List<string>();
                int currentIndex = GameSettings.GetResolutionIndex();
                for (int i = 0; i < m_resolutions.Length; i++) {
                    Resolution resolution = m_resolutions[i];
                    options.Add(resolution.width + " x " + resolution.height);
                    if (currentIndex < 0 && resolution.width == Screen.currentResolution.width &&
                        resolution.height == Screen.currentResolution.height) {
                        currentIndex = i;
                    }
                }
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.SetValueWithoutNotify(Mathf.Max(currentIndex, 0));
                resolutionDropdown.RefreshShownValue();
            }
        }

        public void OnMasterVolumeChanged(float value)
        {
            GameSettings.SetMasterVolume(value);
        }

        public void OnMusicVolumeChanged(float value)
        {
            GameSettings.SetMusicVolume(value);
        }

        public void OnSfxVolumeChanged(float value)
        {
            GameSettings.SetSfxVolume(value);
        }

        public void OnSensitivityChanged(float value)
        {
            GameSettings.SetMouseSensitivity(value);
        }

        public void OnQualityChanged(int index)
        {
            GameSettings.SetQualityLevel(index);
        }

        public void OnFullscreenChanged(bool value)
        {
            GameSettings.SetFullscreen(value);
        }

        public void OnResolutionChanged(int index)
        {
            GameSettings.SetResolutionIndex(index);
        }

        public void Close()
        {
            GameSettings.Save();
            gameObject.SetActive(false);
        }
    }
}
