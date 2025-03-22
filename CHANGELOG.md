###### Version 1.1.34<br> - Added SoftDependency for BrutalCompanyMinusExtraReborn

###### Version 1.1.33<br> - Added beta compatibility for BrutalCompanyMinusExtraReborn by SoftDiamond <br> - Changed default health configuration, enemies like Baldi will have 1 hp instead of 0 hp now <br> - Updated ReadMe to better communicate mod compatibilities

###### Version 1.1.32<br> - Changed the framework for mod compatibilities<br>- Added beta compatibility for LethalHands by SlapItNow<br>- Updated ReadMe with correct shotgun damage<details>Thanks to `ToastIsToasty` for reporting!</details>

###### Version 1.1.31<br> - Uploaded the correct files
## Version 1.1.3

- Fixed the creation of health configs, for not referencing original enemy hp but instead always defaulting to 3 hp <details><summary>Click for details</summary><p> Thanks to `pxntxrez` for reporting it on discord!<p>This fix only takes effect when you delete your existing `nwnt.EverythingCanDieAlternative.cfg` file or during a fresh installation in a new modpack.<p>Before this fix, enemies like Forest Giant would default to 3 hp, now they default to 38 hp like in the vanilla game. You can of course still configure them back to 3 hp or whatever you like to. <p>The default configuration caps enemy hp at 38. You can still manually configure hp to be higher. Why is it limited? The enemy "The Fiend" is configured with 1000 hp, i dont think having an hitable enemy with 1000 hp is what someone expects when installing my mod. 38 is already way to much for the shovel or shotgun but eh, might change the hp cap later.</details>

## Version 1.1.2

- Fixed dead enemies not despawning by adding a feature, that despawns dead enemies <details><summary>Click for details</summary> <p>You can disable this feature in the new `nwnt.EverythingCanDieAlternative_Despawn_Rules.cfg` by setting `EnableDespawnFeature` to `false`. <p>Why should you despawn an enemy? A Coilhead will just be froozen if dead if you dont despawn it, looks awful and is bad player feedback. <p>Why should you NOT despawn an enemy? Enemies like Baboon Hawks have proper death animation and proper corpses that are fine to leave as is. <p>You can configure for every mob if it should be despawned or not. For a couple of vanilla enemies with death animations it is defaulted to false. <p>This feature is compatible with SellBodiesFixed and EnhancedMonsters. </details>

## Version 1.1.1

- Fixed an error, where the enemies could only be hit on the first moon, but not on any moon afterwards in the same playthrough/session</details><summary>Click to see the error message</summary>Error setting up enemy: A variable with the identifier nwnt.EverythingCanDieAlternative.ECD_Health_1 already exists! Please use a different identifier.</details>


## Version 1.1.0

- Fixed an issue that counted one hit two times<details><summary>Click for technical notes:</summary><p>The mod now uses the LethalNetworkAPI to bypass the vanilla games hit and health system. May this lead to unforeseen problems? Perhaps, i keep an eye on it.<p>Vanilla Enemy health gets set to 999 for every enemy to not to worry about. <p>This mod now uses its own health tracking system based on the network id of the enemy. When a client hits an enemy the hit gets networked to the host. The host is the only source of truth and keeps track of enemy health. This means clients will no longer see how much health an enemy has inside the log as this information gets not transmitted back. When an enemy reaches 0 hp of our own health tracking the host simply calls the base games methods for killing it. Some modded enemies dont seem to despawn properly, SellBodiesFixed fixes this. <p>With the 1.0.1 approach i also ran into issues with killing some vanilla enemies at 1 hp instead of zero. This is now fixed too.</details>

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
