# Lutschlons_EverythingCanDieAlternative
This is an alternative version of the EverythingCanDie mod from TheFluff alias nyakowint for LethalCompany

# EverythingCanDieAlternative

A simplified version of the EverythingCanDie mod from TheFluff that makes immortal enemies damageable/killable.
- This mod aims to be more robust as i ran into issues with the original where some mobs would not be damageable even if the config said their should be.
- I recommend using this mod with other mods that give you rewards for killing enemies. I only tested SellBodiesFixed at the moment.

## Differences from Original Mod
- No separate shotgun/melee weapon settings
- No Explosion Effects: Enemies simply die without explosion effects
- Focused solely on making enemies damageable

## Configuration
For each enemy, you can configure:
- `.Unimmortal` - Toggle if the enemy can be damaged (true/false) - Default is every enemy is killable
- `.Health` - Set the enemy's health value

## Known Issues
- The Fiend and Locker enemies do not work with this mod
- Ghost Girl (and potentially Herobrine/Football) may not despawn properly when killed
- In some multiplayer cases, clients may not load the config for an enemy they try to hit. 
<br> But the mod will just fall back to allowing damage thus making every enemy still killable
Iam working on preventing this, still needs further testing.

## Credits
Based on the original EverythingCanDie mod from here: https://thunderstore.io/c/lethal-company/p/TheFluff/EverythingCanDie/ and https://github.com/nyakowint/EverythingCanDie-LC/tree/main
<br> I made the code changes with ai, iam not a programmer.
