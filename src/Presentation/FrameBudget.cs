using Godot;

namespace LandBuilder.Presentation;

/// <summary>
/// One heavy job (building a tile's meshes) per frame. Work that can wait, such as the "next" tile preview or
/// re-dressing a tile after its town grew, asks first and slips to a later frame if this one is already spent, so
/// a placement never stacks three builds into one hitch.
/// </summary>
public static class FrameBudget
{
    private static ulong _spentFrame = ulong.MaxValue;

    /// <summary>Marks this frame as having done its heavy job (for work that cannot wait).</summary>
    public static void Spend() => _spentFrame = Engine.GetProcessFrames();

    /// <summary>True (and the frame is spent) if no heavy job has run this frame yet.</summary>
    public static bool TryClaim()
    {
        var frame = Engine.GetProcessFrames();
        if (_spentFrame == frame)
            return false;
        _spentFrame = frame;
        return true;
    }
}
