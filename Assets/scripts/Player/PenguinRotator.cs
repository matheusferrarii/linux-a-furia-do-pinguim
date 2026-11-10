using Dypsloom.DypThePenguin.Scripts.Character;

namespace LinuxFuriaPinguim
{
    /// <summary>
    /// Rotação normal do Dypsloom (vira para onde o jogador anda), mas trava a direção durante o deslize.
    /// </summary>
    public class PenguinRotator : CharacterRotator
    {
        private readonly PenguinCharacter m_Penguin;

        public PenguinRotator(PenguinCharacter character) : base(character)
        {
            m_Penguin = character;
        }

        public override void Tick()
        {
            if (m_Penguin.IsSliding || m_Penguin.InputLocked) { return; }
            base.Tick();
        }
    }
}
