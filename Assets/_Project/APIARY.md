# 좌표 격자 양봉장

Play 종료 후 ApiaryConfig에서 Width=5, Height=4를 확인하고 Play한다.
기존 hiveCapacity 필드는 제거되었다. 기존 에셋의 이름, ID, 초기 벌통 수는 유지된다.
새 Width/Height의 기본값은 5/4이며 원하는 수치로 편집하고 저장한다.
초기 벌통 수가 새 격자보다 많으면 초기화를 거부하므로 수치를 조정한다.
기존 ApiaryConfig가 연결되어 있으면 Setup 메뉴를 다시 실행할 필요가 없다.

좌표는 (0,0)부터 (Width-1,Height-1). 화면은 위쪽이 높은 Y, 오른쪽이 높은 X다.
초기 벌통은 (0,0)부터 X를 먼저 증가시키고 다음 Y 행으로 채운다.
ApiaryCell은 양봉장 바닥 칸이며 벌집 내부의 방(Cell)과 별개다.
실제 미터, Unity 월드 좌표, 벌통 내부, Colony, Comb는 이번 범위가 아니다.

GetCell/GetHive: 잘못된 좌표는 null 반환.
PlaceHive: 범위 밖, null, 점유 칸, 이미 배치된 개체, 목록 내 중복 ID면 false.
RemoveHive: 잘못된 좌표 또는 빈 칸이면 false.
MoveHive: 범위 밖, 빈 출발점, 점유 도착점(같은 칸 포함)이면 false. 실패 시 원래 상태 유지.
이동은 같은 Hive 객체/ID를 유지하고 개수도 바꾸지 않는다.
제거한 Hive는 참조를 보유하고 있으면 다른 칸에 재배치할 수 있다.
TryCreateHive는 새 고유 번호를 발급한 뒤 선택 좌표에 배치한다.
Hives는 배치 목록의 읽기 전용 뷰. 칸과 목록 변경은 ApiaryState를 통해 수행한다.
격자는 정수 Width x Height로 고정되며 실행 중 리사이즈는 아직 없다.
잘못된 설정으로 과도하게 메모리를 할당하지 않도록 프로토타입은 65536칸을 상한으로 검사한다.
이는 게임 밸런스용 capacity가 아닌 구현 보호 한도다.

APIARY GRID에서 칸을 클릭한다. . 은 빈 칸, H는 벌통, 대괄호는 선택 칸이다.
Place Hive here / Remove Hive here로 선택 칸을 조작한다.
이동: 벌통 칸 선택 → Move: select this hive → 빈 칸 선택 → Move here.
가로/세로 스크롤로 큰 격자를 확인한다. 큰 격자를 매 프레임 전부 그리는 임시 UI이므로 작은 격자로 검증한다.
선택/이동 UI는 Presentation/ApiaryGridDebugPanel에 분리되며 Simulation은 UnityEngine을 참조하지 않는다.
시간/환경 Tick 처리와 마스터데이터의 Play 시작 시 복사 방식은 유지된다.
