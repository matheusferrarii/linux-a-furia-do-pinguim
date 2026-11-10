# Linux: A Fúria do Pinguim

Jogo 3D de plataforma estilo Crash Bandicoot (Unity 6000.5.6f1, Built-in RP).
Linux, o pinguim, sobe a Montanha Ancestral para derrotar o BSOD, um golem de gelo colossal.

## Como abrir

1. Clone com Git LFS (`git lfs pull` depois do clone — modelos e texturas estão no LFS).
2. Abra pelo Unity Hub com a versão **6000.5.6f1**.
3. Cena inicial: `Assets/Scenes/MainMenu.unity`. Fase: `Assets/Scenes/Mountain.unity`.

## Controles

| Ação | Tecla |
|---|---|
| Andar | WASD / setas |
| Pular | Espaço |
| Deslizar de barriga | Ctrl ou C |
| Bola de neve (mira no alvo marcado) | Clique esquerdo ou F |
| Travar câmera no inimigo | Segurar botão direito |
| Conversar | E |
| Pausar | Esc |

## Estrutura

```
Assets/
  scripts/
    Core/              GameState (pausa global)
    Player/            PenguinCharacter, PenguinMover, PenguinRotator (sobre o Dypsloom)
    Camera/            CameraFollow (estilo Crash + combate), CameraZone
    Gameplay/
      Session/         GameSession (vidas, peixes, missões), GameHUD, clima, biomas, dicas
      Combat/          mira assistida, bola de neve, pisão, piscar ao levar dano
      Enemies/         Monstro de Gelo, boss BSOD, portal do boss
      Level/           peixes, checkpoints, portal final, gelo, água, estalactites
      NPC/             diálogos e marcador "!"
    UI/                menu principal, pause, opções, áudio
  Editor/
    LevelBuilders/     geradores da fase (cenário e gameplay)
    UI/                gerador dos menus e ferramentas de sprites
  Gameplay/            materiais e prefabs criados pelos geradores
  Scenes/              MainMenu, Mountain
  Dypsloom/, GeeZyyGames/, nature/, ...   pacotes de terceiros (não editar)
```

## Geradores da fase (menu Tools → Furia Pinguim)

A fase Mountain é montada a partir das cercas originais do mapa. Se mexer no traçado, gere de novo:

1. **Cenário → Gerar cenário em volta do trajeto** — barreiras invisíveis, borda de pedras/gelo e floresta.
2. **Fase → Gerar gameplay** — peixes, inimigos, NPCs, missões, obstáculos, biomas e boss.

Tudo gerado fica em `_CenarioGerado` e `_Gameplay` na cena e pode ser removido pelo mesmo menu.
Textos de diálogo e missões: fim de `Assets/Editor/LevelBuilders/GameplayBuilder.cs`.

> Dica para o grupo: a cena `Mountain.unity` é grande e gerada. Evitem editá-la em paralelo;
> em caso de conflito, fiquem com uma versão e rodem os geradores de novo.
