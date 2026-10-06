using System.Runtime.CompilerServices;
using Arstraea.KoreanPatch.Input;
using VRage.Plugins;
using VRage.Utils;

namespace Arstraea.KoreanPatch
{
    public sealed class Plugin : IPlugin
    {
        internal const string DisplayName = "Korean Localization Plugin";
        internal const string DisplayTitle = DisplayName + " - 한글화 플러그인 by Arstraea";
        private bool clientInitialized;

        // Init은 창 생성 이후다. 여기서 초기 패치를 뒤늦게 재시도하지 않는다.
        //
        // Init runs after window creation; do not attempt a late bootstrap here.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Init(object gameInstance)
        {
            if (!ClientHost.CanRunClient) return;
            InitClient();
            clientInitialized = true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InitClient()
        {
            Localization.LanguageMenu.Install();
            Mods.TextHudFontSupport.Install();
            Mods.RichHudDefaultFont.Install();
            ImeControlPolicy.InstallSessionHooks();
            Gui.RichTextSpacing.Install();
            Gui.ScriptEditorNotice.Install();
            MyLog.Default.WriteLine("[Arstraea.KoreanPatch] " + ImeBootstrap.Status);
            MyLog.Default.WriteLine("[Arstraea.KoreanPatch] " + Fonts.FontStartup.Status);
            MyLog.Default.WriteLine("[Arstraea.KoreanPatch] " + Fonts.FontModCompatibility.Status);
            MyLog.Default.WriteLine("[Arstraea.KoreanPatch] " + Localization.LanguageSupport.Status);
            MyLog.Default.WriteLine("[Arstraea.KoreanPatch] Input hooks: Unicode pump=" + UnicodeMessagePump.Installed
                + "; detailed tracing=" + ImeDiagnostics.Enabled);
            if (ImeBootstrap.CompositionHookInstalled) ImeStartupPreparation.Arm();
            if (!ImeBootstrap.WindowInitialized)
                MyLog.Default.WriteLine("[Arstraea.KoreanPatch] IME window initialization was not observed. Enable this plugin in Pulsar and restart. Font installation is separate.");
        }

        public void Update()
        {
            if (!clientInitialized) return;
            UpdateClient();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UpdateClient()
        {
            ImeStartupPreparation.UpdateReadiness();
            ImeBootstrap.UpdateDiagnostics();
            ImeDiagnostics.Flush(line => MyLog.Default.WriteLine(line));
        }

        public void Dispose()
        {
            // 창 수명 중 패치를 해제하면 기본/플러그인 조합 상태가 섞인다. 변경은 재시작으로 적용한다.
            //
            // Keep hooks for the window lifetime; enabling/disabling requires a restart.
        }

        // 폰트 설정은 다음 실행의 파일 준비에 적용한다.
        //
        // Font preferences are applied during file preparation on the next launch.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OpenConfigDialog()
        {
            if (!clientInitialized || !ClientHost.CanRunClient) return;
            OpenClientConfig();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpenClientConfig() { Gui.SettingsScreen.Open(); }
    }
}
