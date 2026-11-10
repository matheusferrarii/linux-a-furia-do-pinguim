using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>Sons, músicas e efeitos usados pelo Feedback. Gerado em Tools > Furia Pinguim > Áudio.</summary>
    [CreateAssetMenu(menuName = "Furia Pinguim/Feedback Bank")]
    public class FeedbackBank : ScriptableObject
    {
        [Header("Pinguim")]
        public AudioClip jump;
        public AudioClip land;
        public AudioClip slide;
        public AudioClip throwBall;
        public AudioClip stomp;
        public AudioClip playerHurt;
        public AudioClip playerDie;

        [Header("Inimigos")]
        public AudioClip enemyHit;
        public AudioClip enemyDie;
        public AudioClip monsterWindup;

        [Header("Fase")]
        public AudioClip fish;
        public AudioClip rareFish;
        public AudioClip checkpoint;
        public AudioClip mission;
        public AudioClip splash;
        public AudioClip iceCrack;
        public AudioClip area;
        public AudioClip dialogBlip;

        [Header("Boss")]
        public AudioClip bossRoar;
        public AudioClip bossSlam;
        public AudioClip bossFreeze;
        public AudioClip bossCoreHit;

        [Header("Jingles")]
        public AudioClip victory;
        public AudioClip gameOver;

        [Header("Músicas")]
        public AudioClip musicLevel;
        public AudioClip musicBoss;

        [Header("Efeitos visuais")]
        public GameObject hitEffect;
        public GameObject dustEffect;
    }
}
