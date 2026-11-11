using System;
using System.Collections.Generic;
using System.Linq;
using Dypsloom.DypThePenguin.Scripts.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Estado da fase: peixes (moedas), peixes dourados, vidas, checkpoints, missões, Game Over e fim de fase.
    /// Um por cena. Cria o HUD sozinho.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        [Header("Jogador")]
        [SerializeField] private PenguinCharacter player;
        [SerializeField] private int startLives = 3;
        [Tooltip("Abaixo desta altura o pinguim morre (caiu do mapa).")]
        [SerializeField] private float fallKillY = -1000f;

        [Header("Missões")]
        [SerializeField] private List<Mission> missions = new List<Mission>();

        [Header("Som e efeitos")]
        [SerializeField] private FeedbackBank feedbackBank;

        [Header("Próxima fase (vazio = só menu)")]
        [SerializeField] private string nextScene = "";

        [Header("Bioma inicial")]
        [SerializeField] private string startArea = "Floresta Congelada";
        [SerializeField] private bool startBlizzard;

        [Header("Clima")]
        [SerializeField] private Material snowMaterial;

        [Header("Visual do HUD")]
        [SerializeField] private Font titleFont;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;

        [Header("Cenas")]
        [SerializeField] private string mainMenuScene = "MainMenu";

        public int Coins { get; private set; }
        public int RareFish { get; private set; }
        public int Lives { get; private set; }
        public int Kills { get; private set; }
        public int TotalCoins { get; private set; }
        public int TotalRare { get; private set; }
        public int TotalEnemies { get; private set; }
        public int CheckpointsReached { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsLevelComplete { get; private set; }
        public PenguinCharacter Player => player;
        public IReadOnlyList<Mission> Missions => missions;
        public GameHUD Hud { get; private set; }
        public FeedbackBank FeedbackBank => feedbackBank;
        public string NextScene => nextScene;

        /// <summary>Qualquer número do HUD mudou (peixes, vida, vidas...).</summary>
        public event Action Changed;
        public event Action MissionsChanged;

#if UNITY_EDITOR
        /// <summary>Usado pelo gerador da fase (Tools > Furia Pinguim > Fase).</summary>
        public void EditorSetup(PenguinCharacter p, int lives, float killY, List<Mission> list, Font font, Sprite panel, Sprite button, Material snow, FeedbackBank bank, string next, string areaName = null, bool areaBlizzard = false)
        {
            player = p; startLives = lives; fallKillY = killY; missions = list;
            if (areaName != null) { startArea = areaName; startBlizzard = areaBlizzard; }
            titleFont = font; panelSprite = panel; buttonSprite = button; snowMaterial = snow; feedbackBank = bank; nextScene = next;
        }
#endif

        private void Awake()
        {
            Instance = this;
            Lives = startLives;
            Time.timeScale = 1f;
            GameState.IsPaused = false;
            if (player == null) { player = FindAnyObjectByType<PenguinCharacter>(); }

            Hud = gameObject.AddComponent<GameHUD>();
            Hud.Init(this, titleFont, panelSprite, buttonSprite);
            Weather = gameObject.AddComponent<WeatherController>();
            Weather.Init(snowMaterial);
        }

        public WeatherController Weather { get; private set; }
        public string CurrentArea { get; private set; }
        private readonly HashSet<string> m_Hints = new HashSet<string>();

        /// <summary>Mostra um aviso só na primeira vez (ex.: "Gelo escorregadio!").</summary>
        public void HintOnce(string key, string message)
        {
            if (m_Hints.Add(key)) { Hud.Toast(message); Feedback.Area(); }
        }

        /// <summary>Entrou num trecho/bioma da fase (mostra o nome e liga/desliga a nevasca).</summary>
        public void EnterArea(string area, bool blizzard)
        {
            Weather.SetBlizzard(blizzard);
            if (string.IsNullOrEmpty(area) || area == CurrentArea) { return; }
            CurrentArea = area;
            Hud.ShowAreaTitle(area);
            Feedback.Area();
        }

        /// <summary>O BSOD foi derrotado.</summary>
        public void BossDefeated()
        {
            Progress(MissionType.DefeatBoss, 1, absolute: true);
            Hud.Toast("BSOD derrotado! O reino está livre!");
            RaiseChanged();
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
        }

        private void Start()
        {
            foreach (var fish in FindObjectsByType<FishPickup>(FindObjectsInactive.Exclude)) {
                if (fish.IsRare) { TotalRare++; } else { TotalCoins += fish.Value; }
            }
            // Capangas = inimigos pisáveis, menos o boss.
            TotalEnemies = FindObjectsByType<Stompable>(FindObjectsInactive.Exclude).Count(s => s.GetComponent<BsodBoss>() == null);
            CurrentArea = startArea;
            Weather.SetBlizzard(startBlizzard);
            Hud.ShowAreaTitle(CurrentArea);

            if (player != null) {
                player.OnDie += OnPlayerDied;
                if (player.CharacterDamageable != null) { player.CharacterDamageable.OnHpChanged += RaiseChanged; }
            }
            GameManager.Instance.EnemyDiedE += OnEnemyDied;

            foreach (var m in missions) {
                if (m.activeAtStart) { ActivateMission(m, silent: true); }
            }
            RaiseChanged();
            MissionsChanged?.Invoke();
            Feedback.MusicLevel();
        }

        private void Update()
        {
            if (player == null || IsGameOver || IsLevelComplete) { return; }
            if (!player.IsDead && player.gameObject.activeInHierarchy && player.transform.position.y < fallKillY) {
                player.CharacterDamageable.Die();
            }
        }

        // ------------------------------------------------------------------ peixes

        public void CollectFish(FishPickup fish)
        {
            if (fish.IsRare) {
                RareFish++;
                Coins += fish.Value;
                if (fish.Heal > 0 && player != null) { player.CharacterDamageable.Heal(fish.Heal); }
                Feedback.RareFish();
                Hud.Toast(fish.Heal > 0 ? "Peixe dourado! +1 coração" : "Peixe dourado!");
                Progress(MissionType.CollectRare, RareFish, absolute: true);
            } else {
                Coins += fish.Value;
                Feedback.Fish();
            }
            Progress(MissionType.CollectCoins, Coins, absolute: true);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ vidas / checkpoints

        private void OnPlayerDied()
        {
            if (IsGameOver || IsLevelComplete) { return; }
            Lives = Mathf.Max(0, Lives - 1);
            RaiseChanged();
            if (Lives <= 0) {
                IsGameOver = true;
                Hud.ShowGameOver();
                Feedback.GameOver();
            } else {
                Hud.Toast(Lives == 1 ? "Última vida!" : $"Restam {Lives} vidas");
            }
        }

        public void ReachCheckpoint(Checkpoint checkpoint)
        {
            if (player == null) { return; }
            player.SetSpawnPoint(checkpoint.SpawnPoint);
            CheckpointsReached++;
            Hud.Toast("Checkpoint!");
            Progress(MissionType.ReachCheckpoint, CheckpointsReached, absolute: true);
        }

        /// <summary>Chegou ao portal do cume (missão "chegar ao topo").</summary>
        public void ReachSummit()
        {
            Progress(MissionType.ReachGoal, 1, absolute: true);
        }

        public void CompleteLevel()
        {
            if (IsLevelComplete || IsGameOver) { return; }
            Progress(MissionType.ReachGoal, 1, absolute: true);
            IsLevelComplete = true;
            if (player != null) { player.InputLocked = true; }
            Hud.ShowLevelComplete();
            Feedback.Victory();
        }

        private void OnEnemyDied(int total)
        {
            Kills++;
            Progress(MissionType.DefeatEnemies, Kills, absolute: true);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ missões

        public void ActivateMissions(IEnumerable<string> ids)
        {
            if (ids == null) { return; }
            foreach (var id in ids) {
                var m = missions.Find(x => x.id == id);
                if (m != null) { ActivateMission(m, silent: false); }
            }
        }

        private void ActivateMission(Mission m, bool silent)
        {
            if (m.active || m.done) { return; }
            m.active = true;
            m.progress = CurrentValueFor(m.type);
            if (!silent) { Hud.Toast("Nova missão: " + m.title); }
            CheckDone(m);
            MissionsChanged?.Invoke();
        }

        private int CurrentValueFor(MissionType type) => type switch {
            MissionType.CollectCoins => Coins,
            MissionType.CollectRare => RareFish,
            MissionType.DefeatEnemies => Kills,
            MissionType.ReachCheckpoint => CheckpointsReached,
            _ => 0,
        };

        /// <summary>Atualiza missões do tipo. Para TalkTo, passe o id do NPC em key.</summary>
        public void Progress(MissionType type, int value, bool absolute, string key = null)
        {
            bool changed = false;
            foreach (var m in missions) {
                if (!m.active || m.done || m.type != type) { continue; }
                if (type == MissionType.TalkTo && m.key != key) { continue; }
                m.progress = absolute ? value : m.progress + value;
                CheckDone(m);
                changed = true;
            }
            if (changed) { MissionsChanged?.Invoke(); }
        }

        private void CheckDone(Mission m)
        {
            if (m.done || m.progress < m.target) { return; }
            m.done = true;
            Feedback.Mission();
            if (m.bonusCoins > 0) { Coins += m.bonusCoins; }
            Hud.Toast(m.bonusCoins > 0 ? $"Missão completa: {m.title}  (+{m.bonusCoins} peixes)" : $"Missão completa: {m.title}");
            RaiseChanged();
        }

        // ------------------------------------------------------------------ cenas

        public void Restart()
        {
            Time.timeScale = 1f;
            GameState.IsPaused = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToNextScene()
        {
            Time.timeScale = 1f;
            GameState.IsPaused = false;
            SceneManager.LoadScene(string.IsNullOrEmpty(nextScene) ? mainMenuScene : nextScene);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            GameState.IsPaused = false;
            SceneManager.LoadScene(mainMenuScene);
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}
