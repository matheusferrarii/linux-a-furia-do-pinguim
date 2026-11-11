using System.Collections;
using Dypsloom.Shared.Utility;
using LinuxFuriaPinguim.UI;
using UnityEngine;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// "Game feel" num lugar só: cada acontecimento do jogo toca o som certo e, quando faz sentido,
    /// treme a câmera, congela o tempo por um instante (hit-stop) e solta partículas.
    /// </summary>
    public static class Feedback
    {
        private static FeedbackBank Bank => GameSession.Instance != null ? GameSession.Instance.FeedbackBank : null;

        private static void Play(AudioClip clip)
        {
            if (clip != null) { AudioManager.Instance.PlaySfx(clip); }
        }

        private static void Fx(GameObject prefab, Vector3 pos, float scale = 1f)
        {
            if (prefab == null) { return; }
            var go = PoolManager.Instantiate(prefab, pos, Quaternion.identity);
            if (go != null) { go.transform.localScale = Vector3.one * scale; }
        }

        private static void Shake(float amplitude, float duration) => CameraFollow.Shake(amplitude, duration);

        // ------------------------------------------------------------------ pinguim
        public static void Jump(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.jump); Fx(b.dustEffect, pos, 0.6f); }
        public static void Land(Vector3 pos, float strength)
        {
            var b = Bank; if (b == null) { return; }
            Play(b.land);
            Fx(b.dustEffect, pos, Mathf.Lerp(0.6f, 1.3f, strength));
            if (strength > 0.5f) { Shake(0.08f * strength, 0.15f); }
        }
        public static void Slide(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.slide); Fx(b.dustEffect, pos, 0.8f); }
        public static void SlideSpray(Vector3 pos) { var b = Bank; if (b == null) { return; } Fx(b.dustEffect, pos, 0.45f); }
        public static void Blocked(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.blocked != null ? b.blocked : b.enemyHit); Fx(b.hitEffect, pos, 0.5f); }
        public static void QteKey(bool ok) { var b = Bank; if (b == null) { return; } Play(ok ? b.dialogBlip : b.playerHurt); }
        public static void Throw() => Play(Bank != null ? Bank.throwBall : null);
        public static void Stomp(Vector3 pos)
        {
            var b = Bank; if (b == null) { return; }
            Play(b.stomp); Fx(b.hitEffect, pos, 1.2f); Shake(0.15f, 0.15f); HitStop(0.05f);
        }
        public static void PlayerHurt() { var b = Bank; if (b == null) { return; } Play(b.playerHurt); Shake(0.25f, 0.25f); HitStop(0.06f); }
        public static void PlayerDie() { var b = Bank; if (b == null) { return; } Play(b.playerDie); Shake(0.35f, 0.4f); }

        // ------------------------------------------------------------------ inimigos
        public static void EnemyHit(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.enemyHit); Fx(b.hitEffect, pos, 0.8f); Shake(0.06f, 0.1f); }
        public static void EnemyDie(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.enemyDie); Fx(b.hitEffect, pos, 1.4f); HitStop(0.04f); }
        public static void MonsterWindup() => Play(Bank != null ? Bank.monsterWindup : null);

        // ------------------------------------------------------------------ fase
        public static void Fish() => Play(Bank != null ? Bank.fish : null);
        public static void RareFish() => Play(Bank != null ? Bank.rareFish : null);
        public static void Checkpoint() => Play(Bank != null ? Bank.checkpoint : null);
        public static void Mission() => Play(Bank != null ? Bank.mission : null);
        public static void Splash(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.splash); Fx(b.hitEffect, pos, 1.2f); }
        public static void IceCrack(Vector3 pos, bool near) { var b = Bank; if (b == null) { return; } Play(b.iceCrack); if (near) { Shake(0.12f, 0.15f); } }
        public static void Area() => Play(Bank != null ? Bank.area : null);
        public static void DialogBlip() => Play(Bank != null ? Bank.dialogBlip : null);

        // ------------------------------------------------------------------ boss
        public static void BossRoar() { var b = Bank; if (b == null) { return; } Play(b.bossRoar); Shake(0.3f, 1.2f); }
        public static void BossSlam(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.bossSlam); Fx(b.dustEffect, pos, 2.5f); Shake(0.5f, 0.45f); }
        public static void BossFreeze() => Play(Bank != null ? Bank.bossFreeze : null);
        public static void BossCoreHit(Vector3 pos) { var b = Bank; if (b == null) { return; } Play(b.bossCoreHit); Fx(b.hitEffect, pos, 2.5f); Shake(0.45f, 0.35f); HitStop(0.12f); }

        // ------------------------------------------------------------------ jingles / música
        public static void Victory() { var b = Bank; if (b == null) { return; } Play(b.victory); }
        public static void GameOver() { var b = Bank; if (b == null) { return; } Play(b.gameOver); }
        public static void MusicLevel() { var b = Bank; if (b != null && b.musicLevel != null) { AudioManager.Instance.PlayBgm(b.musicLevel); } }
        public static void MusicBoss() { var b = Bank; if (b != null && b.musicBoss != null) { AudioManager.Instance.PlayBgm(b.musicBoss); } }

        // ------------------------------------------------------------------ hit-stop

        /// <summary>Congela o jogo por alguns centésimos: deixa os golpes "pesados".</summary>
        public static void HitStop(float seconds)
        {
            var s = GameSession.Instance;
            if (s == null || GameState.IsPaused || Time.timeScale < 0.5f) { return; }
            s.StartCoroutine(HitStopRoutine(seconds));
        }

        private static IEnumerator HitStopRoutine(float seconds)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!GameState.IsPaused) { Time.timeScale = 1f; }
        }
    }
}
