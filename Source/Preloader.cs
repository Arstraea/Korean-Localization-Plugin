// Pulsar는 네임스페이스 없는 이 진입점을 게임 시작 전에 호출한다.
//
// Pulsar discovers this global entry point before starting the game.
public static class Preloader
{
    public static string[] TargetDLLs => Arstraea.KoreanPatch.ClientHost.CanRunClient
        ? new[] { "VRage.dll" } : new string[0];

    public static void Patch(Mono.Cecil.AssemblyDefinition assembly)
    {
        if (!Arstraea.KoreanPatch.ClientHost.CanRunClient) return;
        Arstraea.KoreanPatch.Localization.LanguageEnumPatch.Apply(assembly);
    }

    public static void Finish()
    {
        // Finish는 모든 사전 패치가 끝난 뒤 호출되어 게임 DLL 참조가 안전하다.
        // 다만 게임 시작 전이므로 IsDedicated가 아직 false일 수 있어 로더 확인도 유지한다.
        //
        // Finish runs after all preloader patches, so game references are safe here.
        // The game has not started yet; retain the loader guard even if IsDedicated is still false.
        if (!Arstraea.KoreanPatch.ClientHost.CanRunClient) return;
        FinishClient();
    }

    // 서버 판별 전에 JIT가 입력·게임 타입을 해석하지 않도록 분리한다.
    //
    // Keep the guarded entry point free of client types until host detection succeeds.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void FinishClient()
    {
        Arstraea.KoreanPatch.Input.ImeBootstrap.Install();
        Arstraea.KoreanPatch.Fonts.FontStartup.Run();
        Arstraea.KoreanPatch.Fonts.PatchStartupGate.Complete();
        Arstraea.KoreanPatch.Localization.LanguageSupport.Install(
            Arstraea.KoreanPatch.Fonts.FontStartup.TranslationResult);
    }
}
