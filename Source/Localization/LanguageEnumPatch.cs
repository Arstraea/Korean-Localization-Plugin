using System;
using System.Linq;
using Mono.Cecil;

namespace Arstraea.KoreanPatch.Localization
{
    // 이 단계에서 게임 타입을 참조하면 VRage가 수정 전에 로드될 수 있다.
    // Cecil 메타데이터만 바꾸고, 저장된 언어 번호는 이전 버전과 동일하게 유지한다.
    //
    // Referencing game types here can load VRage before Pulsar patches it.
    // Edit Cecil metadata only and retain the language ID stored by earlier versions.
    internal static class LanguageEnumPatch
    {
        internal const byte KoreanId = 255;
        internal const string KoreanName = "Korean";

        internal static void Apply(AssemblyDefinition assembly)
        {
            if (assembly == null || assembly.Name.Name != "VRage")
                throw new InvalidOperationException("Expected VRage.dll for Korean enum registration.");
            var type = assembly.MainModule.GetType("VRage.MyLanguagesEnum");
            if (type == null || !type.IsEnum || type.Fields.Single(f => f.Name == "value__").FieldType.MetadataType != MetadataType.Byte)
                throw new InvalidOperationException("MyLanguagesEnum no longer uses the expected byte enum layout.");
            var named = type.Fields.FirstOrDefault(f => f.Name == KoreanName);
            var numbered = type.Fields.Where(f => f.HasConstant && Convert.ToInt32(f.Constant) == KoreanId).ToArray();
            if (named != null || numbered.Length != 0)
            {
                if (named != null && named.IsLiteral && named.IsStatic && named.HasConstant
                    && Convert.ToInt32(named.Constant) == KoreanId && numbered.Length == 1 && numbered[0] == named) return;
                throw new InvalidOperationException("Korean name or language ID 255 is occupied by another enum member.");
            }
            type.Fields.Add(new FieldDefinition(KoreanName,
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, type)
                { Constant = KoreanId });
        }
    }
}
