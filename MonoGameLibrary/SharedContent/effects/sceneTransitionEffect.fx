#include "platform.fxh"

float Progress;
float EdgeWidth;

Texture2D SpriteTexture : register(t0);

sampler2D SpriteTextureSampler : register(s0) = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

float4 MainPS(VertexShaderOutput input) : COLOR
{
    float2 uv = input.TextureCoordinates;  
    float value = SAMPLE_TEXTURE(SpriteTexture, uv).r;  
    float transitioned = smoothstep(Progress, Progress + EdgeWidth, value);  
    return float4(0, 0, 0, transitioned);
    //return SAMPLE_TEXTURE(SpriteTexture, input.TextureCoordinates) * input.Color;
}

technique SpriteDrawing
{
    pass P0
    {
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
};
