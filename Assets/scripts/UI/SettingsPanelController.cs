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
                List<string> options = new List<string>();
                foreach (Vector2Int size in GameSettings.GetAvailableResolutions()) {
                    options.Add(size.x + " x " + size.y);
                }
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.SetValueWithoutNotify(GameSettings.GetResolutionIndex());
                resolutionDropdown.RefreshShownValue();
            }
        }

        public void OnMasterVolumeChanged(float value)
        {
            GameSettings.SetMasterVolume(value);
            AudioManager.PlaySfxPreview();
        }

        public void OnMusicVolumeChanged(float value)
        {
            GameSettings.SetMusicVolume(value);
        }

        public void OnSfxVolumeChanged(float value)
        {
            GameSettings.SetSfxVolume(value);
            AudioManager.PlaySfxPreview();
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
            UIPanelFader fader = GetComponent<UIPanelFader>();
            if (fader != null) {
                fader.Hide();
            } else {
                gameObject.SetActive(false);
            }
        }
    }
}
