# Dependencies and credits

Arstraea가 기획·검증·유지보수를 맡고 AI의 코딩 지원과 Pulsar 개발자들의 기술 검토를 받아 플러그인 코드를 작성했습니다. 게임의 디컴파일 본문이나 외부 템플릿의 구현 코드를 복사해 포함하지 않았습니다.

Arstraea planned, tested and maintains this plugin, with AI coding assistance and technical review from Pulsar developers. Decompiled game implementations and third-party template implementations are not bundled in this repository.

플러그인 코드의 사용 조건은 [LICENSE](LICENSE)를 따릅니다. 폰트·제3자 자료에는 각 원본 라이선스가 우선합니다.

The plugin code terms are in [LICENSE](LICENSE). Fonts and third-party materials retain their own licenses, which take precedence for those materials.

| 대상 | 출처 | 사용 방식 |
| --- | --- | --- |
| Harmony 2 | Andreas Pardeike 및 기여자 · [Harmony](https://github.com/pardeike/Harmony) · [MIT](https://github.com/pardeike/Harmony/blob/master/LICENSE) | 설치된 Pulsar의 라이브러리를 참조합니다. DLL을 동봉하지 않습니다. |
| Mono.Cecil | [Mono.Cecil](https://github.com/jbevain/cecil) · [MIT](https://github.com/jbevain/cecil/blob/master/LICENSE.txt) | 설치된 Pulsar의 라이브러리를 참조합니다. DLL을 동봉하지 않습니다. |
| Pulsar | [Pulsar](https://github.com/SpaceGT/Pulsar) | Preloader와 플러그인 로딩 계약을 사용합니다. 로더 코드·바이너리는 동봉하지 않습니다. |
| Space Engineers / VRage / SharpDX | 게임 설치본과 그 의존 라이브러리 | 설치된 DLL을 참조합니다. 게임 코드·바이너리는 재배포하지 않습니다. |
| Korean Localization Pack | Arstraea · [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1843839106) | 번역·폰트 이미지·RichHud 변환 XML을 창작마당 자료에서 읽습니다. 이 소스 저장소에는 동봉하지 않습니다. |

팩의 한글 폰트는 네이버·네이버 문화재단의 **나눔바른고딕**을 게임용 bitmap과 글자 정보로 변환한 자료입니다. Arstraea는 변환·배치·그림자·외곽선과 메트릭을 구성했습니다. 해당 파생 폰트 자료에는 [SIL OFL 1.1](https://openfontlicense.org/open-font-license-official-text/)이 적용되며 플러그인 코드의 사용 제한은 적용되지 않습니다. 팩에는 `THIRD_PARTY_NOTICES.txt`와 `Licenses/NanumBarunGothic-OFL.txt`를 함께 배포합니다. 원본 영문·게임 아이콘은 해당 게임의 권리를 따릅니다.

The pack's Hangul bitmap font and glyph data are derived from NAVER's NanumBarunGothic. Arstraea prepared the conversion, atlas layout, shadows/outlines and metrics. These font materials remain under SIL OFL 1.1; the plugin code restrictions do not apply to them. Copyright and license notices accompany the pack. Original game characters and icons retain their respective rights. [NAVER's official notice](https://help.naver.com/service/30016/contents/18088?osType=PC&lang=ko), checked 2026-10-13.
