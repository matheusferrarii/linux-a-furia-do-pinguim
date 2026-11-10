using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>Mostra uma dica uma única vez quando o pinguim passa (ex.: "Galho baixo! Deslize com Ctrl").</summary>
    [RequireComponent(typeof(Collider))]
    public class HintTrigger : MonoBehaviour
    {
        [SerializeField] private string key = "dica";
        [SerializeField] private string message = "";

        public void Setup(string hintKey, string text) { key = hintKey; message = text; }

        private void Awake() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PenguinCharacter>() == null) { return; }
            GameSession.Instance?.HintOnce(key, message);
        }
    }
}
