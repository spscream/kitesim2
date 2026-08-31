// Turns a cubemap rendered at the pilot's hands into the wind-window chart.
//
// This is not an approximation of the chart -- it is the chart. WindowChart's
// equidistant projection is r = theta, which is exactly the equidistant fisheye
// mapping; its stereographic projection is r = 2*tan(theta/2), which is exactly
// the stereographic fisheye mapping. So a fisheye lens at the pilot's hands
// looking downwind reproduces the flat chart from the 3D world, with the real
// kite models in it, and the iso-power arcs land on the chart's concentric
// circles because they are the same geometry seen through the same lens.
//
// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
Shader "Kitesim/FisheyeChart"
{
    Properties
    {
        _Cube ("Environment", Cube) = "" {}
        _MaxTheta ("Half angle of the disc, radians", Float) = 1.5707963
        _Stereographic ("Stereographic instead of equidistant", Float) = 0
        _Outside ("Colour outside the disc", Color) = (0.05, 0.06, 0.08, 1)
        _EdgeSoftness ("Edge softness in uv", Float) = 0.004
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Cube;
            float _MaxTheta;
            float _Stereographic;
            float4 _Outside;
            float _EdgeSoftness;

            // The chart frame in world space: where the centre of the disc points,
            // and which way is right and up on it.
            float3 _ChartForward;
            float3 _ChartRight;
            float3 _ChartUp;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Only the sky half of the disc is drawn, and the target is 2:1 to
                // match. Nothing is lost by this: the chart centre is the horizontal
                // downwind direction, so the whole flyable hemisphere already lands in
                // the upper half -- a kite at azimuth 90 and elevation 45, for
                // instance, sits on the rim at 45 degrees up and to the right. The
                // lower half was the ground, and drawing it cost three quarters of
                // the picture. This is also exactly the shape the flat chart draws:
                // flat bottom for the ground, arc for the window edge.
                //
                // x spans -1..1 across the width, y spans 0..1 up the height, so the
                // window centre sits at the bottom middle and the zenith at the top.
                float2 p = float2(i.uv.x * 2.0 - 1.0, i.uv.y);
                float r = length(p);
                if (r > 1.0)
                    return _Outside;

                float theta;
                if (_Stereographic > 0.5)
                {
                    // r is already normalised so that r = 1 is the window edge, where
                    // 2*tan(theta/2) reaches its edge value; inverting gives this.
                    theta = 2.0 * atan(r * tan(_MaxTheta * 0.5));
                }
                else
                {
                    theta = r * _MaxTheta;
                }

                float2 radial = r > 1e-6 ? p / r : float2(0.0, 0.0);
                float sinTheta = sin(theta);

                float3 dir = _ChartForward * cos(theta)
                           + _ChartRight * (radial.x * sinTheta)
                           + _ChartUp * (radial.y * sinTheta);

                fixed4 c = texCUBE(_Cube, dir);

                // Feather the rim so the disc does not alias into a staircase.
                float edge = smoothstep(1.0, 1.0 - _EdgeSoftness, r);
                return lerp(_Outside, c, edge);
            }
            ENDCG
        }
    }

    Fallback Off
}
