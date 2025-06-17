Shader "Custom/UnlitComputeBufferShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                uint vertex_id : SV_VertexID; // We only need the vertex ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            // This is the link to our compute buffer!
            StructuredBuffer<float3> vertices;
            
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;

                // Instead of using the mesh's vertex data,
                // we look up the position from our compute buffer.
                float3 worldPos = vertices[v.vertex_id];
                
                // UnityObjectToClipPos transforms from object space to clip space.
                // Since our buffer is in WORLD space, we must first go from world to object.
                float3 localPos = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;
                
                o.vertex = UnityObjectToClipPos(localPos);
                
                // Note: Normals are not calculated here. For proper lighting,
                // you would need another compute shader pass to calculate normals.
                // For now, we'll just use a placeholder.
                o.normal = float3(0,1,0); 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Simple unlit fragment shader
                return _Color;
            }
            ENDCG
        }
    }
}