using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kitesim.Unity
{
    public class TrainingSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActionsAsset;

        void Awake()
        {
            var pilot = CreatePilot();
            var kite = CreateKite(pilot);
            CreateInput(kite);
            CreateCamera(pilot, kite.transform);
            CreateWindUI(kite);
            CreateFieldBoundary();

            // Disable the editor-only main camera that ships with the empty scene
            var editorCamera = GameObject.FindWithTag("MainCamera");
            if (editorCamera != null && editorCamera.GetComponent<TrainingCamera>() == null)
                editorCamera.SetActive(false);
        }

        private Transform CreatePilot()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Pilot";
            go.transform.position = new Vector3(0f, 1f, 0f);
            go.tag = "Pilot";
            Destroy(go.GetComponent<CapsuleCollider>());
            return go.transform;
        }

        private KiteBehaviour CreateKite(Transform pilot)
        {
            var go = new GameObject("Fury");
            go.transform.position = pilot.position + Vector3.forward * 4.5f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var meshFilter = visual.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = LoadFuryMesh();

            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.9f, 0.2f, 0.2f);

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 4;
            lr.useWorldSpace = true;
            lr.startWidth = 0.01f;
            lr.endWidth = 0.01f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.red;
            lr.endColor = Color.red;

            var config = ScriptableObject.CreateInstance<KiteConfigAsset>();

            var kite = go.AddComponent<KiteBehaviour>();
            kite.Initialize(config, pilot, visual.transform);
            return kite;
        }

        private Mesh LoadFuryMesh()
        {
#if UNITY_EDITOR
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Fury_v01.blend");
            if (mesh != null)
                return mesh;
#endif
            return Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        }

        private void CreateInput(KiteBehaviour kite)
        {
            var go = new GameObject("KiteInputController");
            var controller = go.AddComponent<KiteInputController>();
            controller.Initialize(inputActionsAsset, kite);
        }

        private void CreateCamera(Transform pilot, Transform kite)
        {
            var go = new GameObject("TrainingCamera");
            var cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";

            var trainingCam = go.AddComponent<TrainingCamera>();
            trainingCam.Initialize(pilot, kite);
        }

        private void CreateWindUI(KiteBehaviour kite)
        {
            var go = new GameObject("WindUI");
            var ui = go.AddComponent<WindUI>();
            ui.Initialize(kite);
        }

        private void CreateFieldBoundary()
        {
            var go = new GameObject("FieldBoundary");
            go.AddComponent<FieldBoundary>();
        }
    }
}
