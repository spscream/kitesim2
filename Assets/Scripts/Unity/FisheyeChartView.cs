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
        /// <summary>
        /// The cube is sampled across the full width of the screen when the chart is
        /// the view, so it carries the sharpness of the whole picture.
        /// </summary>
        [SerializeField] private int cubeResolution = 1024;

        /// <summary>Height of the chart target; it is built twice this wide.</summary>
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
        private int frame;

        private bool cornerRequested = true;
        private bool fullscreen;

        /// <summary>
        /// Clears the screen behind the disc when the chart is the whole view. The
        /// chart is an overlay, not a camera, so without something drawing the
        /// backbuffer there would be nothing underneath it. It renders no geometry --
        /// the disc already contains the world.
        /// </summary>
        public Camera BackdropCamera { get; private set; }

        /// <summary>Whether the corner overlay is wanted. Ignored while full screen.</summary>
        public bool CornerVisible
        {
            get => cornerRequested;
            set { cornerRequested = value; Apply(); }
        }

        /// <summary>Show the chart as the whole view rather than in the corner.</summary>
        public void SetFullscreen(bool value)
        {
            fullscreen = value;
            Apply();
        }

        private bool Showing => fullscreen || cornerRequested;

        private void Apply()
        {
            if (image == null)
                return;

            image.enabled = Showing;
            if (BackdropCamera != null)
                BackdropCamera.enabled = fullscreen;

            RectTransform rect = image.rectTransform;
            if (fullscreen)
            {
                // Centred and as large as the window allows, keeping it square: the
                // chart is a disc, and stretching it would falsify the projection
                // this whole view exists to be exact about.
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-screenPadding, screenPadding);
            }
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

            // Twice as wide as tall: the chart is the sky half of the disc, so a
            // square target would waste half its pixels on nothing.
            output = new RenderTexture(outputResolution * 2, outputResolution, 0)
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
            CreateBackdropCamera(background);
            CreateImage();
            Apply();
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
            // The chart shows the oversized kite, not the true-size one. See ViewLayers.
            cubeCamera.cullingMask = ViewLayers.ChartMask;
        }

        private void CreateBackdropCamera(Color background)
        {
            var go = new GameObject("ChartBackdrop");
            go.transform.SetParent(transform, false);

            BackdropCamera = go.AddComponent<Camera>();
            BackdropCamera.clearFlags = CameraClearFlags.SolidColor;
            BackdropCamera.backgroundColor = background;
            BackdropCamera.cullingMask = 0;
            BackdropCamera.enabled = false;
        }

        private void CreateImage()
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
            float width = Screen.height * screenFraction * 2f;
            image.rectTransform.sizeDelta = new Vector2(width, width * 0.5f);
        }

        void LateUpdate()
        {
            if (cubeCamera == null || material == null)
                return;

            if (!Showing)
                return;

            // Always 2:1, so the half-disc keeps its shape: stretching it would
            // falsify the projection this view exists to be exact about. Sized from
            // the canvas rather than from Screen, because a CanvasScaler can put the
            // two in different units and only the canvas is what the image lives in.
            Rect canvas = ((RectTransform)image.canvas.transform).rect;
            float width = fullscreen
                ? Mathf.Min(canvas.width, canvas.height * 2f)
                : canvas.height * screenFraction * 2f;
            image.rectTransform.sizeDelta = new Vector2(width, width * 0.5f);

            // Throttling a corner readout is free; throttling the view someone is
            // flying by is not.
            int every = fullscreen ? 1 : refreshEveryNthFrame;
            if (every > 1 && frame++ % every != 0)
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
