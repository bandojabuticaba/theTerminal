using theTerminal.Dialog;

namespace theTerminal.GameObjects.Inspectables;

/// <summary>
/// A computer mouse the player can inspect. In Neo's room it's the room's own
/// "mouse" layer; its text comes from the stage's inspect file, so each mouse
/// can say something different.
/// </summary>
public class ComputerMouse : Inspectable
{
    /// <param name="layer">The room layer holding its sprite.</param>
    public ComputerMouse(Cubicle cubicle, InspectText text, string layer = "mouse")
        : base(cubicle.TakeFirstFrameSprite(layer), cubicle, text)
    {
    }
}
