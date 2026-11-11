using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager s_instance;

        [SerializeField] private AudioClip defaultBgm;

        private AudioSource m_bgmSource;
        private AudioSource m_sfxSource;
        private AudioClip m_previewClip;
        private float m_lastPreviewTime = -1f;

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
            if (clip == null) {
                return;
            }
            m_bgmSource.volume = GameSettings.MusicVolume;
            if (m_bgmSource.clip == clip && m_bgmSource.isPlaying) {
                return;
            }
            m_bgmSource.clip = clip;
            m_bgmSource.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null) {
                return;
            }
            m_sfxSource.PlayOneShot(clip, GameSettings.SfxVolume);
        }

        /// <summary>
        /// Chamado quando o slider de Musica muda: aplica na hora na musica que ja esta tocando
        /// (antes o volume so era lido ao trocar de faixa). Nao cria o AudioManager se nao existir.
        /// </summary>
        public static void RefreshMusicVolume()
        {
            if (s_instance != null && s_instance.m_bgmSource != null) {
                s_instance.m_bgmSource.volume = GameSettings.MusicVolume;
            }
        }

        /// <summary>
        /// Toca um bip curto no volume de efeitos atual, pra dar retorno sonoro ao arrastar o slider
        /// de Efeitos. Limitado a um bip a cada ~0.12s pra nao virar uma metralhadora durante o arrasto.
        /// </summary>
        public static void PlaySfxPreview()
        {
            AudioManager manager = Instance;
            if (Time.unscaledTime - manager.m_lastPreviewTime < 0.12f) {
                return;
            }
            manager.m_lastPreviewTime = Time.unscaledTime;

            if (manager.m_previewClip == null) {
                manager.m_previewClip = CreateBlip();
            }
            manager.PlaySfx(manager.m_previewClip);
        }

        private static AudioClip CreateBlip()
        {
            const int sampleRate = 44100;
            const float duration = 0.12f;
            int count = (int)(sampleRate * duration);
            float[] data = new float[count];
            for (int i = 0; i < count; i++) {
                float t = i / (float)sampleRate;
                float envelope = 1f - i / (float)count;
                data[i] = Mathf.Sin(2f * Mathf.PI * 660f * t) * envelope * 0.6f;
            }
            AudioClip clip = AudioClip.Create("SfxPreviewBlip", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
