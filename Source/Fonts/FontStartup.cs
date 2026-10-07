using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Arstraea.KoreanPatch.Fonts
{
    // 게임 시작 전 로더가 초기화한 Steam API와 실제 GameDir을 이용한다.
    // Steam 초기화나 사용자 구독 변경은 이 단계에서 수행하지 않는다.
    //
    // Use the loader's initialized Steam API and actual GameDir before game startup.
    // Do not initialize Steam again or change the user's subscriptions.
    internal static class FontStartup
    {
        internal const ulong WorkshopId = 1843839106;
        internal static string Status { get; private set; } = "Fonts: preparation pending.";
        internal static bool Failed { get; private set; }
        internal static PatchFailure Failure { get; private set; }
        internal static bool? LaunchAllowed { get; private set; }
        internal static bool HidePlatformIcons = true;
        internal static Localization.TranslationInstaller.Result TranslationResult { get; private set; }
        internal static string PackageFolder { get; private set; }
        internal static string ContentFolder { get; private set; }
        private static bool attempted;
        internal static string SettingsPath => PluginSettings.PathForSettings;

        internal static void Run()
        {
            if (attempted) return;
            // 격리 검사나 다른 호스트에서는 게임 파일에 접근하지 않는다.
            // An isolated test or unsupported host must not touch game files.
            Assembly pulsar = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Pulsar.Shared");
            if (pulsar == null) { Status = "Fonts: preparation unavailable; Pulsar unavailable."; return; }
            PatchStage stage = PatchStage.Paths;
            PatchSession ui = null;
            try
            {
                Type configType = pulsar.GetType("Pulsar.Shared.Config.ConfigManager", true);
                object config = configType.GetProperty("Instance").GetValue(null);
                if (config == null) { Status = "Fonts: preparation unavailable; Pulsar configuration not initialized."; return; }
                attempted = true;
                ui = new PatchSession();
                string bin = (string)configType.GetProperty("GameDir").GetValue(config);
                string content = GetContentPath(bin);
                ContentFolder = content;
                LoadSettings();
                stage = PatchStage.Workshop;
                ui.Publish(PatchView.Working(stage));
                Type steam = pulsar.GetType("Pulsar.Shared.Steam", true);
                if (!(bool)steam.GetProperty("IsInitialized").GetValue(null))
                    throw new PatchPreparationException("Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam을 실행하고 로그인한 뒤 게임과 Pulsar를 다시 실행해 주세요.");
                Assembly steamworks = Assembly.Load("Steamworks.NET");
                FontInstaller.Result result;
                using (var source = new SteamWorkshopSource(steamworks))
                    result = PatchPreparation.Run(source, ui, (package, recheck, progress) =>
                    {
                        TranslationResult = null;
                        var fonts = FontInstaller.Apply(content, package.Folder, HidePlatformIcons, recheck, progress: progress);
                        TranslationResult = Localization.TranslationInstaller.Apply(content, package.Folder,
                            Localization.TranslationState.PathFor(content), recheck, progress);
                        PackageFolder = package.Folder;
                        return fonts;
                    },
                        () => ui.Elapsed, ui.Wait);
                FontModCompatibility.Install(content);
                Status = "Fonts: ready; XML changed=" + result.ChangedXml + ", DDS copied=" + result.CopiedImages
                    + ", Hangul glyphs=" + result.HangulCount + ", platform icons hidden=" + HidePlatformIcons + ".";
                if (result.OriginalIconsUnavailable) Status += " Original icon metrics were already missing; verify game files before showing icons.";
            }
            catch (Exception error)
            {
                while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                Failed = true;
                if (ui != null) stage = ui.View.Stage;
                Failure = PatchFailure.FromException(stage, error);
                // 한국어 해결 안내는 매니저에 유지하고 진단 로그에는 단계와 원본 예외만 기록한다.
                //
                // Keep Korean guidance in the manager; log the stage and original exception for diagnostics.
                Status = "Fonts: preparation failed; stage=" + Failure.Stage + "; error=" + Failure.Details;
            }
            finally
            {
                if (ui != null)
                {
                    try { LaunchAllowed = ui.Resolve(Failure); }
                    finally
                    {
                        ui.Dispose();
                        if (ui.UiError != null) PatchStartupGate.WriteLog("Preparation UI failed: " + ui.UiError);
                        // 마지막 파일 처리·UI 종료와 경합한 종료 요청도 놓치지 않는다.
                        // Do not lose an exit request racing the last file operation/UI shutdown.
                        if (ui.ExitRequested) LaunchAllowed = false;
                    }
                }
            }
        }

        internal static string GetContentPath(string bin)
        {
            if (string.IsNullOrWhiteSpace(bin) || !File.Exists(Path.Combine(bin, "VRage.dll")))
                throw new DirectoryNotFoundException("Pulsar game Bin64 path is invalid.");
            string content = Path.GetFullPath(Path.Combine(bin, "..", "Content"));
            if (!File.Exists(Path.Combine(content, "Fonts", "white", "FontDataPA.xml")))
                throw new DirectoryNotFoundException("Game content path is invalid.");
            return content;
        }
        private static void LoadSettings()
        {
            PluginSettings.Load();
            HidePlatformIcons = PluginSettings.Current.HidePlatformIcons;
            Input.ImeControlPolicy.Policy.Configure(PluginSettings.Current.InputLanguage);
        }
        internal static void MarkContinued()
        {
            Status += "\nFonts: user continued after preparation failure; retry on next launch.";
        }
    }
}
