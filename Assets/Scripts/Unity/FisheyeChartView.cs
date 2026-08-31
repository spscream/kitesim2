using UnityEngine;
using UnityEngine.UI;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// The wind-window chart, rendered from the 3D world instead of drawn separately.
    ///
    /// The flat chart and a fisheye lens are the same thing: WindowChart's
    /// equidistant projection is r = theta, the equidistant fisheye mapping, and its
    /// stereographic projection is r = 2*tan(theta/2), the stereographic fisheye
    /// mapping. So a cubemap rendered at the pilot's hands, unwrapped by that
    /// mapping, IS the chart -- with the real kite models in it, and with the
    /// iso-power arcs falling exactly on the chart's concentric circles because they
    /// are one geometry seen through one lens.
    ///
    /// It sits in a corner over whichever main view is showing. See
    /// docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    public class FisheyeChartView : MonoBehaviour
    {
        [SerializeField] private int cubeResolution = 512;
        [SerializeField] private int outputResolution = 512;

        /// <summary>Fraction of the screen height the corner chart occupies.</summary>
        [SerializeField] private float screenFraction = 0.32f;
        [SerializeField] private float screenPadding = 16f;

        /// <summary>
        /// Six faces get re-rendered per refresh, so this is the one part of the view
        /// worth throttling. The chart is a supporting readout, not the thing being
        /// flown; a third of the frame rate is invisible on it and cheap.
        /// </summary>
        [SerializeField] private int refreshEveryNthFrame = 2;

        private Camera cubeCamera;
        private RenderTexture cubemap;
        private RenderTexture output;
        private Material material;
        private RawImage image;
        private Transform anchor;
        private int frame;

        public bool Visible
        {
            get => image != null && image.enabled;
            set { if (image != null) image.enabled = value; }
        }

        /// <summary>
        /// Build the chart. The lens sits at the pilot's hands and faces the window
        /// centre, which is what makes the result the chart rather than merely a
        /// wide-angle picture.
        /// </summary>
        public void Build(WindField wind, ChartProjection projection, Vec3 pilotPosition, Color background)
        {
            var shader = Shader.Find("Kitesim/FisheyeChart");
            if (shader == null)
            {
                Debug.LogError("[FisheyeChartView] Kitesim/FisheyeChart shader not found; no chart.");
                enabled = false;
                return;
            }

            cubemap = new RenderTexture(cubeResolution, cubeResolution, 24)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Cube,
                name = "FisheyeCube"
            };
            cubemap.Create();

            output = new RenderTexture(outputResolution, outputResolution, 0)
            {
                name = "FisheyeChart"
            };
            output.Create();

            material = new Material(shader);
            material.SetTexture("_Cube", cubemap);
            material.SetFloat("_MaxTheta", (float)(90.0 * MathD.Deg2Rad));
            material.SetFloat("_Stereographic", projection == ChartProjection.Stereographic ? 1f : 0f);
            material.SetColor("_Outside", background);

            Vec3 centre = WindowChart.Center(wind);
            Vec3 right = WindowChart.Right(wind);
            Vec3 up = Vec3.Cross(centre, right).Normalized();
            material.SetVector("_ChartForward", centre.ToVector3());
            material.SetVector("_ChartRight", right.ToVector3());
            material.SetVector("_ChartUp", up.ToVector3());

            CreateCubeCamera(pilotPosition);
            CreateCornerImage();
        }

        private void CreateCubeCamera(Vec3 pilotPosition)
        {
            var go = new GameObject("ChartLens");
            go.transform.SetParent(transform, false);
            // The pilot's hands, not their eyes: the chart is centred on the sphere
            // the kite actually flies on, and that sphere is centred on the hands.
            go.transform.position = pilotPosition.ToVector3() + Vector3.up * 1.2f;

            cubeCamera = go.AddComponent<Camera>();
            // Never rendered to the screen; it exists only to fill the cubemap.
            cubeCamera.enabled = false;
            cubeCamera.clearFlags = CameraClearFlags.SolidColor;
            cubeCamera.backgroundColor = new Color(0.80f, 0.84f, 0.87f);
            cubeCamera.nearClipPlane = 0.3f;
            cubeCamera.farClipPlane = 1500f;
        }

        private void CreateCornerImage()
        {
            var canvasGo = new GameObject("ChartCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();

            var imageGo = new GameObject("ChartImage");
            imageGo.transform.SetParent(canvasGo.transform, false);

            image = imageGo.AddComponent<RawImage>();
            image.texture = output;

            // Bottom-right, square, sized against screen height so it keeps its
            // proportions on any window shape.
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            float side = Screen.height * screenFraction;
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = new Vector2(-screenPadding, screenPadding);
        }

        void LateUpdate()
        {
            if (cubeCamera == null || material == null)
                return;

            float side = Screen.height * screenFraction;
            image.rectTransform.sizeDelta = new Vector2(side, side);

            if (!Visible)
                return;

            if (refreshEveryNthFrame > 1 && frame++ % refreshEveryNthFrame != 0)
                return;

            cubeCamera.RenderToCubemap(cubemap);
            Graphics.Blit(null, output, material);
        }

        void OnDestroy()
        {
            if (cubemap != null) cubemap.Release();
            if (output != null) output.Release();
        }
    }
}
