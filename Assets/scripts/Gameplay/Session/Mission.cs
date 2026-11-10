using System;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    public enum MissionType
    {
        TalkTo,          // conversar com o NPC cujo id = key
        CollectCoins,    // juntar N peixes
        CollectRare,     // achar N peixes dourados
        DefeatEnemies,   // derrotar N inimigos
        ReachCheckpoint, // ativar N checkpoints
        ReachGoal,       // chegar ao fim da fase (portal do cume)
        DefeatBoss,      // derrotar o BSOD
    }

    [Serializable]
    public class Mission
    {
        public string id;
        public string title;
        public MissionType type;
        [Tooltip("Quantidade necessária.")]
        public int target = 1;
        [Tooltip("Para TalkTo: id do NPC.")]
        public string key;
        [Tooltip("Já aparece no começo da fase.")]
        public bool activeAtStart;
        [Tooltip("Missão principal (destacada no HUD).")]
        public bool main;
        [Tooltip("Peixes de bônus ao completar.")]
        public int bonusCoins = 10;

        [NonSerialized] public int progress;
        [NonSerialized] public bool active;
        [NonSerialized] public bool done;

        public string ProgressText => target > 1 ? $"{Mathf.Min(progress, target)}/{target}" : "";
    }
}
