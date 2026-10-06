using System.Runtime.CompilerServices;
using System.Reflection;

[assembly: InternalsVisibleTo("Arstraea.KoreanPatch.Tests")]
// Pulsar는 csproj를 읽지 않으므로 소스 컴파일에도 같은 버전을 제공한다.
//
// Supply the version to Pulsar's source compiler, which does not read the csproj.
[assembly: AssemblyVersion("0.5.0.0")]
[assembly: AssemblyFileVersion("0.5.0.0")]
[assembly: AssemblyInformationalVersion("0.5.0")]
