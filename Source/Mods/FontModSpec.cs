namespace Arstraea.KoreanPatch.Mods
{
    internal sealed class FontModSpec
    {
        internal static readonly FontModSpec TextHud = new FontModSpec(758597413, 2409196107);
        internal static readonly FontModSpec RichHud = new FontModSpec(1965654081, 2940288034);
        internal readonly ulong FrameworkId, FontId;
        private FontModSpec(ulong framework, ulong font) { FrameworkId = framework; FontId = font; }
    }
}
