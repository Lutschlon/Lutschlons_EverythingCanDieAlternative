# EverythingCanDieAlternative 1.1.66

This mod makes every enemy in the game killable with configurable hp. It aims to works with any modded weapon and any modded enemy.
- Easly configurable via UI
- You can leave enemies immortal in the config to hit the right balance, you can also disable my mod for enemies if you want to 100% preserve their original behaviour
- This mod removes dead enemie's corpses by itself (configurable)
- This mod is compatible with see [Mod Compatibilities Table](#Mod-Compatibilities) below

> <details><summary> Differences from Original Mod</summary>- No separate shotgun/melee weapon settings<br>- No Explosion Effects on death<br>- An alternative version of the EverythingCanDie mod from TheFluff as it did not work for me with a few modded enemies</details>

## Configuration
In order to generate the enemy configs: <br>
Start the game > host a lobby > quit the lobby > check out the EverythingCanDieAlt menu or the .cfg files<br>
If you add more enemies to your modpack you will need to host a game once more to generate their configs
<br>
- In the configuration UI you can make enemies killable, configure their health, configure if their corpse should despawn, or disable my mod for the specific enemy
- You can enable preview images for some enemies (may spoiler you!)

![](https://i.imgur.com/9ZzjvOu.png)

- The menu can be hidden / shown with `LethalConfig` or inside the `nwnt.EverythingCanDieAlternative.cfg` under `EnableConfigMenu`
____
<details>
  <summary>Click for help for the .cfg files</summary>
  <p>For each enemy, you can configure:</p>
  
  <blockquote>
    <p>nwnt.EverythingCanDieAlternative.cfg</p>
  </blockquote>
  <ul>
    <li><code>.Unimmortal</code> - Toggle if the enemy can be damaged (true/false) - Default is every enemy is killable</li>
    <li><code>.Health</code> - You configure the enemy's health value completely to your liking
      <ul>
        <li>For reference: the shovel deals 1 damage, the vanilla shotgun either 1/3/5 based on distance, cruiser deals 12 damage at high speed, modded weapons work as well with their own stats</li>
      </ul>
      <li><code>EnableConfigMenu</code> - Toggle if the configuration ui should be shown in the main menu</li>
      <li><code>EnableInfoLogs</code> - Toggle if info logs should be logged in the console</li>
      <li><code>ShowEnemyImages</code> - Toggle if preview images should be shown in the configuration ui</li>
      <li><code>ProtectImmortalEnemiesFromInstaKill</code> - Toggle if enemies should be protected from instakill effects when they are configure to be immortal, might get bypassed by other mods</li>
      <li><code>AllowSpikeTrapsToKillEnemies</code> - Toggle to decide if the vanilla spike traps should be able to kill enemies</li>
    </li>
  </ul>

  <blockquote>
    <p>nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg</p>
  </blockquote>
  <ul>
    <li><code>.Despawn</code> - Toggle if the model of the enemy should get forced to despawn after its death</li>
    <li><code>EnableDespawnFeature</code> - Master Switch to disable the despawn functionality as a whole if you encounter any problems with it</li>
  </ul>

  <blockquote>
    <p>nwnt.EverythingCanDieAlternative_Enemy_Control.cfg</p>
  </blockquote>
  <ul>
    <li><code>.Enable</code> - Set to false to deactivate this mod for specific enemies to preserve their original health/hit behavior</li>
  </ul>
  <hr>
</details>

Everyone needs to have this mod installed for it to work. Everyone should have the same config.

## Mod Compatibilities
Iam working on compatibilities for a few requested mods. Let me know if you have any issues with said mods, as testing all of them is not possible for me alone. Mods with known issues might be fixed later.
| List of implemented compatibility patches|Mods that seem to work out of the box| Mods with known issues|
| ------- | ------- | ------- |
| [SellBodiesFixed](https://thunderstore.io/c/lethal-company/p/Entity378/SellBodiesFixed/)|[Enhanced Monsters](https://thunderstore.io/c/lethal-company/p/VELD/Enhanced_Monsters/)|[EnemyHealthBars](https://thunderstore.io/c/lethal-company/p/NotezyTeam/EnemyHealthBars/) - HealthBar doesnt work|
| [LethalHands](https://thunderstore.io/c/lethal-company/p/SlapItNow/LethalHands/)|[MoreCounterplay](https://thunderstore.io/c/lethal-company/p/BaronDrakula/MoreCounterplay/)||
| [BrutalCompanyMinusExtraReborn](https://thunderstore.io/c/lethal-company/p/SoftDiamond/BrutalCompanyMinusExtraReborn/)|[FairAi](https://thunderstore.io/c/lethal-company/p/TheFluff/FairAI)||
|[Hitmarker](https://thunderstore.io/c/lethal-company/p/Zehs/Hitmarker/)|||
|[LethalMin](https://thunderstore.io/c/lethal-company/p/NotezyTeam/LethalMin/)|||
|[HexiBetterShotgunFixed](https://thunderstore.io/c/lethal-company/p/Entity378/HexiBetterShotgunFixed/)|||
## Known Issues
- Currently none

<h3>Found a bug? Is my mod not compatible with your favourite mod? You can find me here:</h3>

<table>
  <tr>
    <td style="padding: 8px; background-color: #f2f2f2;"><a href="https://discord.gg/8DgrNrH8Z5">LC Modding Discord</a></td>
    <td style="padding: 8px; background-color: #f2f2f2;"><a href="https://discord.com/channels/1168655651455639582/1348071762549805208">My Mod Page</a></td>
  </tr>
</table>

## Credits
Based on the original EverythingCanDie mod from here: [Thunderstore page](https://thunderstore.io/c/lethal-company/p/TheFluff/EverythingCanDie/) and [GitHub](https://github.com/nyakowint/EverythingCanDie-LC/tree/main).
<br> Thank you nyakowint.
<br> ClaudeAI did the coding. 
<br> Thank you Henni for testing with me.
