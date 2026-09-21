# 시간 시스템

1. Unity 메뉴 HoneyComb > Setup Time System 실행.
2. 선택된 GameMasterData 에셋을 Inspector에서 편집한 뒤 Play.
3. 자동 진행 일시정지/재개, +1 hour, 시간 입력 후 Advance 버튼으로 검증.

마스터데이터: Assets/_Project/Settings/GameMasterData.asset
- realSecondsPerTick: 실제 몇 초마다 게임 내 1시간인가. 기본 1초는 개발용.
- initialElapsedHours: 게임 시작 시 경과 시간. 0 = Year 1 / Day 1 / 00:00.
- startAutomatic: 자동 시간 진행 시작 여부.
- defaultAdvanceHours: 개발용 입력칸 초기값.
- maxAdvanceHours: 한 번에 요청할 수 있는 최대 시간.
- maxTicksPerFrame: 한 렌더 프레임의 최대 처리 Tick 수.
- showTimeDebugPanel: 개발용 패널 표시 여부.

모든 초기 게임 밸런스 수치는 마스터데이터에서 관리한다.
현재 마스터는 시간과 개발 설정만 포함한다. 이후 환경·봉군·벌 초기값을 섹션이나 별도 에셋으로 나누어 연결한다.
실행 상태는 마스터 에셋에 기록하지 않으며 Play 시작 때 값을 복사한다.
1틱=1시간, 24시간=1일, 365일=1년은 이 프로토타입의 고정 시간 규칙이다.
화면 여백과 폰트 크기는 게임 밸런스 수치가 아닌 개발 UI 표현값이다.

자동 시간은 프레임 사이 실제 경과 시간을 누적하며 남은 소수 초를 보존한다.
수동 요청은 자동 일시정지 중에도 실행된다. 수동 처리 중에는 자동 시간을 누적하지 않는다.
기존 자동 시간의 소수 초는 수동 처리 후에도 유지된다.
백그라운드/포커스 상실 동안 시간이 멈추며 오프라인 보상은 아직 없다.
대량 요청도 시계 숫자를 곧바로 더하지 않고 각 Tick 이벤트를 순서대로 발행한다.
향후 단일 Simulation Step을 연결해 생태 계산 후 상태를 갱신하도록 확장한다.
현재 Tick 이벤트가 주는 값은 진행 후 누적 시간이다.

개발 화면은 임시 IMGUI 패널이며 정식 관찰 UI가 아니다.
Game 뷰 1080x1920에서 확인하고 Android 터치/키보드는 실제 기기에서 추가 검증한다.
Tests/EditMode의 테스트는 Unity Test Runner에서 실행할 수 있다.
