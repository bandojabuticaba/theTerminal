namespace theTerminal.GameObjects.Inspectables;

/// <summary>
/// The mouse on Neo's desk: the first object taken from Neo's room, to try the
/// hover and details panel there. It's the room's own mouse layer.
/// </summary>
public class ComputerMouse : Inspectable
{
    public ComputerMouse(Cubicle cubicle)
        : base(cubicle.TakeFirstFrameSprite("mouse"), cubicle)
    {
    }

    public override string Name => "Mouse";

    public override string Description => TimesInspected == 1
        ? "A beige ball mouse. The cable runs behind the monitor."
        : "The ball rattles. Someone has been using this while you were asleep.";
}
