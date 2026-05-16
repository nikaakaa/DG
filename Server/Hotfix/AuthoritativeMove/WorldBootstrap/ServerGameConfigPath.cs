using System;
using System.IO;
using DG.GameCore;

namespace Fantasy;

public static class ServerGameConfigPath
{
    public static string FindGameCoreConfigDirectory()
    {
        return GameCoreConfigPath.FindGeneratedJsonDirectory();
    }
}
