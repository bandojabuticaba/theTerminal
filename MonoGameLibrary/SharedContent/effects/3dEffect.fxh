#ifndef EFFECT_3DEFFECT
#define EFFECT_3DEFFECT
#include "common.fxh"

float4x4 MatrixTransform;

// Not used here, but shadowHullEffect (which includes this file) relies on it.
float2 ScreenSize;

// The point the sprite turns and lifts around, in the same space as the
// sprite's vertices (screen pixels). Usually the center of what's being drawn.
float2 RotationCenter;

// Turn around the vertical (left/right) and horizontal (up/down) axes, from
// -1 to 1. TurnIntensity says how far 1 goes, in turns: 0.08 = ~29 degrees.
// Effects that never set these (point lights, shadow hulls) don't turn.
float SpinAmount;
float TiltAmount;
float TurnIntensity;

// How lifted toward the viewer the sprite is, from 0 to 1. LiftIntensity is
// how much bigger fully lifted is: 0.15 = 15%.
float Lift;
float LiftIntensity;

VertexShaderOutput MainVS(VertexShaderInput input) 
{
    VertexShaderOutput output;

    float4 pos = input.Position;

    // shaders use radians for angles, so 2 pi = 360 degrees
    float spin = SpinAmount * TurnIntensity * 6.28;
    float tilt = TiltAmount * TurnIntensity * 6.28;

    // shift the position to the center of rotation (the sprite lies on z = 0)
    pos.xy -= RotationCenter;

    // lift: grow around the center
    pos.xy *= 1 + Lift * LiftIntensity;

    // spin: rotate in the xz plane, around the vertical axis
    float spunX = pos.x * cos(spin) - pos.z * sin(spin);
    float spunZ = pos.x * sin(spin) + pos.z * cos(spin);
    pos.x = spunX;
    pos.z = spunZ;

    // tilt: rotate in the yz plane, around the horizontal axis
    float tiltedY = pos.y * cos(tilt) - pos.z * sin(tilt);
    float tiltedZ = pos.y * sin(tilt) + pos.z * cos(tilt);
    pos.y = tiltedY;
    pos.z = tiltedZ;

    // shift the position away from the center of rotation
    pos.xy += RotationCenter;
    
    output.Position = mul(pos, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinates = input.TexCoord;
    return output;
}
#endif
