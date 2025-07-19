using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace EverythingCanDieAlternative.UI
{
    // Utility class for loading enemy preview images
    public static class EnemyImageLoader
    {
        // Dictionary to cache loaded textures
        private static Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        // Path to the UI images directory
        private static string uiImagesPath;

        // Flag to track if the cache has been initialized
        private static bool isInitialized = false;

        // Initialize the image loader, setting up paths and directories
        public static void Initialize()
        {
            if (isInitialized) return;

            try
            {
                // Get the plugin base directory
                string pluginDir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);

                // Set the UI images path
                uiImagesPath = Path.Combine(pluginDir);

                // Create the directory if it doesn't exist
                if (!Directory.Exists(uiImagesPath))
                {
                    Directory.CreateDirectory(uiImagesPath);
                    Plugin.LogInfo($"Created UI images directory at {uiImagesPath}");
                }
                else
                {
                    Plugin.LogInfo($"UI images directory found at {uiImagesPath}");
                }

                isInitialized = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error initializing EnemyImageLoader: {ex.Message}");
            }
        }

        // Clear the texture cache (e.g., when exiting the menu)
        public static void ClearCache()
        {
            // Destroy textures to avoid memory leaks
            foreach (var texture in textureCache.Values)
            {
                UnityEngine.Object.Destroy(texture);
            }

            textureCache.Clear();
            Plugin.LogInfo("Enemy image cache cleared");
        }

        // Get a texture for the specified enemy name, returns null if no image exists
        public static Texture2D GetEnemyTexture(string enemyName)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            // If we've already loaded this texture, return it from cache
            if (textureCache.TryGetValue(enemyName, out Texture2D cachedTexture))
            {
                return cachedTexture;
            }

            try
            {
                // Sanitize enemy name to ensure consistent naming
                string sanitizedName = Plugin.RemoveInvalidCharacters(enemyName);

                // Check for image with .png extension
                string imagePath = Path.Combine(uiImagesPath, $"{sanitizedName}.png");
                if (!File.Exists(imagePath))
                {
                    // Check if a jpeg exists
                    imagePath = Path.Combine(uiImagesPath, $"{sanitizedName}.jpg");
                    if (!File.Exists(imagePath))
                    {
                        // No image found for this enemy
                        return null;
                    }
                }

                // Load the texture from file
                byte[] fileData = File.ReadAllBytes(imagePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    // Cache the loaded texture
                    textureCache[enemyName] = texture;
                    return texture;
                }

                // Failed to load image
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error loading enemy image for {enemyName}: {ex.Message}");
                return null;
            }
        }
    }
}