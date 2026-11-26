Shader "HoneyComb/UI/Pollen"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _DetailTex("Pollen texture",2D)="white"{}
  _DetailOpacity("Texture Opacity",Range(0,1))=1
  [Enum(Normal,0,Multiply,1,Screen,2,Overlay,3)] _DetailBlend("Texture Blend",Float)=1
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
  [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
 }
 SubShader {
  Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
  Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha
  ColorMask [_ColorMask]
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float3 detail:TEXCOORD1;};
   struct v2f {float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float3 detail:TEXCOORD1;float4 world:TEXCOORD2;};
   sampler2D _MainTex,_DetailTex;
   fixed4 _TextureSampleAdd;
   float4 _ClipRect;
   float _DetailOpacity, _DetailBlend;
   v2f vert(appdata v){v2f o;o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.detail=v.detail;return o;}
   fixed4 frag(v2f i):SV_Target {
    fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
    fixed4 detail=tex2D(_DetailTex,frac(i.detail.xy));
    fixed3 blended=detail.rgb;
    if(_DetailBlend>.5 && _DetailBlend<1.5) blended=c.rgb*detail.rgb;
    else if(_DetailBlend>=1.5 && _DetailBlend<2.5) blended=1-(1-c.rgb)*(1-detail.rgb);
    else if(_DetailBlend>=2.5) blended=lerp(2*c.rgb*detail.rgb,1-2*(1-c.rgb)*(1-detail.rgb),step(.5,c.rgb));
    // Texture strength changes RGB only: keep the sprite silhouette and opacity.
    c.rgb=lerp(c.rgb,blended,saturate(_DetailOpacity*detail.a*i.detail.z));
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a-.001);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
