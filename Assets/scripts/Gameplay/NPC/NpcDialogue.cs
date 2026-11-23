using System;
using System.Collections;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 5)] public string text;
    }

    /// <summary>
    /// NPC com diálogo. Chegue perto e aperte E. Na primeira conversa conta a história e pode
    /// liberar missões; depois repete uma fala curta.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class NpcDialogue : MonoBehaviour
    {
        [SerializeField] private string npcId = "npc";
        [SerializeField] private string displayName = "Pinguim";
        [SerializeField] private DialogueLine[] firstTalk = Array.Empty<DialogueLine>();
        [SerializeField] private DialogueLine[] repeatTalk = Array.Empty<DialogueLine>();
        [Tooltip("Missões liberadas ao terminar a primeira conversa.")]
        [SerializeField] private string[] activateMissions = Array.Empty<string>();
        [SerializeField] private Transform model;
        [SerializeField] private GameObject attentionMarker;
        [SerializeField] private float talkRadius = 4.5f;

        private bool m_PlayerInRange;
        private bool m_Talked;
        private bool m_Talking;
        private PenguinCharacter m_Player;
        private Quaternion m_BaseRotation;
        private NpcAnimator m_Anim;

        public string DisplayName => displayName;

        public void Setup(string id, string name, DialogueLine[] first, DialogueLine[] repeat, string[] missions, Transform modelRoot, GameObject marker)
        {
            npcId = id; displayName = name; firstTalk = first; repeatTalk = repeat; activateMissions = missions;
            model = modelRoot; attentionMarker = marker;
        }

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = talkRadius / Mathf.Max(0.01f, transform.lossyScale.x);
            if (model != null) {
                m_BaseRotation = model.rotation;
                m_Anim = model.GetComponent<NpcAnimator>();
                if (m_Anim == null) { m_Anim = model.gameObject.AddComponent<NpcAnimator>(); }
            }
        }

        private void Update()
        {
            if (model != null && m_Player != null && (m_PlayerInRange || m_Talking)) {
                Vector3 to = m_Player.transform.position - model.position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) {
                    model.rotation = Quaternion.Slerp(model.rotation, Quaternion.LookRotation(to.normalized), 6f * Time.deltaTime);
                }
            }

            var hud = GameSession.Instance != null ? GameSession.Instance.Hud : null;
            if (hud == null || m_Talking || !m_PlayerInRange || m_Player == null || m_Player.IsDead) { return; }
            if (GameState.IsPaused || hud.IsDialogOpen) { return; }
            if (Input.GetKeyDown(KeyCode.E)) { StartCoroutine(Talk(hud)); }
        }

        private IEnumerator Talk(GameHUD hud)
        {
            m_Talking = true;
            hud.HidePrompt(this);
            m_Player.InputLocked = true;
            if (m_Anim != null) { m_Anim.Wave(); }
            var lines = (!m_Talked || repeatTalk.Length == 0) ? firstTalk : repeatTalk;
            bool finished = false;
            hud.ShowDialog(lines, displayName, () => finished = true);
            while (!finished) { yield return null; }
            yield return null; // não deixa a tecla que fechou o diálogo pular/atirar
            m_Player.InputLocked = false;

            if (!m_Talked) {
                m_Talked = true;
                if (attentionMarker != null) { attentionMarker.SetActive(false); }
                GameSession.Instance?.Progress(MissionType.TalkTo, 1, absolute: false, key: npcId);
                GameSession.Instance?.ActivateMissions(activateMissions);
            }
            m_Talking = false;
            if (m_PlayerInRange) { hud.ShowPrompt(this, "[E] Conversar com " + displayName); }
        }

        private void OnTriggerEnter(Collider other)
        {
            var penguin = other.GetComponentInParent<PenguinCharacter>();
            if (penguin == null) { return; }
            m_Player = penguin;
            m_PlayerInRange = true;
            if (m_Anim != null) { m_Anim.Wave(8f); } // acena quando o jogador chega perto
            GameSession.Instance?.Hud?.ShowPrompt(this, "[E] Conversar com " + displayName);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PenguinCharacter>() == null) { return; }
            m_PlayerInRange = false;
            GameSession.Instance?.Hud?.HidePrompt(this);
        }
    }
}
