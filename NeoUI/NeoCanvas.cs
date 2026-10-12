namespace NeoGameLib.NeoUI;

/// <summary>
/// A standalone viewport root. Layout ignores inherited Position, Size, Anchor and Pivot properties and always fills the viewport from (0, 0)
/// </summary>
public class NeoCanvas : NeoUIElement
{
    private int _order;

    /// <summary>Lower orders renders first</summary>
    public int Order
    {
        get => _order;
        set
        {
            if (_order == value) return;
            _order = value;
            Bus?.InvalidateOrder();
        }
    }

    public NeoUIBus? Bus { get; private set; }
    internal long RegistrationOrder { get; set; }

    /// <summary>Registers this canvas for UI updates and drawing.</summary>
    /// <param name="bus">Defaults to the attached bus, then NeoGame.Singleton.UIBus. No-op if neither exists.</param>
    /// <param name="dedupe">When true, an existing registration is not added again. False allows repeated updates and draws.</param>
    public void Subscribe(NeoUIBus bus = null, bool dedupe = true)
    {
        (bus ?? Bus ?? NeoGame.Singleton?.UIBus)?.Add(this, dedupe);
    }

    /// <summary>Unregisters this canvas without destroying its elements or assets.</summary>
    /// <param name="bus">Defaults to the attached bus, then NeoGame.Singleton.UIBus.</param>
    /// <param name="dedupe">When true, removes all registrations. False removes one, keeping the canvas attached until the last is removed.</param>
    public void Unsubscribe(NeoUIBus bus = null, bool dedupe = true)
    {
        (bus ?? Bus ?? NeoGame.Singleton?.UIBus)?.Remove(this, dedupe);
    }

    internal void AttachToBus(NeoUIBus? bus)
    {
        Bus = bus;
        InvalidateAttachment();
    }
}
