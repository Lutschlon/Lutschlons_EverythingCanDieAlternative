# EverythingCanDieAlternative 1.1.80

This mod makes every enemy in the game killable with configurable HP. It aims to work with any modded weapon and any modded enemy.
- Easily configurable via in game configuration menu
- You can configure the enemies health or leave enemies immortal to achieve the right balance, you can also disable my mod for enemies if you want to 100% preserve their original behaviour
- This mod removes dead enemies' corpses (configurable and compatable with mod-drop mods)
- You can activate enemy health bars as well

> <details><summary> Differences from Original Mod</summary>- No separate shotgun/melee weapon settings<br>- No Explosion Effects on death<br>- An alternative version of the EverythingCanDie mod from TheFluff as it did not work for me with a few modded enemies</details>

## Configuration Menu
- Start the game > host a lobby > quit the lobby > check out the EverythingCanDieAlt menu or the .cfg files<br>
- You can enable preview images for some enemies (may spoiler you!) - not all enemies have preview images
- Search for specific enemies by their name or just scroll through the list

#### Killable Spring with 3 health
![](https://i.imgur.com/WbsX6kk.png)
#### Make Enemies Immortal
![](https://i.imgur.com/K9GzuMu.png)
#### Disable ECDA For An Enemy
![](https://i.imgur.com/BFZP4WC.png)

---------
## Global Settings
- Tweak some ECDA specific rules (the default is what i consider most useful)
- Activate and configure the health bar (the default is Off, remember to turn it on to use it!)
- Do bulk configuration if needed

![](https://i.imgur.com/zGJuwhw.png)

<br>
<details>
<summary>Click to see example images from the health bar</summary>
<br>
  
- This is how the health bar set to `Both` looks like with different sizes:

![](https://i.imgur.com/H5SNKmT.gif)

- This is the maximum Health bar range from `Close` to `Medium` to `Far` shown from above and shown from POV looks like:

![](https://i.imgur.com/TRTo3Zm.png)

![](https://i.imgur.com/vOCmd7e.png)
</details>

---------
- The config menu can be hidden / shown from the main menu with `LethalConfig` or inside the `nwnt.EverythingCanDieAlternative.cfg` under `EnableConfigMenu`
- Many Global Settings can be configured in `LethalConfig` while being inside a live round, the health bar can be configured on the fly without any issues, changing any of the rules isn't tested and mileage may vary
- Everyone needs to have this mod installed for it to work

## Mod Compatibilities
I implemented some compatibilities for popular mods. Let me know if you have any issues with said mods, as testing all of them is not possible for me alone.
| Compatibility implemented | Seem to work out of the box | Incompatible|
| ------- | ------- | ------- |
| [SellBodiesFixed](https://thunderstore.io/c/lethal-company/p/Entity378/SellBodiesFixed/)|[Enhanced Monsters](https://thunderstore.io/c/lethal-company/p/VELD/Enhanced_Monsters/)|[EnemyHealthBars](https://thunderstore.io/c/lethal-company/p/NotezyTeam/EnemyHealthBars/)|
| [LethalHands](https://thunderstore.io/c/lethal-company/p/SlapItNow/LethalHands/)|[MoreCounterplay](https://thunderstore.io/c/lethal-company/p/BaronDrakula/MoreCounterplay/)||
| [BrutalCompanyMinusExtraReborn](https://thunderstore.io/c/lethal-company/p/SoftDiamond/BrutalCompanyMinusExtraReborn/)|[FairAi](https://thunderstore.io/c/lethal-company/p/TheFluff/FairAI)||
|[Hitmarker](https://thunderstore.io/c/lethal-company/p/Zehs/Hitmarker/)|[DeathAnimations](https://thunderstore.io/c/lethal-company/p/chillosopher/DeathAnimations/) - Remove corpse should be adjusted||
|[LethalMin](https://thunderstore.io/c/lethal-company/p/NotezyTeam/LethalMin/)|||
|[HexiBetterShotgunFixed](https://thunderstore.io/c/lethal-company/p/Entity378/HexiBetterShotgunFixed/)|||
|[Natural selection](https://thunderstore.io/c/lethal-company/p/Fandovec03/Natural_selection/)|||

## Report Bugs
Found a bug? 
1. Join the LC Modding Discord 2. Let me know in my mod page:</h3>

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
<br> Thanks to everyone providing feedback on Discord.

<details>
  <summary>Click for help for the .cfg files (old)</summary>
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
      <li><code>EnableConfigMenu</code> - Toggle if the configuration menu should be shown in the main menu</li>
      <li><code>EnableInfoLogs</code> - Toggle if info logs should be logged in the console</li>
      <li><code>ShowEnemyImages</code> - Toggle if preview images should be shown in the configuration menu</li>
      <li><code>ProtectImmortalEnemiesFromInstaKill</code> - Toggle if enemies should be protected from instakill effects when they are configured to be immortal, might get bypassed by other mods</li>
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
