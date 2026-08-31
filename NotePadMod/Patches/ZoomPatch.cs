using NotePadMod.UI;

namespace NotePadMod.Patches;

public static class ZoomPatch
{
    public static bool CanZoomPatch(ref bool __result)
    {
        if (NotePadWindow.IsOpen)
        {
            __result = false;
            return false;
        }
        return true;
    }
}
