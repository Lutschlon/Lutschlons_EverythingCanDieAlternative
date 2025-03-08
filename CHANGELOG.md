### Version 1.1.0

- Fixed an issue that counted one hit two times

##### Technical Notes:
- The mod now uses the LethalNetworkAPI to bypass the vanilla games hit and health system. May this lead to unforseen problems? Perhaps, i keep an eye on it.
- Vanilla Enemy health gets set to 999 for every enemy to not to worry about. 
- This mod now uses its own health tracking system based on the network id of the enemy. When a client hits an enemy the hit gets networked to the host. The host is the only source of truth and keeps track of enemy health. This means clients will no longer see how much health an enemy has inside the log as this information gets not transmitted back. When an enenmy reaches 0 hp of our own health tracking the host simply calls the base games methods for killing it. Some modded enemies dont seem to despawn properly, SellBodiesFixed fixes this. 
- With the 1.0.1 approach i also ran into issues with killing vanilla enemies at 1 hp instead of zero. This is now fixed too. Not sure if it was happening in 1.0.1

---

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
- None, this mods only purpose is to be a robust mod to allow the death of enemeis with being able to configure their hp, i want to use this mod alongside SellBodiesFixed or Enhanced_Monsters or whatever mods reward you for going on the hunt

---

- A word from Lutschlon: Hey, big thanks to nyakowint for having the original mod in an public github repository https://github.com/nyakowint/EverythingCanDie-LC/tree/main 
- As already mentioned in the readme, the creation of this mod was made possible thanks to using ai (sounds like iam putting an ad here lol). I use the pro version of claude.ai - i have no experience using chatgpt and only limited experience using copilot so i dont know if claude is the best or whatnot. So uhm what was i going to say... Iam not exactly a programmer and only have a basic understanding of all of this. So if you are in the same shoes and you want to get into modding why not get assistance from ai. Have a good one fellas.