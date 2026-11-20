#ifndef PLATFORM
#define PLATFORM

// Shader models per backend. Vulkan compiles with DXC, which requires shader
// model 6 and no longer accepts the DX9-era sampler2D type or COLOR output
// semantic, so those are mapped to their modern equivalents there.
#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#elif VULKAN
    #define VS_SHADERMODEL vs_6_0
    #define PS_SHADERMODEL ps_6_0
    #define sampler2D SamplerState
    #define COLOR SV_Target
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

// On Vulkan every texture and its sampler also need an explicit, matching
// register (Texture2D X : register(t0); sampler2D XSampler : register(s0) = ...).
// MonoGame derives the texture slot from it; left implicit, the slot comes out
// negative and EffectPass.Apply throws IndexOutOfRangeException.

// Samples texture Name through its sampler, which must be named Name##Sampler.
// tex2D only takes the sampler, but SM6 needs the texture object too.
#if VULKAN
    #define SAMPLE_TEXTURE(Name, uv) Name.Sample(Name##Sampler, uv)
#else
    #define SAMPLE_TEXTURE(Name, uv) tex2D(Name##Sampler, uv)
#endif

#endif
