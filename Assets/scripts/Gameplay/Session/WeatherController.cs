using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Neve caindo em volta da câmera e nevasca (GDD: "nevascas que reduzem a visibilidade").
    /// Neve leve sempre; na nevasca a neve fica forte, com vento, e a neblina fecha.
    /// </summary>
    public class WeatherController : MonoBehaviour
    {
        private Material m_SnowMaterial;
        private ParticleSystem m_Snow;
        private bool m_Blizzard;
        private float m_Blend;

        private bool m_BaseFog;
        private FogMode m_BaseFogMode;
        private float m_BaseDensity;
        private Color m_BaseFogColor;

        private const float LightRate = 60f, BlizzardRate = 900f;
        private const float BlizzardDensity = 0.028f;
        private static readonly Color BlizzardFog = new Color(0.82f, 0.88f, 0.94f);

        public void Init(Material snowMaterial)
        {
            m_SnowMaterial = snowMaterial;
            m_BaseFog = RenderSettings.fog;
            m_BaseFogMode = RenderSettings.fogMode;
            m_BaseDensity = RenderSettings.fogDensity;
            m_BaseFogColor = RenderSettings.fogColor;
        }

        public void SetBlizzard(bool on) { m_Blizzard = on; }

        private void Start()
        {
            var cam = Camera.main;
            if (cam == null) { return; }
            var go = new GameObject("Neve");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 10f, 12f);
            m_Snow = go.AddComponent<ParticleSystem>();
            m_Snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = m_Snow.main;
            main.duration = 5f; main.loop = true;
            main.startLifetime = 3.5f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = m_Snow.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(45f, 1f, 45f);

            var vel = m_Snow.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            vel.y = new ParticleSystem.MinMaxCurve(-4.5f, -3f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);

            var emission = m_Snow.emission;
            emission.rateOverTime = LightRate;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (m_SnowMaterial != null) { renderer.sharedMaterial = m_SnowMaterial; }
            m_Snow.Play();
        }

        private void Update()
        {
            m_Blend = Mathf.MoveTowards(m_Blend, m_Blizzard ? 1f : 0f, Time.deltaTime * 0.6f);

            if (m_Snow != null) {
                var emission = m_Snow.emission;
                emission.rateOverTime = Mathf.Lerp(LightRate, BlizzardRate, m_Blend);
                var vel = m_Snow.velocityOverLifetime;
                float wind = Mathf.Lerp(0f, 9f, m_Blend);
                vel.x = new ParticleSystem.MinMaxCurve(wind - 0.5f, wind + 0.5f);
                vel.y = new ParticleSystem.MinMaxCurve(Mathf.Lerp(-4.5f, -8f, m_Blend), Mathf.Lerp(-3f, -5f, m_Blend));
                var renderer = m_Snow.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = m_Blend > 0.5f ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
                renderer.velocityScale = 0.04f;
            }

            if (m_Blend > 0.001f) {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = Mathf.Lerp(m_BaseFog ? m_BaseDensity : 0f, BlizzardDensity, m_Blend);
                RenderSettings.fogColor = Color.Lerp(m_BaseFog ? m_BaseFogColor : BlizzardFog, BlizzardFog, m_Blend);
            } else if (RenderSettings.fog != m_BaseFog || RenderSettings.fogMode != m_BaseFogMode) {
                RenderSettings.fog = m_BaseFog;
                RenderSettings.fogMode = m_BaseFogMode;
                RenderSettings.fogDensity = m_BaseDensity;
                RenderSettings.fogColor = m_BaseFogColor;
            }
        }
    }
}
