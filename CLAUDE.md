# AxisTune — 프로젝트 가이드 (CLAUDE.md)

## 개요
AxisTune은 reWASD형 Windows 전용 앱이다. 물리 컨트롤러(Xbox/PS/Switch Joycon 및 기타)의 입력을 받아
스틱·트리거의 입력 범위(min/max)와 응답 곡선(커브)으로 **정제**한 뒤, 가상 Xbox 360 컨트롤러로 출력한다.
게임에는 원본 장치가 보이지 않고 **정제된 가상 입력만** 전달된다.

## 기술 스택
> 주: 당초 .NET 8 LTS를 계획했으나, 이 PC에 .NET 10 런타임이 이미 설치되어 있고 Avalonia 템플릿이 .NET 10/Avalonia 12로 생성되어 버전 정합을 위해 .NET 10으로 통일함.

- **런타임/UI**: C# / .NET 10 + Avalonia 12 (MVVM, CommunityToolkit.Mvvm)
- **입력 감지**: SDL3 (`ppy.SDL3-CS`) — 진동 왕복, 자동 감지, 스틱 보정 일괄 처리
- **가상 출력**: ViGEmBus (`Nefarius.ViGEm.Client`) — 가상 Xbox 360 패드
- **장치 숨김**: HidHide (`Nefarius.Drivers.HidHide`)
- **직렬화**: System.Text.Json (소스 제너레이터 사용 — 리플렉션/AOT 비용 회피)

## 솔루션 구조
```
AxisTune.sln
├─ AxisTune.Core/      도메인: 처리 파이프라인, 커브/LUT, 매핑, 프로파일 (UI/드라이버 무관, 테스트 대상)
├─ AxisTune.Input/     SDL3: 장치 열거·분류, 폴링 스레드, 물리 장치 진동 출력
├─ AxisTune.Output/    ViGEm 가상 패드 + HidHide 숨김/화이트리스트
├─ AxisTune.App/       Avalonia UI, 트레이, 설정, 커브 에디터
├─ AxisTune.Core.Tests/ xUnit 단위 테스트
└─ drivers/            ViGEmBus / HidHide 설치 프로그램 번들
```

---

## 핵심 정책 (반드시 준수)

### 1. 최적화 최우선 (Optimization-first)
입력→출력 지연(latency)이 제품 가치의 핵심이다. 실시간 경로(hot path)에서는 다음을 엄수한다.
- **실시간 스레드는 할당(allocation) 0을 목표**로 한다. hot path에서 `new`, LINQ, 박싱, 문자열 포맷, 람다 캡처 금지.
  상태는 사전 할당된 버퍼/구조체에 재사용한다.
- 커브 평가는 매 틱 계산하지 않고 **사전계산 LUT**(예: 1024 엔트리)를 조회한다. LUT는 설정 변경 시에만 재생성.
- 축 처리 수학은 `float` + 분기 최소화. `struct`(값 타입) 위주, 가능하면 `readonly struct`/`in` 매개변수.
- 입력 스레드는 전용 스레드(고우선순위)에서 고정 틱(~1kHz)으로 돌리되, **불필요한 폴링/스핀 없이** SDL 이벤트·타이밍에 맞춘다.
- UI ↔ 실시간 스레드는 **락 프리 스냅샷**(불변 설정 객체 교체, `Volatile`/`Interlocked`)으로 통신. hot path에서 lock 금지.
- 설정/프로파일 변경은 새 불변 객체를 만들어 원자적으로 교체(swap)한다 (copy-on-write).
- 모든 PR/변경 시 "이 코드가 hot path에 있는가? 할당이 생기는가?"를 스스로 점검한다.

### 2. 드라이버 On/Off 토글 (앱 + 트레이 상시 노출)
- 가상 컨트롤러 출력(드라이버 파이프라인)을 **언제든 On/Off** 할 수 있어야 한다.
- **메인 앱 UI**에 명확한 On/Off 토글(상태 표시 포함)을 둔다.
- **작업표시줄 트레이 아이콘은 항상 표시**되며, 클릭/우클릭 시 컨텍스트 메뉴에 **On/Off 버튼**을 노출한다.
  - 트레이 아이콘 상태(색/툴팁)로 현재 On/Off를 즉시 식별 가능해야 한다.
- Off 시: ViGEm 가상 패드 분리 + HidHide 숨김 해제(원본 장치 복원). On 시: 재적용.

### 3. 백그라운드 실행 / 완전 종료
- **메인 창의 닫기(X)** → 앱은 종료되지 않고 **트레이로 최소화되어 백그라운드 실행**을 계속한다.
- **트레이 메뉴의 "종료"** 클릭 → 드라이버 정리(가상 패드 분리, HidHide 복원) 후 **완전 종료**.
- 완전 종료 시 리소스 정리(SDL quit, ViGEm dispose, HidHide 상태 복원)를 반드시 수행한다.

### 4. 시작프로그램(자동 시작)
- **설정 화면에서 켜고 끌 수 있다.** 기본값은 Off.
- 구현: 현재 사용자 레지스트리 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`에
  실행 파일 경로를 등록/해제 (관리자 권한 불필요한 HKCU 사용).

---

## 권한 / 드라이버 전제
- ViGEm/HidHide 제어는 **관리자 권한** 필요 → `app.manifest`를 `requireAdministrator`로 설정.
- 앱 시작 시 ViGEmBus / HidHide 설치 여부를 확인하고, 미설치 시 `drivers/`의 번들 설치 프로그램으로 안내.

## 반드시 처리할 함정
1. **HidHide 화이트리스트**: 물리 장치를 숨기면 SDL도 못 읽는다 → 앱 프로세스를 HidHide 화이트리스트에 등록.
2. **루프백 차단**: ViGEm 가상 패드도 SDL에 게임패드로 잡힌다 → VID/PID(`0x045E`/`0x028E`)로 필터링하거나
   사용자가 선택한 물리 장치만 열어 입력 루프백을 막는다.
3. **블루투스 진동 한계**: SDL3는 일부 컨트롤러(DualSense/Joycon)의 BT 진동이 제한될 수 있다 → 초기엔 USB 유선 우선 안내.
   DualSense는 `SDL_HINT_JOYSTICK_ENHANCED_REPORTS` 힌트를 켠다.

## 자동 감지 / 수동 매핑
- SDL gamepad add/remove 이벤트 + `SDL_GetGamepadType`로 Xbox/PS4/PS5/SwitchPro/Joycon 자동 분류·매핑.
- 미지원 컨트롤러는 일반 조이스틱으로 노출 → UI에서 버튼/축을 Xbox 컨트롤로 직접 수동 매핑.

## 빌드 / 테스트
- 빌드: `dotnet build`
- 테스트: `dotnet test` (Core 단위 테스트 — 커브 평가/축 처리)
- 실행: `dotnet run --project AxisTune.App` (관리자 권한 콘솔에서)
- Stage 1 통합 검증: 게임패드 테스터(`joy.cpl`, https://hardwaretester.com/gamepad)에서
  가상 Xbox 360 패드 인식 + 물리 장치 숨김 + 진동 왕복 확인.

## 작업 진행 방식 (단계적 MVP)
- **Stage 1**: 감지 → 가상 출력 패스스루(+진동 왕복) + 드라이버 On/Off + 트레이/백그라운드 + 시작프로그램.
- **Stage 2**: 축 정제(min/max·데드존·커브) + 커브 에디터 + 실시간 미리보기.
- **Stage 3**: 수동 매핑 + 프로파일 저장/불러오기/전환.
각 단계는 검증 후 다음 단계로 확장한다.

## 코드 스타일
- nullable 활성화, `file-scoped namespace`, `var`는 타입이 자명할 때만.
- Core는 플랫폼/UI 의존성 0 (테스트 가능성 유지). 드라이버/SDL 호출은 Input·Output 계층에 격리.
