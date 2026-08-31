using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity.EditorTools
{
    /// <summary>
    /// Builds the 2D wind-window scene. The scene holds one object: the bootstrap
    /// builds the camera, the chart and the pilots at runtime, so there is nothing
    /// else to serialise and nothing to drift out of sync with the code.
    /// </summary>
    public static class WindowSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/WindowScene.unity";
        private const string ActionsPath = "Assets/kitesim.inputactions";

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

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log($"WindowSceneBuilder: wrote {ScenePath}");
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
