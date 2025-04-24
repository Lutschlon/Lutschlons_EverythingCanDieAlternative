# EverythingCanDieAlternative 1.1.55

This mod makes every enemy in the game killable with configurable hp. It works with any modded weapon and any modded enemy.
- Easly configurable via UI
- You can of course leave enemies immortal in the config to hit the right balance, you can also disable my mod for enemies if you want to 100% preserve their original behaviour
- This mod despawns dead enemies by itself (configurable)
- This mod is compatible with see [Mod Compatibilities Table](#Mod-Compatibilities) below

> <details><summary> Differences from Original Mod</summary>- No separate shotgun/melee weapon settings<br>- No Explosion Effects: Enemies simply despawn or play their death animation if allowed to by the despawn config<br>- An alternative version of the EverythingCanDie mod from TheFluff as it did not work for me with a few modded enemies</details>

## Configuration
In order to generate the enemy configs: 
Start the game > host a lobby > quit the lobby > check out the EverythingCanDieAlt menu or the .cfg files

![](https://i.imgur.com/jMikt8q.png)
____
- The menu can be hidden / shown with `LethalConfig` as well
____
_Help for the .cfg files_
<br>For each enemy, you can configure:
> nwnt.EverythingCanDieAlternative.cfg
- `.Unimmortal` - Toggle if the enemy can be damaged (true/false) - Default is every enemy is killable
- `.Health` - You configure the enemy's health value completely to your liking
  - For reference: the shovel deals 1 damage, the vanilla shotgun either 1/3/5 based on distance, modded weapons work as well with their own stats

> nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg<br>
- `.Despawn` - Toggle if the model of the enemy should get forced to despawn after its death
- `EnableDespawnFeature` - Master Switch to enable or disable the despawn functionality as a whole

> nwnt.EverythingCanDieAlternative_Enemy_Control.cfg<br>
- `.Enable` - Set to false to deactivate this mod for specific enemies to preserve their original health/hit behavior

<br>Everyone needs to have this mod installed for it to work. Everyone should have the same config.

## Mod Compatibilities
Iam working on compatibilities for a few requested mods. Let me know if you have any issues with said mods, as testing all of them is not possible for me alone. Mods with known issues might be fixed later.
| List of implemented compatibility patches|Mods that seem to work out of the box| Mods with known issues|
| ------- | ------- | ------- |
| [SellBodiesFixed](https://thunderstore.io/c/lethal-company/p/Entity378/SellBodiesFixed/)|[Enhanced Monsters](https://thunderstore.io/c/lethal-company/p/VELD/Enhanced_Monsters/)|[EnemyHealthBars](https://thunderstore.io/c/lethal-company/p/NotezyTeam/EnemyHealthBars/) - HealthBar doesnt work|
| [LethalHands](https://thunderstore.io/c/lethal-company/p/SlapItNow/LethalHands/)|[MoreCounterplay](https://thunderstore.io/c/lethal-company/p/BaronDrakula/MoreCounterplay/)||
| [BrutalCompanyMinusExtraReborn](https://thunderstore.io/c/lethal-company/p/SoftDiamond/BrutalCompanyMinusExtraReborn/)|||
|[Hitmarker](https://thunderstore.io/c/lethal-company/p/Zehs/Hitmarker/)|||

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
