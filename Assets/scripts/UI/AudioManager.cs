using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager s_instance;

        [SerializeField] private AudioClip defaultBgm;

        private AudioSource m_bgmSource;
        private AudioSource m_sfxSource;

        public static AudioManager Instance
        {
            get
            {
                if (s_instance == null) {
                    s_instance = Object.FindAnyObjectByType<AudioManager>();
                }
                if (s_instance == null) {
                    GameObject go = new GameObject("AudioManager");
                    s_instance = go.AddComponent<AudioManager>();
                }
                return s_instance;
            }
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this) {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);

            m_bgmSource = gameObject.AddComponent<AudioSource>();
            m_bgmSource.loop = true;
            m_bgmSource.playOnAwake = false;

            m_sfxSource = gameObject.AddComponent<AudioSource>();
            m_sfxSource.playOnAwake = false;
        }

        private void Start()
        {
            if (defaultBgm != null) {
                PlayBgm(defaultBgm);
            }
        }

        public void PlayBgm(AudioClip clip)
        {
            if (clip == null || m_bgmSource.clip == clip) {
                return;
            }
            m_bgmSource.clip = clip;
            m_bgmSource.volume = GameSettings.MusicVolume;
            m_bgmSource.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null) {
                return;
            }
            m_sfxSource.PlayOneShot(clip, GameSettings.SfxVolume);
        }

        public void RefreshVolumes()
        {
            m_bgmSource.volume = GameSettings.MusicVolume;
        }
    }
}
