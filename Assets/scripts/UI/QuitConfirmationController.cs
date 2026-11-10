using UnityEngine;

namespace LinuxFuriaPinguim.UI
{
    public class QuitConfirmationController : MonoBehaviour
    {
        public void ConfirmQuit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void CancelQuit()
        {
            UIPanelFader fader = GetComponent<UIPanelFader>();
            if (fader != null) {
                fader.Hide();
            } else {
                gameObject.SetActive(false);
            }
        }
    }
}
