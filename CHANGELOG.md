## Version 1.1.55
- Updated general hit detection and creation of network variables (when an enemy spawns) for maybe better performance
- Updated nearly all info log entries to be hidden when `EnableInfoLogs` is deactivated

##
###### Version 1.1.54 - Fixed Hitmarker compatibility being detected when using SellMyScrap from Zehs

## Version 1.1.53
- Added beta compatibility for [Hitmarker](https://thunderstore.io/c/lethal-company/p/Zehs/Hitmarker/) by Zehs, not tested in a multiplayer session, let me know if you encounter any problems<details><summary>Click for details</summary>- The death hitmarker will now be displayed correctly</details>

## Version 1.1.52
- Added a configuration menu to the games main menu <details><summary>Click for details</summary>- Can be deactivated inside `nwnt.EverythingCanDieAlternative.cfg` > `EnableConfigMenu` or inside LethalConfig or inside the menu itself.<br>-  Changes inside the menu take effect immediately <br>- Configuration will be updated when starting a lobby now, you dont need to restart your game for the changes inside the UI to take effect</details>
- Added `EnableInfoLogs` to `nwnt.EverythingCanDieAlternative.cfg` <details><summary>Click for details</summary>- This will supress nearly all of the info logs as of 1.1.55, may help against lag spikes</details>
  
##
###### Version 1.1.51 (unreleased) - First UI-Config implementation

## Version 1.1.50
- Fixed that some enemies like `Light Eater` or `SCP682` will not die after their configured HP reach 0 <details><summary>Click for details</summary> - Added a more forceful way of removing enemies that resist the vanilla kill method<p>- This approach should be robust for any future enemies as well</details>
- Updated how `.Unimmortal = false` effects gameplay - it will now give enemies 999 vanilla HP, this wont save them from instakills<details><summary>Click for details</summary>Hitting such an enemy will not trigger the EverythingCanDieAlternative hit processing, this is unchanged from earlier patches <p> Before this patch, enemies would revert to vanilla HP values, you can make enemies use vanilla values by setting `.Enable = false`</details>
- Updated the SellBodiesFixed compatibility. The enemies `Light Eater` and `SCP682` will now spawn an item
  
## Version 1.1.43

- Added beta feature, that allows you to deactivate my mod for specific enemies if you want to preserve their original behavior<details><summary>Click for details</summary>Inside the configuration file `nwnt.EverythingCanDieAlternative_Enemy_Control.cfg` you can set `Enemy.Enable = false` and my mod will let the vanilla game handle health, hits etc.<p>This can be useful if specific enemies have built-in hit/health/death mechanisms that you want to preserve.</details>

## Version 1.1.42

- Updated the SellBodiesFixed compatibility. The enemies `SCP3166` (Gorefield) and `Rabbit?` will now spawn an item <details><summary>Click for details</summary> The original SellBodiesFixed mod does not spawn an item for them. These enemies are currently hardcoded with power level 2 and 1 items. Btw, i have no clue what mod adds the Rabbit enemy or if it even spawns naturally.</details>

## Version 1.1.41

- Updated the SellBodiesFixed compatibility. The enemies `Baldi` and `The Fiend` will now spawn an item <details><summary>Click for details</summary> The original SellBodiesFixed mod does not spawn an item for them. These enemies are currently hardcoded with power level 2 items, let me know if there are more enemies that dont spawn an item with the SellBodiesFixed mod and i add them.</details>
- Updated the default configuration so enemy health caps at 30, this should result in more consistent 1 shot kills when hitting an enemy with the [LethalThings](https://thunderstore.io/c/lethal-company/p/Evaisa/LethalThings/) Rocket Launcher, this only effects new generated configs, you can still configure health to be higher

## Version 1.1.40

- Fixed Enemies not being able to kill each other like BaboonHawks vs EyelessDogs <details><summary>Click for details</summary><p> Thanks to `SpinoRavenger` for reporting it on Discord!</details>

##
###### Hotfix 1.1.34 - Added SoftDependency for BrutalCompanyMinusExtraReborn

## Version 1.1.33
- Added beta compatibility for [BrutalCompanyMinusExtraReborn](https://thunderstore.io/c/lethal-company/p/SoftDiamond/BrutalCompanyMinusExtraReborn/) by SoftDiamond
- Updated default health configuration, enemies like Baldi will have 1 hp instead of 0 hp now
- Updated ReadMe to better communicate mod compatibilities

## Version 1.1.32
- Updated the framework for mod compatibilities
- Added beta compatibility for [LethalHands](https://thunderstore.io/c/lethal-company/p/SlapItNow/LethalHands/) by SlapItNow
- Updated ReadMe with correct shotgun damage<details>Thanks to `ToastIsToasty` for reporting it on Discord!</details>

###### Hotfix 1.1.31 - Uploaded the correct files

## Version 1.1.30
##

- Fixed the creation of health configs, for not referencing original enemy hp but instead always defaulting to 3 hp <details><summary>Click for details</summary><p> Thanks to `pxntxrez` for reporting it on discord!<p>This fix only takes effect when you delete your existing `nwnt.EverythingCanDieAlternative.cfg` file or during a fresh installation in a new modpack.<p>Before this fix, enemies like Forest Giant would default to 3 hp, now they default to 38 hp like in the vanilla game. You can of course still configure them back to 3 hp or whatever you like to. <p>The default configuration caps enemy hp at 38. You can still manually configure hp to be higher. Why is it limited? The enemy "The Fiend" is configured with 1000 hp, i dont think having an hitable enemy with 1000 hp is what someone expects when installing my mod. 38 is already way to much for the shovel or shotgun but eh, might change the hp cap later.</details>

## Version 1.1.20

- Fixed dead enemies not despawning by adding a feature, that despawns dead enemies <details><summary>Click for details</summary> <p>You can disable this feature in the new `nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg` by setting `EnableDespawnFeature` to `false`. <p>Why should you despawn an enemy? A Coilhead will just be froozen if dead if you dont despawn it, looks awful and is bad player feedback. <p>Why should you NOT despawn an enemy? Enemies like Baboon Hawks have proper death animation and proper corpses that are fine to leave as is. <p>You can configure for every mob if it should be despawned or not. For a couple of vanilla enemies with death animations it is defaulted to false. <p>This feature is compatible with SellBodiesFixed and EnhancedMonsters. </details>

## Version 1.1.10

- Fixed an error, where the enemies could only be hit on the first moon, but not on any moon afterwards in the same playthrough/session <details><summary>Click to see the error message</summary>Error setting up enemy: A variable with the identifier nwnt.EverythingCanDieAlternative.ECD_Health_1 already exists! Please use a different identifier.</details>


## Version 1.1.00

- Fixed an issue that counted one hit two times<details><summary>Click for technical notes:</summary><p>The mod now uses the LethalNetworkAPI to bypass the vanilla games hit and health system. May this lead to unforeseen problems? Perhaps, i keep an eye on it.<p>Vanilla Enemy health gets set to 999 for every enemy to not to worry about. <p>This mod now uses its own health tracking system based on the network id of the enemy. When a client hits an enemy the hit gets networked to the host. The host is the only source of truth and keeps track of enemy health. This means clients will no longer see how much health an enemy has inside the log as this information gets not transmitted back. When an enemy reaches 0 hp of the own health tracking the host simply calls the base games methods for killing it. Some modded enemies dont seem to despawn properly, SellBodiesFixed fixes this. <p>With the 1.0.1 approach i also ran into issues with killing some vanilla enemies at 1 hp instead of zero. This is now fixed too.</details>

---
<details><summary> Click for Older Versions</summary>

### Version 1.0.1

#### Fixes
- Changed the hit detection for modded enemies that deviate from using the standard enemyAi system
  - Now properly works with Shrimp, CountryRoadCreature
  - Locker should work as well
  - Could work with a wider range of modded enemies now
  - The configuration for clients should work better as well now

#### Technical Improvements
- Improved hit detection system to catch hits at network synchronization level
- More robust handling of network ownership and client/server interactions
- Better integration with the game's hit registration system
- More logging

---

### Version 1.0.0 (Initial Release)

#### Features
- Makes any mob using Lethal Company's enemyAI system killable (this includes most modded enemies)
- Configurable health values for each enemy
- Robust fallback system for multiplayer edge cases

- **Despawn Issues:**
  - Ghost Girl doesn't despawn when killed (needs more testing)
  - Herobrine and Football might have similar issues (untested)

#### Technical Notes
- If client config fails to load while hitting a monster, the mod defaults to allowing enemy deaths (needs further testing on how to prevent it or on how big the issue actually is, but if the issue appears the mod will just allow the monster to be killable no matter what, the configured hp might be ignored)
- More robust handling of edge cases compared to original EverythingCanDie mod
- Less precise configuration (differentiate between shovel and shotgun) and no explosions on enemy death compared to original EverythingCanDie mod

#### Future Plans For Known Issues
- Not planning to fix The Fiend or Locker (changed my mind, did try to fix it in 1.0.1)
- Ghost Girl despawn issue will remain as is (i leave her at immortal anyway)
- Investigating config synchronization between host and clients (works better as of 1.0.1)

#### Future Features?
- None, this mods only purpose is to be a robust mod to allow the death of enemies with being able to configure their hp, i want to use this mod alongside SellBodiesFixed or Enhanced_Monsters or whatever mods reward you for going on the hunt
</details>

---

- A word from Lutschlon: Hey, big thanks to nyakowint for having the original mod in an public github repository https://github.com/nyakowint/EverythingCanDie-LC/tree/main 
- As already mentioned in the readme, the creation of this mod was made possible thanks to using ai (sounds like iam putting an ad here lol). I use the pro version of claude.ai - i have no experience using chatgpt and only limited experience using copilot so i dont know if claude is the best or whatnot. So uhm what was i going to say... Iam not a programmer and only have a basic understanding of all of this. So if you are in the same shoes and you want to get into modding why not get assistance from ai. Have a good one fellas.
