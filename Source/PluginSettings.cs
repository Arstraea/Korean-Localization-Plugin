using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Arstraea.KoreanPatch.Input;

namespace Arstraea.KoreanPatch
{
    // 선택한 정책만 저장한다. 입력창에서 기억한 한/영 상태는 월드 세션 밖으로 내보내지 않는다.
    //
    // Persist preferences, never the volatile per-context Korean/English state.
    internal sealed class PluginSettings
    {
        internal bool HidePlatformIcons = true;
        internal InputLanguagePolicy InputLanguage = InputLanguagePolicy.EnglishExceptChat;
        internal bool VanillaScriptEditor = true;
        internal static PluginSettings Current { get; private set; } = new PluginSettings();
        internal static string PathForSettings => Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "Arstraea", "KoreanPatch", "settings.xml");

        internal static PluginSettings Read(string path)
        {
            var value = new PluginSettings();
            if (!File.Exists(path)) return value;
            using (var reader = XmlReader.Create(path, new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                var root = XElement.Load(reader);
                if (root.Name != "KoreanPatch") throw new InvalidDataException("Invalid KoreanPatch settings.");
                bool flag;
                if (bool.TryParse((string)root.Element("HidePlatformIcons"), out flag)) value.HidePlatformIcons = flag;
                if (bool.TryParse((string)root.Element("VanillaScriptEditor"), out flag)) value.VanillaScriptEditor = flag;
                InputLanguagePolicy policy;
                if (Enum.TryParse((string)root.Element("InputLanguagePolicy"), out policy)
                    && Enum.IsDefined(typeof(InputLanguagePolicy), policy)) value.InputLanguage = policy;
            }
            return value;
        }

        internal static void Load() { Current = Read(PathForSettings); }

        internal static void Write(string path, PluginSettings value)
        {
            if (!Enum.IsDefined(typeof(InputLanguagePolicy), value.InputLanguage))
                throw new ArgumentOutOfRangeException(nameof(value.InputLanguage));
            string temporary = FilePaths.Temporary(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                new XElement("KoreanPatch", new XElement("HidePlatformIcons", value.HidePlatformIcons),
                    new XElement("InputLanguagePolicy", value.InputLanguage),
                    new XElement("VanillaScriptEditor", value.VanillaScriptEditor)).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static void Save(PluginSettings value)
        {
            Write(PathForSettings, value);
            Current = value;
            Fonts.FontStartup.HidePlatformIcons = value.HidePlatformIcons;
            ImeControlPolicy.Policy.Configure(value.InputLanguage);
        }
    }
}
