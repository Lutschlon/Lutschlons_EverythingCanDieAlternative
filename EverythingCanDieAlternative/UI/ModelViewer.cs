using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EverythingCanDieAlternative.UI
{
    /// <summary>
    /// Handles loading and displaying 3D models in the UI
    /// </summary>
    public class ModelViewer : MonoBehaviour
    {
        // The main camera for the model viewer
        private Camera modelViewerCamera;

        // The render texture to display the model
        private RenderTexture renderTexture;

        // The Raw Image component to display the render texture
        private RawImage displayImage;

        // The game object that contains the loaded model
        private GameObject currentModel;

        // The rotation speed for the model
        private float rotationSpeed = 30f;

        // Whether the model should auto-rotate
        private bool autoRotate = true;

        // Path to the asset bundle
        private string assetBundlePath;

        // The loaded asset bundle
        private AssetBundle modelAssetBundle;

        // Layer to use for rendering (default layer is always available)
        private const int MODEL_LAYER = 0; // Default layer

        // Initialize the model viewer
        public void Initialize(RawImage display, string bundlePath)
        {
            try
            {
                Plugin.LogInfo("ModelViewer.Initialize starting...");

                // Store references
                displayImage = display;
                assetBundlePath = bundlePath;

                // Create render texture - make it higher resolution for clarity
                renderTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
                renderTexture.Create();

                // Check if render texture was created successfully
                if (renderTexture.IsCreated())
                {
                    Plugin.LogInfo("Render texture created successfully");
                }
                else
                {
                    Plugin.LogError("Failed to create render texture");
                }

                // Assign render texture to display
                displayImage.texture = renderTexture;
                Plugin.LogInfo("Assigned render texture to display image");

                // Create camera for rendering - child of this GameObject
                var cameraObj = new GameObject("ModelViewerCamera");
                cameraObj.transform.SetParent(transform);
                modelViewerCamera = cameraObj.AddComponent<Camera>();
                modelViewerCamera.clearFlags = CameraClearFlags.SolidColor;
                modelViewerCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                modelViewerCamera.targetTexture = renderTexture;
                modelViewerCamera.cullingMask = 1 << MODEL_LAYER; // Use default layer instead of UI
                modelViewerCamera.orthographic = false;
                modelViewerCamera.fieldOfView = 60f;
                modelViewerCamera.nearClipPlane = 0.01f;
                modelViewerCamera.farClipPlane = 1000f;

                // Position camera
                modelViewerCamera.transform.position = new Vector3(0, 0, -3);
                modelViewerCamera.transform.LookAt(Vector3.zero);

                Plugin.LogInfo("Camera created and configured");

                // Load the asset bundle
                StartCoroutine(LoadAssetBundleAsync());

                Plugin.LogInfo("ModelViewer initialization complete");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error initializing ModelViewer: {ex.Message}");
                Plugin.LogError(ex.StackTrace);

                // Create fallback cube even after error
                CreateFallbackCube();
            }
        }

        // Load the asset bundle asynchronously
        private IEnumerator LoadAssetBundleAsync()
        {
            Plugin.LogInfo($"Loading asset bundle from: {assetBundlePath}");

            // Check for path validity outside try-catch
            if (string.IsNullOrEmpty(assetBundlePath))
            {
                Plugin.LogWarning("Asset bundle path is null or empty");
                CreateFallbackCube();
                yield break;
            }

            if (!File.Exists(assetBundlePath))
            {
                Plugin.LogWarning($"Asset bundle file not found at: {assetBundlePath}");
                CreateFallbackCube();
                yield break;
            }

            // Load the asset bundle asynchronously - WITHOUT try-catch around yield
            AssetBundleCreateRequest bundleLoadRequest = null;

            try
            {
                bundleLoadRequest = AssetBundle.LoadFromFileAsync(assetBundlePath);
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error initiating asset bundle load: {ex.Message}");
                Plugin.LogError(ex.StackTrace);
                CreateFallbackCube();
                yield break;
            }

            // This yield is outside of any try-catch
            yield return bundleLoadRequest;

            // Now handle the result after the yield
            try
            {
                modelAssetBundle = bundleLoadRequest.assetBundle;

                if (modelAssetBundle == null)
                {
                    Plugin.LogError("Failed to load asset bundle");
                    CreateFallbackCube();
                    yield break;
                }

                Plugin.LogInfo($"Asset bundle loaded successfully: {modelAssetBundle.name}");

                // Log available assets
                string[] assetNames = modelAssetBundle.GetAllAssetNames();
                Plugin.LogInfo($"Available assets in bundle ({assetNames.Length}):");
                foreach (string assetName in assetNames)
                {
                    Plugin.LogInfo($"  - {assetName}");
                }

                // Load the test cube model from the bundle
                LoadModel("testcube");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error processing loaded asset bundle: {ex.Message}");
                Plugin.LogError(ex.StackTrace);
                CreateFallbackCube();
            }
        }

        // Load a model from the asset bundle
        public void LoadModel(string modelName)
        {
            try
            {
                Plugin.LogInfo($"LoadModel called for: {modelName}");

                // Clean up previous model if exists
                if (currentModel != null)
                {
                    Destroy(currentModel);
                    currentModel = null;
                }

                // If modelName is null or we don't have an asset bundle, create a fallback cube
                if (string.IsNullOrEmpty(modelName) || modelAssetBundle == null)
                {
                    Plugin.LogInfo("Creating fallback cube due to null model name or missing asset bundle");
                    CreateFallbackCube();
                    return;
                }

                // Get all asset names in the bundle
                string[] assetNames = modelAssetBundle.GetAllAssetNames();
                GameObject modelPrefab = null;
                string targetAsset = null;

                Plugin.LogInfo($"Looking for model: '{modelName}' in {assetNames.Length} assets");

                // First try direct asset loading - this works best when you know the exact path
                if (assetNames.Length > 0)
                {
                    // Try to load the first prefab if we have any assets
                    targetAsset = assetNames[0];
                    for (int i = 0; i < assetNames.Length; i++)
                    {
                        Plugin.LogInfo($"Checking asset {i}: {assetNames[i]}");

                        // If we find a prefab, prioritize it
                        if (assetNames[i].EndsWith(".prefab"))
                        {
                            targetAsset = assetNames[i];
                            Plugin.LogInfo($"Found prefab: {targetAsset}");
                            break;
                        }
                    }

                    // Load the asset
                    Plugin.LogInfo($"Attempting to load asset: {targetAsset}");
                    modelPrefab = modelAssetBundle.LoadAsset<GameObject>(targetAsset);

                    if (modelPrefab != null)
                    {
                        Plugin.LogInfo($"Successfully loaded prefab: {targetAsset}");
                    }
                    else
                    {
                        Plugin.LogError($"Failed to load prefab: {targetAsset}");
                    }
                }

                // If we found and loaded a prefab, instantiate it
                if (modelPrefab != null)
                {
                    currentModel = Instantiate(modelPrefab);
                    currentModel.transform.SetParent(transform);
                    currentModel.transform.localPosition = Vector3.zero;
                    currentModel.transform.localRotation = Quaternion.identity;
                    currentModel.transform.localScale = Vector3.one * 0.7f; // Scale it down a bit

                    // Set the layer to our model layer so our camera can see it
                    SetLayerRecursively(currentModel, MODEL_LAYER);

                    Plugin.LogInfo($"Model '{modelName}' instantiated successfully");
                }
                else
                {
                    // If no matching prefab was found, create fallback cube
                    Plugin.LogWarning($"No matching prefab found for '{modelName}', using fallback cube");
                    CreateFallbackCube();
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error loading model: {ex.Message}");
                Plugin.LogError(ex.StackTrace);

                // Create fallback cube on error
                CreateFallbackCube();
            }
        }

        // Create a fallback cube when model loading fails
        private void CreateFallbackCube()
        {
            try
            {
                Plugin.LogInfo("Creating fallback test cube");

                // Clean up previous model if exists
                if (currentModel != null)
                {
                    Destroy(currentModel);
                    currentModel = null;
                }

                // Create a test cube
                currentModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                currentModel.transform.SetParent(transform);
                currentModel.transform.localPosition = Vector3.zero;
                currentModel.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                // Set the layer so our camera can see it
                currentModel.layer = MODEL_LAYER;

                // Add a simple colored material
                var renderer = currentModel.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.2f, 0.5f, 0.8f);
                    renderer.material = mat;

                    Plugin.LogInfo("Fallback cube material set");
                }

                Plugin.LogInfo("Fallback cube created");
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error creating fallback cube: {ex.Message}");
            }
        }

        // Set layer recursively for all child objects
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;

            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                if (child != null)
                {
                    SetLayerRecursively(child.gameObject, layer);
                }
            }
        }

        // Update is called once per frame
        private void Update()
        {
            // Auto-rotate the model if enabled
            if (autoRotate && currentModel != null)
            {
                currentModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }

        // Called when the component is destroyed
        private void OnDestroy()
        {
            try
            {
                Plugin.LogInfo("ModelViewer OnDestroy called");

                // Clean up resources
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    Destroy(renderTexture);
                    Plugin.LogInfo("Render texture released");
                }

                if (currentModel != null)
                {
                    Destroy(currentModel);
                    Plugin.LogInfo("Current model destroyed");
                }

                if (modelAssetBundle != null)
                {
                    modelAssetBundle.Unload(true);
                    Plugin.LogInfo("Asset bundle unloaded");
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError($"Error in ModelViewer.OnDestroy: {ex.Message}");
            }
        }
    }
}