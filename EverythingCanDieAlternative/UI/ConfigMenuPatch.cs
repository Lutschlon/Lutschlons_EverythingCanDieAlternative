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
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        public static void Start(ref MenuManager __instance)
        {
            if (__instance.isInitScene) return;

            __instance.StartCoroutine(DelayedMainMenuInjection());
        }

        private static IEnumerator DelayedMainMenuInjection()
        {
            yield return new WaitForSeconds(0.1f);
            InjectToMainMenu();
        }

        private static void InjectToMainMenu()
        {
            Plugin.Log.LogInfo("Injecting ECDA configuration menu into main menu...");

            var menuContainer = GameObject.Find("MenuContainer");
            if (!menuContainer)
            {
                Plugin.Log.LogError("MenuContainer not found!");
                return;
            }

            var mainButtonsTransform = menuContainer.transform.Find("MainButtons");
            if (!mainButtonsTransform)
            {
                Plugin.Log.LogError("MainButtons not found!");
                return;
            }

            var quitButton = mainButtonsTransform.Find("QuitButton");
            if (!quitButton)
            {
                Plugin.Log.LogError("QuitButton not found!");
                return;
            }

            // Clone the quit button for our config button
            var configButtonObj = UnityEngine.Object.Instantiate(quitButton.gameObject, mainButtonsTransform);
            configButtonObj.name = "ECDAConfigButton";

            var buttonText = configButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = "EverythingCanDieAlt";
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
                var startMethod = AccessTools.Method(typeof(MenuManager), "Start");
                var startPostfixMethod = AccessTools.Method(typeof(ConfigMenuPatch), nameof(Start));
                harmony.Patch(startMethod, null, new HarmonyMethod(startPostfixMethod));

                Plugin.Log.LogInfo("Registered config menu patch");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing ConfigMenuPatch: {ex.Message}");
            }
        }
    }
}