Shader "ExampleShader"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };

            struct appdata
            {
                float4 vertex : POSITION;
            };

            StructuredBuffer<float4x4> _Transforms;
            uniform float _NumInstances;

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                // Hole die Instanz-Transformationsmatrix
                float4x4 instanceMatrix = _Transforms[instanceID];

                // Transformiere Vertex in Weltkoordinaten
                float4 wpos = mul(instanceMatrix, float4(v.vertex.xyz, 1.0));

                // Transformiere Weltkoordinaten in den Kamera-Space
                o.pos = mul(UNITY_MATRIX_VP, wpos);

                // Färbe das Objekt je nach Instanz-ID
                o.color = float4(instanceID / _NumInstances, 0.0f, 0.0f, 1.0f);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}