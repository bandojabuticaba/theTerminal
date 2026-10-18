using System;

namespace MonoGameLibrary;

/// <summary>
/// Easing functions from https://easings.net. Each one maps a normalized time
/// in [0, 1] to a progress value, with f(0) = 0 and f(1) = 1. Back ones
/// overshoot past 1 before settling.
/// </summary>
/// <remarks>
/// Rule of thumb for picking one: motion the player triggers (a click) should
/// use an Out curve - it responds instantly and settles softly. InOut curves
/// start slowly, which reads as input lag on a click, and suit motion the game
/// starts on its own. Higher powers (Quad, Cubic, Quart, Quint, Expo) front-load
/// more of the motion and leave a longer, gentler settle.
/// </remarks>
public static class Easing
{
    public static float Linear(float x) => x;

    public static float InOutSine(float x) => -(MathF.Cos(MathF.PI * x) - 1f) / 2f;

    public static float OutQuad(float x) => 1f - (1f - x) * (1f - x);

    public static float OutCubic(float x) => 1f - MathF.Pow(1f - x, 3f);

    public static float OutQuart(float x) => 1f - MathF.Pow(1f - x, 4f);

    public static float OutQuint(float x) => 1f - MathF.Pow(1f - x, 5f);

    public static float OutExpo(float x) => x >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * x);

    public static float InOutCubic(float x) =>
        x < 0.5f ? 4f * x * x * x : 1f - MathF.Pow(-2f * x + 2f, 3f) / 2f;

    public static float OutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * MathF.Pow(x - 1f, 3f) + c1 * MathF.Pow(x - 1f, 2f);
    }
}
