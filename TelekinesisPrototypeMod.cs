using Microsoft.Xna.Framework.Input;
using Terraria.ModLoader;

namespace TelekinesisPrototype;

public sealed class TelekinesisPrototypeMod : Mod
{
    public static ModKeybind ToggleRemoteControlKeybind { get; private set; }
    public static ModKeybind ToggleLevitationKeybind { get; private set; }
    public static ModKeybind ManipulateEnemyKeybind { get; private set; }

    public override void Load()
    {
        ToggleRemoteControlKeybind = KeybindLoader.RegisterKeybind(this, "ToggleRemoteControl", Keys.G);
        ToggleLevitationKeybind = KeybindLoader.RegisterKeybind(this, "ToggleLevitation", Keys.LeftShift);
        ManipulateEnemyKeybind = KeybindLoader.RegisterKeybind(this, "ManipulateEnemy", Keys.V);
    }

    public override void Unload()
    {
        ToggleRemoteControlKeybind = null;
        ToggleLevitationKeybind = null;
        ManipulateEnemyKeybind = null;
    }
}
