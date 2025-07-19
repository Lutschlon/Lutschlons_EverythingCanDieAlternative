using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace EverythingCanDieAlternative.UI
{
    public class ConfigMenuPatch
    {
        private static bool menuInjectionSuccessful = false;
        private static MenuManager lastMenuManagerInstance = null;

        static ConfigMenuPatch()
        {
            // Scene-based fallback - listen for scene changes
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only try scene-based injection if it's a menu scene
            if (scene.name.Contains("Menu") || scene.name.Contains("MainMenu"))
            {
                // Find a MenuManager to start the coroutine with, and delay the check
                var menuManager = UnityEngine.Object.FindObjectOfType<MenuManager>();
                if (menuManager != null)
                {
                    menuManager.StartCoroutine(DelayedSceneBasedCheck());
                }
            }
        }

        private static IEnumerator DelayedSceneBasedCheck()
        {
            // Wait longer to give the normal MenuManager patches time to work
            yield return new WaitForSeconds(3f);
            
            // Only attempt scene-based injection if normal methods truly failed
            if (!menuInjectionSuccessful)
            {
                Plugin.LogInfo("Normal menu injection methods failed, attempting scene-based fallback...");
                TryInjectToMainMenu("SceneLoad");
            }
        }


        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        public static void Start(ref MenuManager __instance)
        {
            if (__instance.isInitScene) return;

            // Reset state for new menu manager instance
            if (lastMenuManagerInstance != __instance)
            {
                menuInjectionSuccessful = false;
                lastMenuManagerInstance = __instance;
            }

            __instance.StartCoroutine(DelayedMainMenuInjection(__instance));
        }

        // Alternative patch point - sometimes MenuManager.Awake works better
        [HarmonyPatch(typeof(MenuManager), "Awake")]
        [HarmonyPostfix]
        public static void Awake(ref MenuManager __instance)
        {
            if (__instance.isInitScene) return;

            // Only try this if Start method failed
            __instance.StartCoroutine(AwakeDelayedInjection(__instance));
        }

        // Additional fallback - patch OnEnable which is called when the menu becomes active
        [HarmonyPatch(typeof(MenuManager), "OnEnable")]
        [HarmonyPostfix]
        public static void OnEnable(ref MenuManager __instance)
        {
            if (__instance.isInitScene || menuInjectionSuccessful) return;

            __instance.StartCoroutine(OnEnableDelayedInjection(__instance));
        }

        private static IEnumerator DelayedMainMenuInjection(MenuManager menuManager)
        {
            // Wait for the vanilla Start method to complete fully
            // Based on MenuManager.Start.txt, we need to wait for EventSystem setup and music coroutine
            yield return new WaitForSeconds(0.2f);
            TryInjectToMainMenu("Start");
        }

        private static IEnumerator AwakeDelayedInjection(MenuManager menuManager)
        {
            // Awake happens before Start, so wait longer
            yield return new WaitForSeconds(0.8f);
            if (!menuInjectionSuccessful)
            {
                TryInjectToMainMenu("Awake");
            }
        }

        private static IEnumerator OnEnableDelayedInjection(MenuManager menuManager)
        {
            // OnEnable can happen multiple times, so be more conservative
            yield return new WaitForSeconds(1.2f);
            if (!menuInjectionSuccessful)
            {
                TryInjectToMainMenu("OnEnable");
            }
        }

        private static void TryInjectToMainMenu(string source)
        {
            if (menuInjectionSuccessful) return;

            try
            {
                Plugin.LogInfo($"Attempting menu injection from {source}");
                InjectToMainMenu();
                menuInjectionSuccessful = true;
                Plugin.LogInfo($"Menu injection successful from {source}");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Menu injection failed from {source}: {ex.Message}");
                Plugin.LogError($"Trying minimal fallback...");
                TryMinimalFallback();
            }
        }


        private static void TryMinimalFallback()
        {
            try
            {
                Plugin.LogInfo("Attempting minimal fallback menu injection...");
                
                // Try to find any available menu structure
                var allMenuContainers = UnityEngine.Object.FindObjectsOfType<Canvas>()
                    .Where(c => c.name.Contains("Menu") || c.name.Contains("UI"))
                    .ToArray();

                foreach (var canvas in allMenuContainers)
                {
                    if (TryInjectIntoCanvas(canvas))
                    {
                        menuInjectionSuccessful = true;
                        Plugin.LogInfo("Minimal fallback injection successful");
                        return;
                    }
                }

                Plugin.LogError("All fallback methods failed. Menu button will not be available.");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Minimal fallback also failed: {ex.Message}");
            }
        }

        private static bool TryInjectIntoCanvas(Canvas canvas)
        {
            try
            {
                // Look for button containers in this canvas
                var buttons = canvas.GetComponentsInChildren<Button>();
                if (buttons.Length == 0) return false;

                // Find a suitable parent (preferably containing "main" or "menu" buttons)
                Transform buttonParent = null;
                foreach (var button in buttons)
                {
                    var parent = button.transform.parent;
                    if (parent != null && (parent.name.ToLower().Contains("main") || parent.name.ToLower().Contains("button")))
                    {
                        buttonParent = parent;
                        break;
                    }
                }

                if (buttonParent == null)
                {
                    buttonParent = buttons[0].transform.parent;
                }

                if (buttonParent == null) return false;

                // Create our button as a simple clone
                var referenceButton = buttons.FirstOrDefault(b => b.name.ToLower().Contains("quit") || b.name.ToLower().Contains("exit"));
                if (referenceButton == null) referenceButton = buttons[0];

                CreateConfigButton(buttonParent, referenceButton.gameObject);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void CreateConfigButton(Transform parent, GameObject referenceButton)
        {
            var configButtonObj = UnityEngine.Object.Instantiate(referenceButton, parent);
            configButtonObj.name = "ECDAConfigButton";

            var buttonText = configButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "> EverythingCanDieAlt";
            }

            var button = configButtonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => {
                    try
                    {
                        // Try to play sound
                        var menuManager = UnityEngine.Object.FindObjectOfType<MenuManager>();
                        if (menuManager != null && menuManager.MenuAudio != null)
                        {
                            menuManager.PlayConfirmSFX();
                        }
                    }
                    catch { }
                    
                    ConfigMenuManager.ToggleConfigMenu();
                });
            }

            // Position the button appropriately
            var rectTransform = configButtonObj.GetComponent<RectTransform>();
            var referenceRect = referenceButton.GetComponent<RectTransform>();
            if (rectTransform != null && referenceRect != null)
            {
                rectTransform.anchoredPosition = referenceRect.anchoredPosition + new Vector2(0, 50);
            }
        }

        private static void InjectToMainMenu()
        {
            Plugin.Log.LogInfo("Injecting ECDA configuration menu into main menu...");

            // More robust object finding with detailed error reporting
            var menuContainer = GameObject.Find("MenuContainer");
            if (!menuContainer)
            {
                // Try alternative names
                var alternatives = new[] { "Menu Container", "MainMenuContainer", "UI" };
                foreach (var alt in alternatives)
                {
                    menuContainer = GameObject.Find(alt);
                    if (menuContainer) 
                    {
                        Plugin.LogInfo($"Found menu container with alternative name: {alt}");
                        break;
                    }
                }
                
                if (!menuContainer)
                {
                    throw new InvalidOperationException("MenuContainer not found! Available GameObjects: " + 
                        string.Join(", ", UnityEngine.Object.FindObjectsOfType<GameObject>().Take(10).Select(go => go.name)));
                }
            }

            var mainButtonsTransform = menuContainer.transform.Find("MainButtons");
            if (!mainButtonsTransform)
            {
                // Log all children to help debug
                var children = new string[menuContainer.transform.childCount];
                for (int i = 0; i < menuContainer.transform.childCount; i++)
                {
                    children[i] = menuContainer.transform.GetChild(i).name;
                }
                throw new InvalidOperationException($"MainButtons not found! MenuContainer children: {string.Join(", ", children)}");
            }

            var quitButton = mainButtonsTransform.Find("QuitButton");
            if (!quitButton)
            {
                // Log all MainButtons children to help debug
                var children = new string[mainButtonsTransform.childCount];
                for (int i = 0; i < mainButtonsTransform.childCount; i++)
                {
                    children[i] = mainButtonsTransform.GetChild(i).name;
                }
                throw new InvalidOperationException($"QuitButton not found! MainButtons children: {string.Join(", ", children)}");
            }

            // Clone the quit button for our config button
            var configButtonObj = UnityEngine.Object.Instantiate(quitButton.gameObject, mainButtonsTransform);
            configButtonObj.name = "ECDAConfigButton";

            var buttonText = configButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "> EverythingCanDieAlt";
            }

            // Replace the click event
            var button = configButtonObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => {
                    // Play the button press sound when clicked
                    var menuManager = menuContainer.GetComponent<MenuManager>();
                    if (menuManager != null && menuManager.MenuAudio != null)
                    {
                        menuManager.PlayConfirmSFX();
                    }
                    ConfigMenuManager.ToggleConfigMenu();
                });
            }

            // Offsets all buttons inside the main buttons to make room
            var buttonsList = mainButtonsTransform.GetComponentsInChildren<Button>()
                .Select(b => b.gameObject);

            // Gets the smallest distance between two buttons
            var gameObjects = buttonsList.ToList();
            var positions = gameObjects
                .Where(b => b != configButtonObj)
                .Select(b => b.transform as RectTransform)
                .Select(t => t.anchoredPosition.y);
            var enumerable = positions.ToList();
            var offsets = enumerable
                .Zip(enumerable.Skip(1), (y1, y2) => Mathf.Abs(y2 - y1));
            var offset = offsets.Min();

            // Move all buttons up to make room
            foreach (var btn in gameObjects.Where(g => g != quitButton.gameObject))
                btn.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, offset);

            // Position our button above the quit button
            configButtonObj.GetComponent<RectTransform>().anchoredPosition =
                quitButton.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, offset);

            Plugin.Log.LogInfo("ECDA config button added to main menu");
        }

        public static void Initialize(Harmony harmony)
        {
            try
            {
                // Patch Start method (primary)
                var startMethod = AccessTools.Method(typeof(MenuManager), "Start");
                var startPostfixMethod = AccessTools.Method(typeof(ConfigMenuPatch), nameof(Start));
                harmony.Patch(startMethod, null, new HarmonyMethod(startPostfixMethod));

                // Patch Awake method (fallback 1)
                var awakeMethod = AccessTools.Method(typeof(MenuManager), "Awake");
                var awakePostfixMethod = AccessTools.Method(typeof(ConfigMenuPatch), nameof(Awake));
                harmony.Patch(awakeMethod, null, new HarmonyMethod(awakePostfixMethod));

                // Patch OnEnable method (fallback 2)
                var onEnableMethod = AccessTools.Method(typeof(MenuManager), "OnEnable");
                var onEnablePostfixMethod = AccessTools.Method(typeof(ConfigMenuPatch), nameof(OnEnable));
                harmony.Patch(onEnableMethod, null, new HarmonyMethod(onEnablePostfixMethod));

                Plugin.Log.LogInfo("Registered config menu patches with multiple fallback injection points");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing ConfigMenuPatch: {ex.Message}");
            }
        }

        // Check if menu injection was successful
        public static bool IsMenuInjectionSuccessful => menuInjectionSuccessful;
    }
}