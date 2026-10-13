# Korean Localization Plugin

Space Engineers 1의 Windows 클라이언트에서 한국어 표시와 한글 입력을 지원하는 Pulsar 플러그인입니다. 제작자: **Arstraea**.

Steam 창작마당의 [Korean Localization Pack 한글화 팩](https://steamcommunity.com/sharedfiles/filedetails/?id=1843839106)과 연계합니다.

- 게임의 사용 언어 목록에 한국어를 추가합니다.
- 영어 UI에서도 한글 입력을 사용할 수 있습니다.
- 한/영 입력 상태 기억 방식과 스크립트 코드 편집기의 한글 입력 제한을 설정할 수 있습니다.
- TextHudAPI · RichHudMaster · Build Info에 한글 폰트를 보충합니다.
- 메인 메뉴에서 적용된 한글화 팩과 플러그인의 업데이트 내역을 확인할 수 있습니다.

현재 버전은 **1.0.1**입니다. Pulsar 플러그인 목록에는 **1.0.1.0**으로 표시됩니다. [PluginHub 등록 신청](https://github.com/StarCpt/PluginHub/pull/216)은 검토 중입니다.

## Requirements

- Steam Windows판 Space Engineers 1
- [Pulsar](https://github.com/SpaceGT/Pulsar)의 Legacy / CLR 런타임
- Steam 창작마당 Korean Localization Pack (1843839106) 구독 및 다운로드

클라이언트 전용 플러그인입니다. 멀티플레이 서버에 설치할 필요가 없으며 전용 서버 호스트에서는 처리를 생략합니다. Interim/CoreCLR 및 다른 플랫폼은 현재 지원 검증 범위에 포함하지 않습니다.

게임 설치 파일은 변경하지 않습니다. 폰트·번역 캐시와 설정은 게임 사용자 자료 폴더의 `Storage/Arstraea.KoreanPatch/`에 저장하며 별도 `-appdata` 경로도 따릅니다. 이전 개발 버전의 옵션과 뉴스 확인 기록은 가져오지 않습니다. 과거에 수동 설치하거나 이전 DLL이 설치한 게임 파일을 지우거나 원복하지는 않습니다.

제작자의 구형 폰트 전용 모드 `2409196107`·`2940288034`가 선택되어 있어도 대체 폰트 준비에 성공하면 클라이언트에서 중복 스크립트·정의 읽기를 생략합니다. TextHudAPI·RichHudMaster 본체, 모드 구독과 월드의 모드 목록은 변경하지 않습니다.

## Source and build

Pulsar의 소스 빌드 대상은 `Source/`입니다. 폰트 이미지와 번역은 한글화 팩에서 공급하므로 이 저장소에 동봉하지 않습니다. 게임·Pulsar의 DLL도 재배포하지 않습니다.

직접 빌드하려면 .NET SDK와 .NET Framework 4.8 개발 도구를 설치하고 다음 명령을 실행하세요.

```powershell
./Build.ps1 -GameBinPath 'C:/Program Files (x86)/Steam/steamapps/common/SpaceEngineers/Bin64' -PulsarRoot "$env:APPDATA/Pulsar"
```

결과물은 `bin/Release/net48/`에 생성됩니다. 사용자 설치 폴더로 자동 복사하지 않습니다.

플러그인 뉴스는 소스 문자열로 DLL 안에 포함됩니다. `Assets/PluginVersion.xml`과 출력 폴더의 `PluginVersion.xml`은 같은 내용을 확인할 기록이며, 직접 DLL을 설치할 때도 XML을 복사할 필요가 없습니다. 일반 플러그인 소개용 XML도 필요하지 않습니다.

플러그인이 표시하는 한국어 문구는 [KoreanText.cs](Source/Localization/KoreanText.cs) 한 파일에서 확인할 수 있습니다. 각 문구 위에는 검토자를 위한 영어 번역 주석이 있습니다. 뉴스 본문은 이 파일의 자동 생성 영역과 `Assets/PluginVersion.xml`에 함께 기록합니다.

## Support

문제 발생 시 증상·재현 순서와 플러그인 설정창의 **이번 실행 관련 로그 → 내역 복사** 내용을 함께 알려 주세요. 경로에 Windows 사용자 이름이 포함될 수 있으므로 공유 전에 확인하세요.

- [한글화 팩 창작마당 페이지](https://steamcommunity.com/sharedfiles/filedetails/?id=1843839106)
- [Space Engineers 네이버 카페](https://cafe.naver.com/spaceengineersforum)
- 제작자: Arstraea

의존성과 자료 공급 방식은 [CREDITS.md](CREDITS.md)를 참고하세요.
