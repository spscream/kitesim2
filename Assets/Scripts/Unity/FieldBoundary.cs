using UnityEngine;

namespace Kitesim.Unity
{
    public class FieldBoundary : MonoBehaviour
    {
        [SerializeField] private float fieldSize = 110f;
        [SerializeField] private float yellowInset = 3f;

        void Start()
        {
            CreateGround();
            DrawBoundary("RedBoundary", fieldSize, Color.red, 0.05f);
            DrawBoundary("YellowBoundary", fieldSize - 2f * yellowInset, Color.yellow, 0.04f);
        }

        private void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform);
            ground.transform.localPosition = new Vector3(0, -0.01f, 0);
            ground.transform.localScale = new Vector3(fieldSize / 10f, 1f, fieldSize / 10f);

            var renderer = ground.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.material = new Material(shader);
            renderer.material.color = new Color(0.35f, 0.45f, 0.25f);
        }

        private void DrawBoundary(string name, float size, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 5;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.loop = false;

            float half = size * 0.5f;
            lr.SetPosition(0, new Vector3(-half, 0.02f, -half));
            lr.SetPosition(1, new Vector3(half, 0.02f, -half));
            lr.SetPosition(2, new Vector3(half, 0.02f, half));
            lr.SetPosition(3, new Vector3(-half, 0.02f, half));
            lr.SetPosition(4, new Vector3(-half, 0.02f, -half));
        }
    }
}
