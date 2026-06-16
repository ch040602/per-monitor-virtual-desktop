# per-monitor-virtual-desktop

Windows용 모니터별 가상 데스크톱 제어 도구입니다. 앱 이름은 **PerMonitorVD**, CLI 이름은 **pvdctl**입니다.

[English](README.md) | [简体中文](README.zh-CN.md)

PerMonitorVD는 Windows에서 **모니터별 가상 데스크톱 UX**를 실험하는 프로토타입입니다. Windows Shell이 여러 native virtual desktop을 동시에 활성화하도록 바꾸지는 않습니다. 대신 `[PMVD] ACTIVE` 데스크톱 하나를 현재 작업 공간으로 유지하고, 비활성 모니터 workspace의 창을 실제 Windows virtual desktop에 parking합니다.

## 주요 기능

- 모니터별 workspace 전환.
- `Ctrl+Alt+Shift+Left/Right`로 마우스가 있는 모니터만 전환.
- `Ctrl+Alt+Shift+Up/Down`으로 포커스된 창을 이전/다음 workspace로 이동.
- `Ctrl+Alt+Shift+Home`으로 PMVD Home 창 열기.
- Home 오버레이에서 각 모니터의 PMVD 데스크톱과 사용 중인 앱만 확인.
- 앱 칩을 원하는 데스크톱 카드로 바로 드래그 앤 드롭해서 이동.
- 트레이 메뉴에서 현재 보이지 않는 다른 PMVD 데스크톱의 앱을 보고, 해당 앱이 있는 데스크톱으로 바로 이동.
- Windows Task View와 비슷한 모니터별 데스크톱 카드 UI.
- Home 창에서 모니터별 최대 관리 창 수 조절. `0`은 무제한입니다.
- `pvdctl diagnostics`로 진단 보고서 생성.
- Windows taskbar가 모든 native virtual desktop의 앱을 보이도록 best-effort 설정 적용.
- 일반 작업 표시줄 앱 버튼 없이 시스템 트레이에서 상주 실행하고 Windows 로그인 시 자동 시작 등록.

## 빌드

일반 사용자는 GitHub Releases에서 `per-monitor-virtual-desktop-*-win-x64.zip` 파일을 내려받아 계속 둘 폴더에 압축을 풀고 실행하면 됩니다.

```powershell
.\PerMonitorVD.exe
.\pvdctl.exe status
```

Release zip은 Windows x64 self-contained 패키지라 .NET 런타임을 별도로 설치하지 않아도 됩니다.

```powershell
dotnet restore .\PerMonitorVD.sln
dotnet build .\PerMonitorVD.sln -c Release
```

실행:

```powershell
.\src\PerMonitorVD\bin\x64\Release\net8.0-windows10.0.19041.0\PerMonitorVD.exe
```

CLI:

```powershell
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe status
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe diagnostics
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe home
.\src\pvdctl\bin\Release\net8.0-windows\pvdctl.exe activate-window --hwnd 0x00000000000A1234
```

## 기본 단축키

```text
Ctrl + Alt + Shift + Left    마우스가 있는 모니터의 이전 workspace
Ctrl + Alt + Shift + Right   마우스가 있는 모니터의 다음 workspace
Ctrl + Alt + Shift + 1/2/3   마우스가 있는 모니터의 workspace 1/2/3
Ctrl + Alt + Shift + Up      포커스된 창을 이전 workspace로 이동
Ctrl + Alt + Shift + Down    포커스된 창을 다음 workspace로 이동
Ctrl + Alt + Shift + Home    PMVD Home 열기
Ctrl + Alt + Shift + R       상태 복구
```

## Logitech 권장 매핑

```text
Gesture left   -> Ctrl + Alt + Shift + Left
Gesture right  -> Ctrl + Alt + Shift + Right
Gesture up     -> Ctrl + Alt + Shift + Up
Gesture down   -> Ctrl + Alt + Shift + Down
```

Logitech의 기본 Virtual Desktop profile은 처음에는 쓰지 마세요. Windows native global desktop 전환을 보낼 수 있습니다.

## 시스템 트레이와 자동 시작

PerMonitorVD는 일반 작업 표시줄 앱 버튼 없이 실행됩니다. 실행 후에는 Wi-Fi, Bluetooth, 볼륨 아이콘이 있는 Windows 시스템 트레이/알림 영역의 아이콘을 사용하세요. Windows가 아이콘을 숨긴 경우 `설정 -> 개인 설정 -> 작업 표시줄 -> 기타 시스템 트레이 아이콘`에서 PerMonitorVD를 켜면 됩니다.

`StartWithWindows`는 기본값이 `true`입니다. 앱 시작 시 현재 `PerMonitorVD.exe` 경로를 현재 사용자 `Run` 레지스트리에 등록해 Windows 로그인 때 자동 실행되도록 합니다. 압축을 푼 폴더를 나중에 옮기면 새 위치에서 `PerMonitorVD.exe`를 한 번 실행해 자동 시작 경로를 갱신하세요. 트레이 메뉴의 `Start with Windows`에서 켜고 끌 수 있고, `%LOCALAPPDATA%\PerMonitorVD\config.json`에서도 수정할 수 있습니다.

## 검증 순서

1. `PerMonitorVD.exe`를 실행합니다.
2. Task View에서 `[PMVD] ACTIVE`와 parking desktop이 생성됐는지 확인합니다.
3. 모니터 1에 메모장, 모니터 2에 브라우저를 엽니다.
4. 마우스를 모니터 1에 두고 `Ctrl+Alt+Shift+Right`를 누릅니다.
5. 모니터 1의 창만 전환되고 모니터 2의 창은 유지되는지 확인합니다.
6. `Ctrl+Alt+Shift+Left`로 복귀를 확인합니다.
7. `pvdctl diagnostics`를 실행하고 반환된 report 경로를 저장합니다.
8. `Ctrl+Alt+Shift+Home`으로 Home 창을 열고 앱 칩을 다른 데스크톱 카드로 드래그 앤 드롭해 이동을 확인합니다.
9. 트레이 아이콘 우클릭 -> `Other desktop apps`에서 다른 데스크톱의 앱을 선택해 해당 데스크톱으로 바로 이동되는지 확인합니다.
10. Home 창에서 각 모니터의 최대 관리 창 수를 조절합니다. 제한에 도달한 모니터에는 새 창 자동 추적과 명시적 이동이 제한됩니다.
11. Logitech gesture를 권장 매핑대로 설정합니다.

## 주의 사항

- 일반 사용 중에는 Task View에서 `[PMVD] ...` parking desktop을 직접 사용하지 마세요.
- 작업 표시줄이 모든 데스크톱 앱을 보이지 않으면 Windows 설정에서 `개인 설정 -> 작업 표시줄 -> 작업 표시줄 동작 -> 작업 표시줄에 열려 있는 모든 창 표시`를 `모든 데스크톱에서`로 설정하세요.
- 관리자 권한 창을 제어하려면 PerMonitorVD도 관리자 권한으로 실행해야 할 수 있습니다.
- 창이 사라지면 tray menu에서 `Return to active desktop`, `Rescue all windows`, `Repair state` 순서로 복구하세요.
