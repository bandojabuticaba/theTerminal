#include "platform.fxh"

Texture2D SpriteTexture : register(t0);

sampler2D SpriteTextureSampler : register(s0) = sampler_state
{
    Texture = <SpriteTexture>;
};

#include "3dEffect.fxh"

float4 MainPS(VertexShaderOutput input) : COLOR
{
    return SAMPLE_TEXTURE(SpriteTexture,input.TextureCoordinates) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
        VertexShader = compile VS_SHADERMODEL MainVS();
    }
};
