Shader "TMLine/TMLine" {
    Properties {
        [Toggle]_EnableBoundaryLine("Enable Boundary Line (Control Tex R Channel)", int)=1
        [Toggle]_EnableOutlineLine("Enable Outline Line (Control Tex G Channel)", int)=1
        [Toggle]_EnableCreaseLine("Enable Crease Line (Control Tex B Channel)", int)=1
        [Toggle]_EnableForcedLine("Enable Forced Line (Control Tex A Channel)", int)=0
        _WidthScale("WidthScale",Float) = 2
        _Color("Color",Color)=(0, 0, 0, 1)
        _ColorTex("Color Texture(颜色贴图)",2D)="white"{}
        _ControlTex("Control Texture(控制贴图)",2D)="white"{}
        _WidthTex("Width Texture(宽度贴图)",2D)="white"{}
        _NormalOffset("Normal Offset",Float) = 0.001
    }

    HLSLINCLUDE
    StructuredBuffer<float3> Vertices;
    StructuredBuffer<float3> Normals;
    StructuredBuffer<float2> Uvs;
    ENDHLSL

    SubShader{
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass {
            HLSLPROGRAM
            #include "UnityCG.cginc"
            #pragma target 2.5
            #pragma vertex vertexShader
            #pragma fragment fragmentShader
            #pragma geometry geometryShader

            struct g2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2g {
                float4 vertex1 : POSITION;
                float4 vertex2 : COLOR0;
                float4 uv : TEXCOORD0;
            };

            struct TMDataStruct {
                int Vertex1;
                int Vertex2;
                int Triangle1Vertex3;
                int Triangle2Vertex3;
            };

            float4 _Color;
            float _NormalOffset;
            float _WidthScale;
            int _EnableBoundaryLine;
            int _EnableOutlineLine;
            int _EnableCreaseLine;
            int _EnableForcedLine;
            sampler2D _ColorTex;
            sampler2D _ControlTex;
            sampler2D _WidthTex;
            StructuredBuffer<TMDataStruct> TMData;

            v2g vertexShader(uint id : SV_VertexID, uint inst : SV_InstanceID) {
                v2g o;
                TMDataStruct Data      = TMData[id];
                float2 uv1             = Uvs[Data.Vertex1];
                float2 uv2             = Uvs[Data.Vertex2];
                float4 vertex1         = float4(Vertices[Data.Vertex1], 1.0f);
                float4 vertex2         = float4(Vertices[Data.Vertex2], 1.0f);
                float4 vControl        = tex2Dlod(_ControlTex, float4((uv1 + uv2) * 0.5f, 0, 0));
                float4 triangle1Vertex = float4(Vertices[Data.Triangle1Vertex3], 1.0f);
                float3 vertex1Normal   = Normals[Data.Vertex1];
                float3 vertex2Normal   = Normals[Data.Vertex2];
                float4 centerPoint1    = (vertex1 + vertex2 + triangle1Vertex) / 3.0f;
                float3 viewTriangle1   = ObjSpaceViewDir(centerPoint1);

                bool isBoundary = !step(0, Data.Triangle2Vertex3) * !step(vControl.r, 0) * _EnableBoundaryLine; // Data.triangle2Vertex < 0

                float4 triangle2Vertex = float4(Vertices[Data.Triangle2Vertex3 * !isBoundary], 1.0f);

                float3 v1 = (vertex2 - vertex1).xyz;
                float3 v2 = (triangle1Vertex - vertex1).xyz;
                float3 v3 = (triangle2Vertex - vertex1).xyz;

                float3 face1Normal = cross(v1, v2);
                float3 face2Normal = cross(v3, v1);

                // !step(xx, 0) => xx > 0, step(xx, 0) => xx <= 0
                bool isOutline = !isBoundary * !step(0, dot(face1Normal, viewTriangle1) * dot(face2Normal, viewTriangle1)) * !step(vControl.g, 0) * _EnableOutlineLine;
                // pow(dot(face1Normal, face2Normal) / cos(degree), 2)在[0, PI/2]上单调递增, 可避免开方
                bool isCrease  = !isBoundary * step(pow(dot(face1Normal, face2Normal) / cos(1.0472f), 2), dot(face1Normal, face1Normal) * dot(face2Normal, face2Normal)) * !step(vControl.b, 0) * _EnableCreaseLine;
                bool isForced  = !step(vControl.a, 0) * _EnableForcedLine;
                bool isLine = isBoundary | isOutline | isCrease | isForced;
                
                vertex1.xyz *= isLine;
                vertex2.xyz *= isLine;

                //把线往模型法线方向移出一点，防止线画和面共面而穿模
                o.vertex1 = UnityObjectToClipPos(vertex1.xyz + vertex1Normal * _NormalOffset);
                o.vertex2 = UnityObjectToClipPos(vertex2.xyz + vertex2Normal * _NormalOffset);
                
                o.uv = float4(uv1, uv2);
                
                return o;
            }

            /*
            [maxvertexcount(2)]
            void geometryShader(point v2g input[1], inout LineStream<g2f> stream) {
                if (input[0].isLine == 0) {
                    return;
                }
                g2f o;
                o.vertex = input[0].vertex1;
                stream.Append(o);
                o.vertex = input[0].vertex2;
                stream.Append(o);
                stream.RestartStrip();
            }
            */
            
            [maxvertexcount(6)]
            void geometryShader(point v2g inputs[1], inout TriangleStream<g2f> stream) {
                g2f o;

                v2g input = inputs[0];
                float2 uv0 = input.uv.xy;
                float2 uv1 = input.uv.zw;
                float3 e0 = input.vertex1.xyz / input.vertex1.w;
                float3 e1 = input.vertex2.xyz / input.vertex2.w;
                float4 e0Width = tex2Dlod(_WidthTex, float4(uv0, 0, 0));
                float4 e1Width = tex2Dlod(_WidthTex, float4(uv1, 0, 0));
                float width0 = max(e0Width.r, max(e0Width.g, max(e0Width.b, e0Width.a))) * _WidthScale;
                float width1 = max(e1Width.r, max(e1Width.g, max(e1Width.b, e1Width.a))) * _WidthScale;

                float2 v = normalize(e1.xy - e0.xy);
                float2 n = float2(-v.y, v.x) * 0.005f;

                float2 ext = 0.001f * v;
                float4 v0 = float4(e0.xy + n * 0.5f * width0 * (e0.z + 1) * 0.5f - ext, e0.z, 1.0f) * input.vertex1.w;
                float4 v1 = float4(e0.xy - n * 0.5f * width0 * (e0.z + 1) * 0.5f - ext, e0.z, 1.0f) * input.vertex1.w;
                float4 v2 = float4(e1.xy + n * 0.5f * width1 * (e1.z + 1) * 0.5f + ext, e1.z, 1.0f) * input.vertex2.w;
                float4 v3 = float4(e1.xy - n * 0.5f * width1 * (e1.z + 1) * 0.5f + ext, e1.z, 1.0f) * input.vertex2.w;
                
                o.vertex = v0;
                o.uv = uv0;
                stream.Append(o);
                o.vertex = v3;
                o.uv = uv1;
                stream.Append(o);
                o.vertex = v2;
                o.uv = uv1;
                stream.Append(o);
                stream.RestartStrip();

                o.vertex = v0;
                o.uv = uv0;
                stream.Append(o);
                o.vertex = v1;
                o.uv = uv0;
                stream.Append(o);
                o.vertex = v3;
                o.uv = uv1;
                stream.Append(o);
                stream.RestartStrip();
            }

            float4 fragmentShader(g2f i) : SV_Target {
                return tex2D(_ColorTex, i.uv) * _Color;
            }

            ENDHLSL
        }
    }
}