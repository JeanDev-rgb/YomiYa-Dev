using System;

namespace YomiYa.Core.Common;

public static class KamojiHelper
{
    private static readonly string[] KamojiList =
    [
        "(╥﹏╥)", "(╯︵╰,)", "( -_- )", "(´；д；`)",
        "( T⌓T)", "(◞‸◟)", "(｡•́ ︿ •̀｡)", "(｡•́‿•̀｡)", "(｡•́︿•̀｡)",
        "(o´ω`o)", "(>_<)", "(>︿<)", "(￣ω￣)", "(；一_一)",
        "(´・ω・`)", "(ノ_<。)", "ʕ•́ᴥ•̀ʔっ"
    ];

    public static string GetRandomKamoji()
    {
        var random = new Random();
        return KamojiList[random.Next(KamojiList.Length)];
    }
}