using System.Collections.Generic;

namespace ATFTools.Core;

public static class PlayerNames
{
    private static Dictionary<ulong, string> _names = new();

    public static void Initialize()
    {
        _names.Add(76561198119031431, "Solar | Cheetah 2-2");
    }

    public static string GetName(ulong steamID)
    {
        return _names.GetValueOrDefault(steamID, "");
    }
}