using System;
using System.IO;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Arstraea.KoreanPatch.Mods
{
    // TextHudAPI와 RichHudMaster가 같은 설치 DDS와 HUD 재질을 참조한다.
    // 게임 GUI와의 GPU 텍스처 공유 여부까지 보장하는 것은 아니다.
    //
    // Both HUD frameworks reference the same installed DDS and material names.
    // This does not guarantee GPU texture sharing with the game's GUI renderer.
    internal static class SharedHudMaterials
    {
        internal static MyStringId Name(string path)
            => MyStringId.GetOrCompute("Arstraea.KoreanPatch.Hangul." + Path.GetFileName(path));

        internal static void Register(string path)
        {
            var name = Name(path);
            MyTransparentMaterial existing;
            if (MyTransparentMaterials.TryGetMaterial(name, out existing))
            {
                if (!string.Equals(existing.Texture, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Shared Korean material name is occupied: " + name);
                return;
            }
            MyTransparentMaterials.AddMaterial(new MyTransparentMaterial(name, MyTransparentMaterialTextureType.FileTexture,
                path, null, 0f, false, false, Vector4.One, Vector4.Zero, Vector4.One, Vector4.One, false, false, useAtlas: false));
        }
    }
}
