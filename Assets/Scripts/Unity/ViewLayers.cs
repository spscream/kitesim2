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

        /// <summary>
        /// Marks where the pilots stand. The chart never shows these: its lens sits
        /// in the lead pilot's hands, and a team stands within a metre of it, so a
        /// neighbour's post half a metre from the lens smears across a quarter of the
        /// chart. Measured, not feared -- it is what the first build did.
        /// </summary>
        public const int PilotMarker = 28;

        /// <summary>
        /// The lead pilot's own marker. Hidden from the chart for the reason above and
        /// from the first-person view, which looks out of that pilot's own eyes; the
        /// judge sees it like any other pilot on the field.
        /// </summary>
        public const int PilotSelf = 29;

        /// <summary>
        /// The flying lines. Kept off the chart: its lens sits in the lead pilot's
        /// hands, which is where the lines begin, so they would be drawn from inside
        /// the camera -- and being held at a minimum width in pixels, they smear
        /// across it. The chart shows where the kites are; the lines are for the
        /// views that stand back and watch them.
        /// </summary>
        public const int KiteLines = 27;

        public const int KiteTrueSizeMask = 1 << KiteTrueSize;
        public const int KiteChartSizeMask = 1 << KiteChartSize;
        public const int PilotMarkerMask = 1 << PilotMarker;
        public const int PilotSelfMask = 1 << PilotSelf;
        public const int KiteLinesMask = 1 << KiteLines;

        /// <summary>Everything a normal camera should see: the world, minus chart symbols.</summary>
        public const int WorldMask = ~KiteChartSizeMask;

        /// <summary>
        /// What a camera standing in the lead pilot's place should see: everything
        /// except that pilot themselves. Team-mates stay visible -- they are part of
        /// what a flier watches.
        /// </summary>
        public const int FirstPersonMask = WorldMask & ~PilotSelfMask;

        /// <summary>
        /// What the chart lens should see: the world, minus true-size kites, minus
        /// every pilot marker, minus the lines that start at the lens itself.
        /// </summary>
        public const int ChartMask =
            ~KiteTrueSizeMask & ~PilotSelfMask & ~PilotMarkerMask & ~KiteLinesMask;

        public static void Apply(UnityEngine.GameObject go, int layer)
        {
            go.layer = layer;
            foreach (UnityEngine.Transform child in go.transform)
                Apply(child.gameObject, layer);
        }
    }
}
