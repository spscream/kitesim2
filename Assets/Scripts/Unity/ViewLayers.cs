namespace Kitesim.Unity
{
    /// <summary>
    /// Layers that let one world be drawn differently by different cameras.
    ///
    /// The chart is an instrument, and instruments draw their symbols larger than
    /// scale -- the kite is 3 degrees across, too small to read on a chart. To draw
    /// it larger there while still rendering the one real world, each
    /// kite carries two models: one at true size for the judge and pilot, one
    /// exaggerated for the chart. Each camera sees exactly one of them.
    ///
    /// The indices are user layers, deliberately high so they cannot collide with
    /// anything a project would add early. Names are set by WindowSceneBuilder for
    /// legibility in the inspector; nothing at runtime depends on them, because
    /// culling masks work on indices.
    /// </summary>
    public static class ViewLayers
    {
        /// <summary>Kite drawn at its real size. Hidden from the chart.</summary>
        public const int KiteTrueSize = 30;

        /// <summary>Kite drawn oversized for the chart. Hidden from every other view.</summary>
        public const int KiteChartSize = 31;

        public const int KiteTrueSizeMask = 1 << KiteTrueSize;
        public const int KiteChartSizeMask = 1 << KiteChartSize;

        /// <summary>Everything a normal camera should see: the world, minus chart symbols.</summary>
        public const int WorldMask = ~KiteChartSizeMask;

        /// <summary>Everything the chart lens should see: the world, minus true-size kites.</summary>
        public const int ChartMask = ~KiteTrueSizeMask;

        public static void Apply(UnityEngine.GameObject go, int layer)
        {
            go.layer = layer;
            foreach (UnityEngine.Transform child in go.transform)
                Apply(child.gameObject, layer);
        }
    }
}
