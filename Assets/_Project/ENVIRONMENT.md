# 환경 시스템 v0.1

Unity 메뉴 HoneyComb > Setup Environment System을 한 번 실행한다.
Settings/EnvironmentConfig.asset을 생성하고 기존 GameMasterData.environmentConfig에 연결한다.
다시 실행해도 기존 연결과 설정값을 유지한다. Setup Time System도 이 연결을 준비한다.
Play를 시작하면 시간 패널 아래 ENVIRONMENT DEBUG가 나타난다.

초기값: 외기온 20°C, 습도 60%, 강수 강도 0 mm/h, 풍속 0 m/s, 밝기 1.
수치는 EnvironmentConfig에서 편집하며 Play를 다시 시작할 때 복사된다.
이 에셋은 GameMasterData에서 참조하는 마스터데이터의 환경 부분이다.
실행 상태가 마스터 에셋에 기록되지 않는다. 현재는 고정 환경이며 날씨 생성, 낮밤, 계절 변화는 없다.
강수는 누적 강수량이 아닌 mm/h 강도다. 밝기는 일조 시간이 아닌 0~1 값이다.

구조:
- Simulation/Environment/EnvironmentState: 단위가 명시된 불변 외부 환경 스냅샷.
- Simulation/Environment/EnvironmentSystem: Unity에 의존하지 않는 시간별 환경 갱신.
- Unity/Configuration/EnvironmentConfig: Inspector 편집용 초기값.
- SimulationRunner: Clock.Tick 구독과 해제, 환경 호출, 개발 표시.

시계는 환경을 참조하지 않는다. Runner의 단일 Tick 처리 지점에서 순서를 명시한다.
향후 벌통/봉군 시스템을 붙일 때 이벤트 구독 순서에 의존하지 말고,
같은 상태 읽기 → 수요/변경 계산 → 공유 자원 배분 → 일괄 적용 단계를 조정한다.
현재 Tick은 한 번 실행될 때마다 환경 처리 횟수와 마지막 갱신 시간을 기록한다.
초기 경과 시간이 100이면 마지막 갱신 시간은 100, 처리 횟수는 0에서 시작한다.

수동 검증: Play → 자동 진행 Pause → 현재 Environment ticks 기록 → +1 hour → 1 증가 확인.
24 입력 후 Advance → 정확히 24 증가 확인. 고정 환경값은 그대로인 것이 정상이다.
EditMode 테스트는 환경 초기화, 자동/수동 진행, 1년 일괄 진행, 해제, 잘못된 값 거부를 검증한다.
벌통, 봉군, Frame, Cell 생성은 다음 작업이다.
