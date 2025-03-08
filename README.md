# EverythingCanDieAlternative 1.1.0

This mod aims to make every enemy in the game killable with configurable hp. It aims to work with any modded weapon and any modded enemy. You can of course also leave enemies immortal in the config to hit the right balance.
An alternative version of the EverythingCanDie mod from TheFluff as it did not work for me with a few modded enemies.

- I recommend using this mod with something like SellBodiesFixed as it handles the despawning of enemy corpses. My mod does not deal with it.

## Differences from Original Mod
- No separate shotgun/melee weapon settings
- No Explosion Effects: Enemies simply die without explosion effects

## Configuration
Start the game and host a lobby, then close the game and check out the configuration file in Thunderstore.
For each enemy, you can configure:
- `.Unimmortal` - Toggle if the enemy can be damaged (true/false) - Default is every enemy is killable
- `.Health` - Set the enemy's health value, default is often 3 or 0(1) for modded enemies that are not meant to be killed
- For reference: the shovel deals 1 damage, the vanilla shotgun either 2/6/10 based on distance, modded weapons work as well with their own stats

## Known Issues
- Hitting a Forrest Giant or Old Bird with the cruiser does not kill them. I will test this further and work on a fix.
- See changelog for more technical details
- Found a game breaking bug? You can inform me on my Discord mod-release post in the Lethal Company Modding Server.

## Credits
Based on the original EverythingCanDie mod from here: https://thunderstore.io/c/lethal-company/p/TheFluff/EverythingCanDie/ and https://github.com/nyakowint/EverythingCanDie-LC/tree/main
<br> Thank you nyakowint.
ClaudeAI did the coding. Thanks to Henni for testing with me.