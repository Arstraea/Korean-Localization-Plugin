using System;
using System.IO;

namespace Arstraea.KoreanPatch
{
    // Preloader는 게임 파일시스템 초기화보다 먼저 실행되므로 같은 시작 인수로
    // 사용자 자료 경로를 결정한다. 게임 설치 폴더에는 쓰지 않는다.
    //
    // Preloading precedes the game's filesystem initialization. Resolve its user-data
    // root from the same arguments, keeping all writable files outside the installation.
    internal static class PluginPaths
    {
        private static string startupDirectory;
        internal static void Configure(string gameBin) { startupDirectory = Path.GetFullPath(gameBin); }
        internal static string Root => Path.Combine(UserData(Environment.GetCommandLineArgs(), startupDirectory), "Storage", "Arstraea.KoreanPatch");

        internal static string UserData(string[] arguments, string gameDirectory = null)
        {
            string custom = CustomData(arguments, gameDirectory);
            return custom ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpaceEngineers");
        }

        private static string CustomData(string[] arguments, string gameDirectory = null)
        {
            // 현재 MyCommonProgramStartup.GetAppDataPath와 인수 우선순위를 맞춘다.
            //
            // Match the installed GetAppDataPath: -appdata wins over --appdata.
            int index = Array.IndexOf(arguments, "-appdata");
            if (index < 0) index = Array.IndexOf(arguments, "--appdata");
            if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal)) return null;
            string requested = Environment.ExpandEnvironmentVariables(arguments[index + 1]);
            // Pulsar는 Finish 이후 게임 Bin64로 작업 경로를 바꾸고 같은 인수를 전달한다.
            //
            // Pulsar changes working directory to Bin64 after Finish, before forwarding args.
            if (!Path.IsPathRooted(requested) && gameDirectory != null) requested = Path.Combine(gameDirectory, requested);
            return Path.GetFullPath(requested);
        }

        internal static string CacheFor(string content)
        {
            // 사용자 자료를 다른 PC로 복원해도 같은 캐시를 갱신한다. 설치 경로는
            // 상태 XML의 기록이며 저장 폴더를 나누는 식별자로 쓰지 않는다.
            //
            // Refresh the same cache after restoring user data on another PC. Record the
            // installation path in state XML, rather than partitioning storage by it.
            string cache = Path.Combine(Root, "Cache");
            RequireExternal(content, cache);
            return cache;
        }

        // 공급 팩의 디렉터리 배치와 무관하게 모드별 캐시 자료를 한 곳에 모은다.
        //
        // Group mod-specific cache data independently of the source pack layout.
        internal static string ModSupportFor(string cache, string mod) => Path.Combine(cache, "ModSupports", mod);

        internal static void RequireWritableRoot()
        {
            if (Fonts.FontStartup.ContentFolder != null) RequireExternal(Fonts.FontStartup.ContentFolder, Root);
        }

        internal static void RequireExternal(string content, string cache)
        {
            string game = Path.GetFullPath(Path.Combine(content, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(cache).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (target.StartsWith(game, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Plugin data/cache must be outside the game installation: " + target);
        }
    }
}
