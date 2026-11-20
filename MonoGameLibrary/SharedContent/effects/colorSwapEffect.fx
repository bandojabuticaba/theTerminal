#include "platform.fxh"
  
// the main Sprite texture passed to SpriteBatch.Draw()  
Texture2D SpriteTexture : register(t0);  
sampler2D SpriteTextureSampler : register(s0) = sampler_state  
{  
   Texture = <SpriteTexture>;  
};  
  
#include "colors.fxh"  
  
technique SpriteDrawing  
{  
   pass P0  
   {  
      PixelShader = compile PS_SHADERMODEL ColorSwapPS();  
   }  
};
