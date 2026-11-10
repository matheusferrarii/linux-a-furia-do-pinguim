using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Fenda de água gelada (GDD: "buraco ou precipício de gelo"): cair aqui perde uma vida.
    /// Precisa pular por cima.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaterHazard : MonoBehaviour
    {
        [SerializeField] private GameObject splashEffect;

        public void Setup(GameObject splash) { splashEffect = splash; }

        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p == null || p.IsDead) { return; }
            if (splashEffect != null) { PoolManager.Instantiate(splashEffect, p.transform.position + Vector3.up * 0.5f, Quaternion.identity); }
            GameSession.Instance?.HintOnce("agua", "Água gelada! Pule as fendas");
            p.CharacterDamageable.Die();
        }
    }
}
