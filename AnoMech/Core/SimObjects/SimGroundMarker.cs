using System;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Native.Interfaces;

namespace AnoMech.Core.SimObjects;

// An on-floor guide drawn with one of the game's own omen files rather than an overlay, so it sits
// in the scene like a real telegraph. Kept by its owner; a Place somewhere new respawns the VFX,
// since a world VFX moved after spawning either restarts every frame or doesn't move at all. Hide
// frees the native VFX until the next Place. Scenario-local, like the rest of the SimXxx API.
public sealed class SimGroundMarker : ISimObject
{
    // Below this a re-place is skipped: no visible change, no respawn.
    private const float MoveEpsilon = 0.02f;

    private readonly Coordinates coordinates;
    private readonly string path;
    private IStaticVfxProxy? vfx;
    private Placement placed;
    private Vector3 scale;
    private Vector4 color = Vector4.One;
    private bool despawned;

    internal SimGroundMarker(Coordinates coordinates, string path)
    {
        this.coordinates = coordinates;
        this.path = path;
    }

    public bool IsActive => !despawned;
    public bool IsShown => vfx != null;

    public void Place(Vector3 position, float rotation, Vector3 newScale)
    {
        if (despawned) return;
        var placement = new Placement(position, rotation);
        if (vfx is not null
            && Vector3.Distance(placed.Position, position) <= MoveEpsilon
            && MathF.Abs(placed.Rotation - rotation) <= MoveEpsilon
            && Vector3.Distance(scale, newScale) <= MoveEpsilon)
            return;
        vfx?.Remove();
        vfx = Natives.Vfx.SpawnStatic(path, coordinates.ToGlobal(placement), newScale);
        if (color != Vector4.One) vfx?.SetColor(color);
        placed = placement;
        scale = newScale;
    }

    public void Tint(Vector4 newColor)
    {
        if (newColor == color) return;
        color = newColor;
        vfx?.SetColor(newColor);
    }

    public void Hide()
    {
        vfx?.Remove();
        vfx = null;
    }

    public void Tick(float deltaSeconds) { }

    public void Despawn()
    {
        Hide();
        despawned = true;
    }
}
