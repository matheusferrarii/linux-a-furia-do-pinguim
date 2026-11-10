using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Divisa entre trechos da fase (biomas do GDD). Ao atravessar mostra o nome do lugar e
    /// liga/desliga a nevasca.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaGate : MonoBehaviour
    {
        [SerializeField] private string areaAhead = "Vale Glacial";
        [SerializeField] private bool blizzardAhead;
        [SerializeField] private string areaBehind = "Floresta Congelada";
        [SerializeField] private bool blizzardBehind;

        public void Setup(string ahead, bool blizAhead, string behind, bool blizBehind)
        {
            areaAhead = ahead; blizzardAhead = blizAhead; areaBehind = behind; blizzardBehind = blizBehind;
        }

        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerExit(Collider other)
        {
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p == null || GameSession.Instance == null) { return; }
            bool ahead = Vector3.Dot(p.transform.position - transform.position, transform.forward) > 0f;
            GameSession.Instance.EnterArea(ahead ? areaAhead : areaBehind, ahead ? blizzardAhead : blizzardBehind);
        }
    }
}
