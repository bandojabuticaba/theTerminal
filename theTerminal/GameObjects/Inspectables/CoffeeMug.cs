using MonoGameLibrary.Graphics;

namespace theTerminal.GameObjects.Inspectables;

/// <summary>
/// The coffee mug on the desk. Placeholder object and the example to copy for
/// new ones: pick the atlas region, give the panel text, and override
/// <see cref="Inspectable.OnInspect"/> if clicking it should do more.
/// </summary>
public class CoffeeMug : Inspectable
{
    public CoffeeMug(TextureAtlas sceneAtlas, Cubicle cubicle)
        : base(sceneAtlas.GetRegion("mug"), cubicle)
    {
    }

    public override string Name => "Coffee mug";

    // A second look notices something the first didn't - an example of text
    // that changes with state.
    public override string Description => TimesInspected == 1
        ? "Cold coffee, half full. Someone left in a hurry."
        : "Cold coffee, half full. There's lipstick on the rim - and it isn't yours.";
}
