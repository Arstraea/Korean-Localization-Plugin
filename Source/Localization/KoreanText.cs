namespace Arstraea.KoreanPatch.Localization
{
    // 플러그인이 표시하는 한국어 문구의 단일 편집 위치다. 영어 주석은 검토자를 위한
    // 번역이며 영어 UI를 제공하는 런타임 리소스가 아니다. {0} 등은 실행 중 채워진다.
    //
    // Edit the plugin's Korean UI text here. English comments are reviewer translations,
    // not runtime English resources. Numbered placeholders are filled by callers.
    internal static class KoreanText
    {
        // 공통 / Common

        // Korean Localization Plugin - Korean localization plugin by Arstraea
        internal const string PluginTitle = Plugin.DisplayName + " - 한글화 플러그인 by Arstraea";

        // Korean by Arstraea
        internal const string LanguageName = "한국어 by Arstraea";

        // Copy details
        internal const string CopyDetails = "내역 복사";

        // Error details
        internal const string ErrorDetails = "오류 상세";

        // Confirm
        internal const string Confirm = "확인";

        // The original is being displayed as translated text.
        internal const string DatapadTranslationNotice = "원문을 번역된 문장으로 표시 중입니다.";

        // Save datapad changes
        internal const string DatapadSaveTitle = "저장 확인";

        // This datapad uses localization keys to display text in the reader's selected language.
        // Saving changes prevents the edited fields from adapting to other readers' languages.
        internal const string DatapadSaveExplanation = "이 데이터패드는 텍스트가 아닌 번역 키가 저장되어 있어, 열람한 사람의 사용 언어에 맞춰 표시되고 있습니다.\n"
            + "내용을 수정하고 저장하면, 번역 키가 사라지고 수정된 내용 자체가 저장되므로 다른 사용자의 언어에 맞춰 표시되지 않게 됩니다.";

        // Korean characters may appear corrupted in environments without Korean display support.
        internal const string DatapadSaveWarning = "한글이 포함된 경우, 한글을 사용하지 않는 유저가 열람 시 글자가 깨져 보이게 됩니다.\n(외국인이거나, 한글화를 적용 중이지 않은 사람 등)";

        // Do you want to save?
        internal const string DatapadSaveQuestion = "저장하시겠습니까?";

        // 설정 / Settings

        // Korean input support settings
        internal const string InputSettingsHeading = "한글 입력 지원 관련 설정";

        // How to remember Korean/English input state
        internal const string InputLanguageLabel = "한/영 입력 상태 기억 방식";

        // Choose how to remember the Korean/English state used when opening an input field.
        // This works independently of the game's UI language.
        // * The first input without a remembered state is treated as English.
        // * States are temporary for this game run and reset when entering/leaving a world.
        // * Changes apply from the next entry into an input field.
        internal const string InputLanguageTooltip = "입력창을 열 때 사용할 한/영 상태를 기억할 방식을 선택하세요.\n"
            + "게임에서 사용하는 UI 표시 언어와 별개로 작동합니다.\n"
            + "* 기억한 상태가 없는 최초 입력은 영어 상태로 간주합니다.\n"
            + "* 상태 기억은 게임 단위로 임시 저장되어 월드 입장과 퇴장 시 초기화됩니다.\n"
            + "* 옵션 변경 시 바로 다음 입력창 진입부터 적용됩니다.";

        // Remember the previous Korean/English input state
        internal const string RememberPreviousInput = "이전 한/영 입력 상태 기억";

        // Remember Korean/English input state for each input field
        internal const string RememberEachInput = "입력창별 한/영 입력 상태 기억";

        // Remember Korean/English state in chat | Always start in English elsewhere
        internal const string EnglishExceptChat = "채팅창에선 한/영 입력 상태 기억  |  그 외엔 항상 영어로 입력 시작";

        // Always start input in English everywhere
        internal const string AlwaysEnglish = "모든 곳에서 항상 영어로 입력 시작";

        // Ignore Korean input in the script code editor
        internal const string ScriptEditorInputLabel = "스크립트 코드 편집기에서 한글 입력 무시";

        // Prevent Korean typing in the script code editor, as in the original game.
        // * Pasting Korean text remains possible, as before.
        // * This helps prevent Korean input from damaging a script,
        //   and applies only to the script code editor.
        internal const string ScriptEditorInputTooltip = "스크립트 코드 편집기에서는 기존 게임처럼 한글 입력이 되지 않도록 막습니다.\n"
            + "* 기존처럼 붙여넣기로 넣는 한글 입력은 가능합니다.\n"
            + "* 한글 입력으로 스크립트가 망가지는 걸 방지하기 위한 기능으로,\n"
            + "  오직 스크립트 코드 편집기에서만 적용되는 기능입니다.";

        // Font settings
        internal const string FontSettingsHeading = "폰트 설정";

        // Hide platform icons (restart required)
        internal const string HidePlatformIconsLabel = "플랫폼 아이콘 숨기기 (재시작 필요)";

        // Hide platform icons shown next to names in chat and similar places.
        // * Restart the game after changing this setting for it to take effect.
        internal const string HidePlatformIconsTooltip = "채팅 등에서 이름 옆에 표시되는 플랫폼 아이콘을 숨깁니다.\n* 변경 후 게임을 재시작해야 적용됩니다.";

        // Logs for this run
        internal const string RunLogsHeading = "이번 실행 관련 로그";

        // Save
        internal const string Save = "저장";

        // Cancel
        internal const string Cancel = "취소";

        // Failed to save settings. {0}: error message.
        internal const string SettingsSaveFailed = "설정 저장에 실패했습니다.\n\n{0}";

        // Logs copied.
        internal const string LogsCopied = "로그를 복사했습니다.";

        // Failed to copy logs. Please try again.
        internal const string LogsCopyFailed = "로그 복사에 실패했습니다. 다시 시도해 주세요.";

        // Korean typing in this editor is restricted by the localization plugin settings.
        internal const string ScriptEditorNotice = "한글화 플러그인 설정으로 인해 편집기 내에서의 한글 입력이 제한됩니다.";

        // 시작 전 7단계 / Seven preparation stages

        // Check the game installation and Workshop localization pack download paths
        internal const string StagePaths = "게임 설치 경로와 창작마당 한글화 팩 다운로드 위치 확인";

        // Check whether the Workshop localization pack is subscribed and fully downloaded
        internal const string StageWorkshop = "창작마당 한글화 팩 구독 및 다운로드 완료 여부 확인";

        // Check font/translation data and the cache folder
        internal const string StageAccessAndRecovery = "폰트·번역 자료와 캐시 폴더 확인";

        // Prepare fonts and check translation file readability
        internal const string StagePrepare = "폰트 준비 및 번역 파일 읽기 확인";

        // Perform a final data check before applying
        internal const string StageRecheck = "적용 전 최종 자료 확인";

        // Prepare and apply the font/translation cache
        internal const string StageApply = "폰트·번역 캐시 준비 및 적용";

        // Finish
        internal const string StageLaunch = "마무리";

        // 매니저 / Pre-game manager

        // Prepare localization pack data before starting the game.
        internal const string PreparationIntro = "게임을 시작하기 전에 한글화 팩 자료를 준비합니다.";

        // Patch stage where the error occurred
        internal const string FailedStageHeading = "오류가 발생한 패치 단계";

        // Patch stage currently in progress
        internal const string CurrentStageHeading = "현재 진행 중인 패치 단계";

        // Preparation status
        internal const string PreparationStatusHeading = "준비 상태";

        // Check again
        internal const string CheckAgain = "다시 확인";

        // Open the localization pack Workshop page
        internal const string OpenWorkshop = "한글화 팩 창작마당 열기";

        // Ignore and run
        internal const string IgnoreAndRun = "무시하고 실행";

        // Exit
        internal const string Exit = "종료";

        // Start the game now
        internal const string StartGameNow = "지금 게임 시작";

        // Stop waiting
        internal const string StopWaiting = "대기 중단";

        // Patch stage and error details copied together.
        internal const string FailureReportCopied = "패치 단계와 오류 상세를 함께 복사했습니다.";

        // Copy failed. Please press [Copy details] again in a moment.
        internal const string FailureReportCopyFailed = "복사하지 못했습니다. 잠시 후 [내역 복사]를 다시 눌러 주세요.";

        // A request to open the Workshop page was sent.
        internal const string WorkshopOpenRequested = "창작마당 페이지를 여는 요청을 보냈습니다.";

        // Could not open the page. Find Korean Localization Pack (1843839106) on Steam Workshop.
        internal const string WorkshopOpenFailed = "페이지를 열지 못했습니다. Steam 창작마당에서 Korean Localization Pack 한글화 팩(1843839106)을 찾아 주세요.";

        // Ignore the error and start the game?
        internal const string IgnoreErrorQuestion = "오류를 무시하고 게임을 실행할까요?";

        // Preparation is complete.
        internal const string PreparationCompleteTitle = "준비가 완료되었습니다.";

        // Continue automatically when preparation is complete.
        internal const string ContinueAfterPreparation = "준비가 완료되면 자동으로 계속 진행합니다.";

        // The patch may be incomplete or may not work correctly.
        // This choice applies only to this run.
        internal const string IgnoreErrorWarning = "패치가 일부 적용되지 않거나 정상적으로 동작하지 않을 수 있습니다.\n이 선택은 이번 실행에만 적용됩니다.";

        // The game will start when patching is complete.
        // Choosing Exit does not cancel the Steam download itself.
        internal const string PreparationExitHint = "패치가 완료되면 게임이 실행됩니다.\n종료를 선택해도 Steam 다운로드 자체는 취소하지 않습니다.";

        // Stop requested. Please wait for the current application/recovery operation to finish.
        internal const string StopRequested = "중단 요청을 받았습니다. 진행 중인 적용·복구가 끝날 때까지 기다려 주세요.";

        // This window will close automatically and the game will start in {0} seconds.
        internal const string LaunchCountdown = "{0}초 후 이 창이 자동으로 닫히고 게임이 실행됩니다.";

        // Checking status again...
        internal const string CheckingStatus = "상태를 다시 확인하는 중…";

        // Recheck download status in {0} seconds
        internal const string DownloadCountdown = "{0}초 후 다운로드 상태 재확인";

        // Recheck subscription status in {0} seconds
        internal const string SubscriptionCountdown = "{0}초 후 구독 여부 재확인";

        // Preparing the patch.
        internal const string PreparingPatch = "패치를 준비하고 있습니다.";

        // Proceed to the next stage automatically when the current operation finishes.
        // Pressing Exit waits for application/recovery to finish, then ends this game launch.
        internal const string WorkingGuidance = "현재 작업이 끝나면 자동으로 다음 단계로 진행합니다.\n\n종료를 누르면 적용·복구 중인 작업을 마친 뒤 게임 실행을 종료합니다.";

        // You need to subscribe to the localization pack on Steam Workshop.
        internal const string SubscriptionRequired = "Steam 창작마당에서 한글화 팩 구독이 필요합니다.";

        // Updating the localization pack to the latest data.
        internal const string UpdatingPack = "한글화 팩을 최신 자료로 업데이트하고 있습니다.";

        // Waiting for Steam to finish downloading.
        internal const string WaitingForDownload = "Steam 다운로드가 완료되기를 기다리고 있습니다.";

        // Open the localization pack Workshop page below and press [Subscribe].
        // Subscription is checked automatically; patching starts when the download is complete.
        internal const string SubscriptionGuidance = "아래 버튼으로 한글화 팩 창작마당 페이지를 열어 [구독]을 눌러 주세요.\n\n구독 여부를 자동으로 확인하고 다운로드가 완료되면 한글패치 적용을 시작합니다.";

        // The localization pack download status is checked automatically.
        // If it does not progress, check the Steam download list and your internet connection.
        internal const string DownloadGuidance = "Steam의 한글화 팩 다운로드 상태를 자동으로 확인하고 있습니다.\n\n진행되지 않으면 Steam 다운로드 목록과 인터넷 연결을 확인해 주세요.";

        // Wait up to 10 minutes for the localization pack download.
        internal const string DownloadWaitLimit = "\n\n한글화 팩 다운로드는 최대 10분 동안 기다립니다.";

        // Korean patch preparation did not finish successfully.
        internal const string PreparationFailed = "한글패치 준비가 정상적으로 완료되지 않았습니다.";

        // Korean patch preparation is complete.
        internal const string PreparationComplete = "한글패치 준비가 완료되었습니다.";

        // Required data checks and font/translation preparation finished without problems.
        internal const string PreparationCompleteGuidance = "필요한 자료 확인과 폰트·번역 준비가 문제 없이 완료되었습니다.";

        // Patch operation progress
        internal const string ProgressAccessibleName = "패치 작업 진행";

        // Checking progress
        internal const string ProgressUnknown = "진행률 확인 중";

        // 준비 실패 / Preparation failures

        // {0}: plugin name, {1}: patch stage, {2}: original error details.
        // Localization plugin application error / Patch stage where the error occurred / Error details
        internal const string FailureReport = "{0} — 한글화 플러그인 적용 오류\r\n\r\n오류가 발생한 패치 단계\r\n{1}\r\n\r\n오류 상세\r\n{2}";

        // File access permissions are insufficient.
        // Check write permission for the game user-data folder and read permission for the pack.
        // If using a separate data folder, also check the -appdata launch argument.
        internal const string FileAccessDenied = "파일 접근 권한이 부족합니다.\n\n게임 사용자 데이터 폴더의 쓰기 권한과 한글화 팩 자료의 읽기 권한을 확인해 주세요. 별도 데이터 폴더를 사용한다면 실행 인수의 -appdata 경로도 확인해 주세요.";

        // A file path is too long to process. Check the failing path in the error details.
        // Use Steam storage settings for game/pack paths or -appdata for user-data paths,
        // then retry with a shorter path.
        internal const string FilePathTooLong = "파일 경로가 너무 길어 처리하지 못했습니다.\n\n오류 내역에서 문제가 된 경로를 확인해 주세요. 게임·한글화 팩 경로는 Steam의 저장 공간 설정을, 게임 사용자 데이터 경로는 실행 인수의 -appdata 설정을 확인하고 더 짧은 경로를 사용한 뒤 다시 실행해 주세요.";

        // A required file or folder was not found.
        // Verify game integrity and completion of the Workshop pack download in Steam, then retry.
        internal const string RequiredFilesMissing = "필요한 파일이나 폴더를 찾지 못했습니다.\n\nSteam에서 게임 무결성 검사와 창작마당 한글화 팩 다운로드 완료를 확인한 뒤 다시 실행해 주세요.";

        // Font/translation data could not be read or is incompatible with the current game font.
        // Check that Steam downloading is complete and contact the author on the pack's Workshop page.
        internal const string InvalidFontOrTranslation = "폰트·번역 자료를 읽지 못했거나 현재 게임 폰트와 호환되지 않습니다.\n\nSteam 다운로드가 완료됐는지 확인하고, 한글화 팩 창작마당 페이지에서 제작자에게 문의하세요.";

        // Another program is using files required by the patch, so the operation could not proceed.
        // Close other game or Pulsar instances and retry.
        internal const string FileInUse = "한글패치 적용에 필요한 파일을 다른 프로그램에서 사용하고 있어 작업을 진행하지 못했습니다.\n\n다른 게임 또는 Pulsar 실행을 모두 종료한 뒤 다시 시도해 주세요.";

        // Could not read files or prepare the cache.
        // Close other game or Pulsar instances and retry.
        internal const string CachePreparationFailed = "파일을 읽거나 캐시를 준비하지 못했습니다.\n\n다른 게임 또는 Pulsar 실행을 모두 종료한 뒤 다시 시도해 주세요.";

        // An error occurred during Korean patching.
        // Close the game and Pulsar, then restart.
        internal const string UnexpectedPatchError = "한글패치 작업 중 오류가 발생했습니다.\n\n게임과 Pulsar를 종료한 뒤 다시 실행해 주세요.";

        // If the error repeats, press [Copy details] and send the copied information to the author
        // on the localization pack Workshop page.
        internal const string FailureContactHint = "\n\n오류가 반복되면 [내역 복사]를 눌러 복사한 내용을 한글화 팩 창작마당 페이지에서 제작자에게 알려 주세요.";

        // Another instance is preparing Korean patch fonts for this game.
        // Close the other game or Pulsar instance and retry.
        internal const string PreparationBusy = "다른 실행에서 같은 게임의 한글패치 폰트를 준비하고 있습니다.\n\n다른 게임 또는 Pulsar 실행을 종료한 뒤 다시 시도해 주세요.";

        // The localization pack download did not finish after a long wait.
        // Check the Steam subscription/download status and try again later.
        internal const string DownloadTimedOut = "한글화 팩 자료 다운로드를 오래 기다렸지만 완료되지 않았습니다.\n\nSteam에서 구독·다운로드 상태를 확인하고 잠시 후 다시 실행해 주세요.";

        // The pack data changed repeatedly during preparation, so application was stopped.
        // Retry after Steam updates finish.
        internal const string PackChangedRepeatedly = "패치 준비 중 한글화 팩 자료가 반복해서 바뀌어 적용을 중단했습니다. Steam 업데이트가 완료된 뒤 다시 실행해 주세요.";

        // The localization pack data was updated; checking the latest state again.
        internal const string PackChanged = "한글화 팩 자료가 갱신되어 최신 상태를 다시 확인합니다.";

        // The user stopped waiting for the localization pack download.
        // Complete the Steam subscription/download and retry.
        internal const string DownloadWaitStopped = "사용자가 한글화 팩 다운로드 대기를 중단했습니다.\n\nSteam에서 구독·다운로드를 완료한 뒤 다시 실행해 주세요.";

        // Steam connection status could not be checked, so waiting for the pack download cannot continue.
        // Start Steam, sign in, and restart the game and Pulsar.
        internal const string SteamNotInitialized = "Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam을 실행하고 로그인한 뒤 게임과 Pulsar를 다시 실행해 주세요.";

        // Steam did not finish the pack download. Check download status and internet connectivity, then retry.
        // Steam result code: {0}
        internal const string SteamDownloadFailed = "Steam이 한글화 팩 다운로드를 완료하지 못했습니다. Steam 다운로드 상태와 인터넷 연결을 확인한 뒤 다시 실행해 주세요.\nSteam 결과 코드: {0}";

        // Steam could not determine the pack's download location. Check download completion and retry.
        internal const string SteamDownloadLocationMissing = "Steam에서 한글화 팩의 다운로드 위치를 확인하지 못했습니다. 다운로드 완료를 확인한 뒤 다시 실행해 주세요.";

        // Steam connection status could not be checked, so waiting for the pack download cannot continue.
        // Check Steam running/sign-in status and restart the game and Pulsar.
        internal const string SteamConnectionCheckFailed = "Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam 실행·로그인 상태를 확인하고 게임과 Pulsar를 다시 실행해 주세요.";

        // Steam connection status could not be checked, so waiting for the pack download cannot continue.
        // Switch Steam to online mode if offline, check sign-in/connectivity, then restart the game and Pulsar.
        internal const string SteamOffline = "Steam 연결 상태를 확인할 수 없어 한글화 팩 다운로드 대기를 계속할 수 없습니다.\n\nSteam이 오프라인 모드라면 온라인으로 전환하고, 로그인과 인터넷 연결을 확인한 뒤 게임과 Pulsar를 다시 실행해 주세요.";

        // Steam did not accept the pack download request. Check sign-in/download status and retry.
        internal const string SteamDownloadRequestFailed = "Steam이 한글화 팩 다운로드 요청을 받지 못했습니다. Steam 로그인과 다운로드 상태를 확인한 뒤 다시 실행해 주세요.";

        // 뉴스 UI / News UI

        // Localization pack update information
        internal const string PackNewsHeading = "한글화 팩 업데이트 정보";

        // Plugin update information
        internal const string PluginNewsHeading = "플러그인 업데이트 정보";

        // Show localization news
        internal const string ShowLocalizationNews = "한글화 소식 보기";

        // Show game news
        internal const string ShowGameNews = "게임 소식 보기";

        // Version: {0}
        internal const string NewsVersion = "버전: {0}";

        // Could not load update notes.
        internal const string NewsUnavailable = "업데이트 내역을 불러올 수 없습니다.";

        // No update notes are registered.
        internal const string NewsEmpty = "등록된 업데이트 내역이 없습니다.";
    }

    #region GeneratedPluginNews
    // 자동 생성: 프로젝트 폴더 기준 Assets/PluginVersion.xml에서 뉴스와 영어 번역 주석을 수정한다.
    //
    // Generated: edit news and English translation comments in Assets/PluginVersion.xml, relative to the project directory.
    internal static class PluginNews
    {
        // Select "Korean" as the language in the game settings!
        // Korean text input + options for remembering Korean/English input state
        // Automatically add Korean fonts to TextHudAPI, RichHudMaster, and Build Info
        // Add translated datapad display and original-content protection
        internal const string Xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<KoreanPatchRelease schemaVersion=""1"">
  <Version>1.1.0</Version>
  <PublishedAt>2026-11-17</PublishedAt>
  <!-- Select ""Korean"" as the language in the game settings! -->
  <Introduction>게임 설정에서 언어를 ""한국어""로 변경하세요!</Introduction>
  <Changes>
    <!-- Korean text input + options for remembering Korean/English input state -->
    <Change>한글 입력 지원 + 한/영 입력 상태 기억 옵션 제공</Change>
    <!-- Automatically add Korean fonts to TextHudAPI, RichHudMaster, and Build Info -->
    <Change>TextHudAPI · RichHudMaster · Build Info 한글 폰트 자동 추가</Change>
    <!-- Add translated datapad display and original-content protection -->
    <Change>데이터패드에 번역 표시 및 원문 보호 기능 추가</Change>
  </Changes>
</KoreanPatchRelease>
";
    }
    #endregion
}
