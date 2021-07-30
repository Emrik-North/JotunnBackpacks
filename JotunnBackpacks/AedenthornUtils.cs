/* AedenthornUtils.cs
 * 
 * From Aedenthorn's amazing mod repository. I've not touched this code, but benefited greatly from it.
 * https://github.com/aedenthorn/ValheimMods/blob/master/BackpackRedux/AedenthornUtils.cs
 * 
 */

using UnityEngine;

public class AedenthornUtils
{
    public static bool IgnoreKeyPresses(bool extra = false)
    {
        if (!extra)
            return ZNetScene.instance == null || Player.m_localPlayer == null || Minimap.IsOpen() || Console.IsVisible() || TextInput.IsVisible() || ZNet.instance.InPasswordDialog() || Chat.instance?.HasFocus() == true || Menu.IsVisible();
        return ZNetScene.instance == null || Player.m_localPlayer == null || Minimap.IsOpen() || Console.IsVisible() || TextInput.IsVisible() || ZNet.instance.InPasswordDialog() || Chat.instance?.HasFocus() == true || StoreGui.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible() || TextViewer.instance?.IsVisible() == true;
    }
    public static bool CheckKeyDown(KeyCode value)
    {
        try
        {
            return Input.GetKeyDown(value);
        }
        catch
        {
            return false;
        }
    }
    public static bool CheckKeyHeld(KeyCode value, bool req = true)
    {
        try
        {
            return Input.GetKey(value);
        }
        catch
        {
            return !req;
        }
    }
}