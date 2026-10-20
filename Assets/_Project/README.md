# 초기 구조

Simulation: UnityEngine 참조가 없는 C# 생태 계산과 상태.
Unity: 설정 에셋, 실행 연결, 화면 표시. Simulation을 참조.
Persistence: 저장 및 로그. Simulation을 참조하며 화면에는 의존하지 않음.

벌과 Cell은 일반 C# 데이터로 만들고 개체마다 GameObject를 생성하지 않는다.
규칙 설정과 실행 중 상태를 분리한다. Cell의 먹이가 원본이고 봉군 합계는 집계값이다.
1 Tick은 게임 내 1시간이다. 렌더 프레임 수와 분리한다.
공유 자원은 수요 계산 → 배분 → 변경 적용 순서로 처리한다.

Unity 메뉴 HoneyComb > Setup Android Prototype으로 초기 구조와 씬을 생성한다.
기존 SampleScene을 복사하므로 Universal 2D 카메라와 조명 설정을 유지한다.
이 단계에는 생태 시뮬레이션과 시간 UI가 아직 포함되지 않는다.

Game 뷰: + 버튼에서 Fixed Resolution 1080 x 1920을 추가해 선택한다.
Android: File > Build Profiles에서 Android 프로필을 추가하고 Switch Profile을 실행한다.
프로필이 별도 씬 목록을 사용하면 Assets/_Project/Scenes/Simulation.unity를 포함한다.
첫 모바일 UI 작성 시 Canvas Scaler 및 Screen.safeArea 대응을 추가한다.
