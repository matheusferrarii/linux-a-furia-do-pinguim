using System.Collections;
using Dypsloom.DypThePenguin.Scripts.Damage;
using Dypsloom.Shared.Utility;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>Placa de gelo escorregadio (GDD: "trechos de gelo escorregadio").</summary>
    [RequireComponent(typeof(Collider))]
    public class IceZone : MonoBehaviour
    {
        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p == null) { return; }
            p.SetOnIce(true);
            GameSession.Instance?.HintOnce("gelo", "Gelo escorregadio! Cuidado com as curvas");
        }

        private void OnTriggerExit(Collider other)
        {
            var p = other.GetComponentInParent<PenguinCharacter>();
            if (p != null) { p.SetOnIce(false); }
        }
    }
}
