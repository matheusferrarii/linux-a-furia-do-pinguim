using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    /// <summary>
    /// O personagem original so entra no estado "Idle" do Animator quando o parametro
    /// "Grounded" fica true (normalmente setado pelo script de movimento, que removemos do
    /// preview do menu). Sem isso ele fica preso numa pose/estado diferente, parado. Forca
    /// a pose correta assim que o jogo realmente roda (Animator.Play so funciona em Play Mode).
    /// </summary>
    public class PenguinPreviewIdlePose : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string idleStateName = "Dyp_interact";

        private void Start()
        {
            if (animator == null) {
                return;
            }
            animator.SetBool("Grounded", true);
            animator.SetFloat("Horizontal Speed", 0f);
            animator.SetFloat("Vertical Speed", 0f);
            animator.Play(idleStateName, 0, 0f);
        }
    }
}
