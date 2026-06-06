// UI 可编辑属性
uniform sampler2D	_FaceTex;					// Alpha：有符号距离
uniform float		_FaceUVSpeedX;
uniform float		_FaceUVSpeedY;
uniform fixed4		_FaceColor;					// RGBA：颜色+不透明度
uniform float		_FaceDilate;				// v[0, 1]
uniform float		_OutlineSoftness;			// v[0, 1]

uniform sampler2D	_OutlineTex;				// RGBA：颜色+不透明度
uniform float		_OutlineUVSpeedX;
uniform float		_OutlineUVSpeedY;
uniform fixed4		_OutlineColor;				// RGBA：颜色+不透明度
uniform float		_OutlineWidth;				// v[0, 1]

uniform float		_Bevel;						// v[0, 1]
uniform float		_BevelOffset;				// v[-1, 1]
uniform float		_BevelWidth;				// v[-1, 1]
uniform float		_BevelClamp;				// v[0, 1]
uniform float		_BevelRoundness;			// v[0, 1]

uniform sampler2D	_BumpMap;					// 法线贴图
uniform float		_BumpOutline;				// v[0, 1]
uniform float		_BumpFace;					// v[0, 1]

uniform samplerCUBE	_Cube;						// 立方体/球体贴图
uniform fixed4 		_ReflectFaceColor;			// RGB 强度
uniform fixed4		_ReflectOutlineColor;
//统一浮动 _EnvTiltX；// v[-1, 1]
//统一浮动 _EnvTiltY；// v[-1, 1]
uniform float3      _EnvMatrixRotation;
uniform float4x4	_EnvMatrix;

uniform fixed4		_SpecularColor;				// RGB 强度
uniform float		_LightAngle;				// v[0,τ]
uniform float		_SpecularPower;				// v[0, 1]
uniform float		_Reflectivity;				// v[5, 15]
uniform float		_Diffuse;					// v[0, 1]
uniform float		_Ambient;					// v[0, 1]

uniform fixed4		_UnderlayColor;				// RGBA：颜色+不透明度
uniform float		_UnderlayOffsetX;			// v[-1, 1]
uniform float		_UnderlayOffsetY;			// v[-1, 1]
uniform float		_UnderlayDilate;			// v[-1, 1]
uniform float		_UnderlaySoftness;			// v[0, 1]

uniform fixed4 		_GlowColor;					// RGBA：颜色+强度
uniform float 		_GlowOffset;				// v[-1, 1]
uniform float 		_GlowOuter;					// v[0, 1]
uniform float 		_GlowInner;					// v[0, 1]
uniform float 		_GlowPower;					// v[1, 1/(1+4*4)]

// API 可编辑属性
uniform float 		_ShaderFlags;
uniform float		_WeightNormal;
uniform float		_WeightBold;

uniform float		_ScaleRatioA;
uniform float		_ScaleRatioB;
uniform float		_ScaleRatioC;

uniform float		_VertexOffsetX;
uniform float		_VertexOffsetY;

//均匀浮子 _UseClipRect；
uniform float		_MaskID;
uniform sampler2D	_MaskTex;
uniform float4		_MaskCoord;
uniform float4		_ClipRect;	// bottom left(x,y) : top right(z,w)
uniform float		_MaskSoftnessX;
uniform float		_MaskSoftnessY;

// 字体图集属性
uniform sampler2D	_MainTex;
uniform float		_TextureWidth;
uniform float		_TextureHeight;
uniform float 		_GradientScale;
uniform float		_ScaleX;
uniform float		_ScaleY;
uniform float		_PerspectiveFilter;
uniform float		_Sharpness;
