#if OPENGL
   #define SV_POSITION POSITION
   #define VS_SHADERMODEL vs_3_0
   #define PS_SHADERMODEL ps_3_0
#else
   #define VS_SHADERMODEL vs_4_0_level_9_1
   #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// Based on https://github.com/educrod/shaders/blob/master/Content/Pixelation.fx,
// but working in the sprite's texture space instead of a fixed screen size, so
// it pixelates a single sprite at whatever scale and position it is drawn.

// the main Sprite texture passed to SpriteBatch.Draw()
Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
   Texture = <SpriteTexture>;
};

// Size of SpriteTexture in pixels.
float2 TextureSize;

// Size of one block, in texture pixels. 1 samples each texel at its center
// (crisp, like point sampling); 4 merges every 4x4 texels into one block.
float PixelSize;

float4 PixelatePS(float4 position : SV_POSITION, float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
   // Snap to the center of the block this pixel falls in. floor + 0.5 rather
   // than round, so every block samples inside itself and never its neighbor.
   float2 texel = uv * TextureSize;
   float2 block = (floor(texel / PixelSize) + 0.5) * PixelSize;

   return tex2D(SpriteTextureSampler, block / TextureSize) * color;
}

technique SpriteDrawing
{
   pass P0
   {
      PixelShader = compile PS_SHADERMODEL PixelatePS();
   }
};
