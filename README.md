# EverythingCanDieAlternative 1.1.3

This mod aims to make every enemy in the game killable with configurable hp. It aims to work with any modded weapon and any modded enemy.
- You can of course also leave enemies immortal in the config to hit the right balance.
- This mod despawns dead enemies by itself (configurable)
- This mod is compatible with SellBodiesFixed and EnhancedMonsters

> <details><summary> Differences from Original Mod</summary>- No separate shotgun/melee weapon settings<br>- No Explosion Effects: Enemies simply despawn or play their death animation if allowed to by the despawn config<br>- An alternative version of the EverythingCanDie mod from TheFluff as it did not work for me with a few modded enemies.</details>

## Configuration
Start the game and host a lobby, then close the game and check out the configuration files in Thunderstore.
<br>For each enemy, you can configure:
> nwnt.EverythingCanDieAlternative.cfg
- `.Unimmortal` - Toggle if the enemy can be damaged (true/false) - Default is every enemy is killable
- `.Health` - Set the enemy's health value, default values are like the original game, or how the modder configured their enemy
  - For reference: the shovel deals 1 damage, the vanilla shotgun either 2/6/10 based on distance, modded weapons work as well with their own stats

> nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg<br>
- `.Despawn` - Toggle if the model of the enemy should get forced to despawn after its death
- `EnableDespawnFeature` - Master Switch to enable or disable the despawn functionality as a whole

 
<br>Everyone needs to have this mod installed for it to work

## Known Issues
- Hitting a Forest Giant or Old Bird with the cruiser does not kill them. I will test this further and work on a fix.

<h3>Found a bug? You can find me on my mod page:</h3>

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