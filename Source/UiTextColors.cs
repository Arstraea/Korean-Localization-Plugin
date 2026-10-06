namespace Arstraea.KoreanPatch
{
    // 사용자 요청에 따라 빨간색 텍스트의 기본색은 매니저에서 쓰던 연한 빨간색으로 통일한다.
    // 상수만 제공하여 시작 전 매니저에서 게임 GUI의 정적 초기화를 유발하지 않는다.
    //
    // Use the manager's soft red as the default for red text, as requested by the user.
    // Constants avoid triggering game GUI initialization in the pre-game manager.
    internal static class UiTextColors
    {
        internal const byte SoftRedR = 0xF1;
        internal const byte SoftRedG = 0x5F;
        internal const byte SoftRedB = 0x5F;
    }
}
