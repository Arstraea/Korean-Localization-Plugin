namespace Arstraea.KoreanPatch
{
    // 모드별 로그 표현이 개발 시점에 따라 달라지지 않도록 대상과 상태의 형식을 공유한다.
    // 게임 시작 전에도 사용할 수 있게 GUI나 렌더 타입에 의존하지 않는다.
    //
    // Share target/state formatting so mod diagnostics stay consistent across development stages.
    // Avoid GUI/render dependencies so pre-game preparation can use the same format.
    internal static class DiagnosticText
    {
        internal static string ModHookReady(string target, string steamIds, string fonts) =>
            "Mod hook ready - " + target + " (Steam: " + steamIds + "); waiting for " + fonts + " font definitions.";

        internal static string ModFont(string target, string state) => "Mod font - " + target + ": " + state;
    }
}
