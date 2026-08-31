using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity.EditorTools
{
    /// <summary>
    /// Builds the wind-window scene. The scene holds one object: the bootstrap
    /// builds the world, the cameras and the pilots at runtime, so there is nothing
    /// else to serialise and nothing to drift out of sync with the code.
    /// </summary>
    public static class WindowSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/WindowScene.unity";
        private const string ActionsPath = "Assets/kitesim.inputactions";
        private const string ModelPath = "Assets/Models/Fury_v01.blend";

        [MenuItem("Kitesim/Rebuild Window Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            var bootstrap = go.AddComponent<WindowSceneBootstrap>();

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
                Debug.LogError($"WindowSceneBuilder: {ActionsPath} not found");

            // The judge's view instantiates this; without it WindowKiteMesh falls
            // back to a placeholder box and says so.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                Debug.LogError($"WindowSceneBuilder: {ModelPath} not found");

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.FindProperty("kiteModel").objectReferenceValue = model;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            NameViewLayers();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"WindowSceneBuilder: wrote {ScenePath}");
        }

        /// <summary>
        /// Give the layers ViewLayers uses their names, so the scene is legible in
        /// the inspector. Nothing at runtime depends on this -- culling masks work on
        /// indices, and the scene builds correctly with the layers unnamed.
        /// </summary>
        private static void NameViewLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null)
                return;

            SetLayerName(layers, ViewLayers.KiteTrueSize, "KiteTrueSize");
            SetLayerName(layers, ViewLayers.KiteChartSize, "KiteChartSize");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerName(SerializedProperty layers, int index, string name)
        {
            if (index < 0 || index >= layers.arraySize)
                return;

            SerializedProperty layer = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(layer.stringValue))
                layer.stringValue = name;
            else if (layer.stringValue != name)
                Debug.LogWarning($"WindowSceneBuilder: layer {index} is already '{layer.stringValue}', "
                    + $"but ViewLayers wants it for '{name}'.");
        }

        private static void RegisterInBuildSettings()
        {
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath)
                    return;
            }

            var scenes = new EditorBuildSettingsScene[EditorBuildSettings.scenes.Length + 1];
            EditorBuildSettings.scenes.CopyTo(scenes, 0);
            scenes[scenes.Length - 1] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
