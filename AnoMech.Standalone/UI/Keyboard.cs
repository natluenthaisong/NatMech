using System.Numerics;
using System.Runtime.InteropServices;

namespace AnoMech.Standalone.UI;

// Movement keys are polled rather than taken from KeyDown: WinForms routes arrow keys to whichever
// control has focus for navigation, and a held key's repeat rate isn't a movement speed.
internal static class Keyboard
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private static bool Down(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

    // Arena axes: +X east, +Y south (north is up on screen).
    public static Vector2 MoveDirection()
    {
        var x = (Down(Keys.D) || Down(Keys.Right) ? 1 : 0) - (Down(Keys.A) || Down(Keys.Left) ? 1 : 0);
        var y = (Down(Keys.S) || Down(Keys.Down) ? 1 : 0) - (Down(Keys.W) || Down(Keys.Up) ? 1 : 0);
        return new Vector2(x, y);
    }
}
