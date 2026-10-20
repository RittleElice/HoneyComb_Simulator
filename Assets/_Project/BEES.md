# 벌 모델: 직접 배치 v0.1

## 사용법
Play를 재시작하고 양봉장 격자에서 벌통 → 계상 → 소비장 슬롯을 선택한다.
BEES / MANUAL PLACEMENT에서 Worker/Queen/Drone, Egg/Larva/Pupa/Adult, Front/Back을 고른다.
Add 1 Bee to selected Frame은 한 번에 한 마리씩 추가한다.
성충은 소비장 표면에 놓이며 미건설 소비장에도 배치 가능하다. 성충은 소방 내용물을 차지하지 않는다.
봉충(Egg/Larva/Pupa)은 X/Y로 지정한 건설도 100%, Empty 소방에만 놓인다.
같은 소방에 두 개체를 놓거나 범위 밖에 놓는 요청은 거부한다.
소방 단계는 BeeState.Development가 소유한다. CellState에는 Brood와 OccupantBeeId만 기록한다.
일반 소방 편집으로 OccupantBeeId를 삽입/지우지 못하게 하여 고아 봉충을 방지한다.

## 마스터데이터
GameMasterData Inspector의 Bee Creation Defaults:
- defaultCaste: Worker
- defaultDevelopment: Adult
- initialHealth: 100 (0~100)
새로 Play할 때 복사한다. 런타임 종류/단계 선택은 에셋을 바꾸지 않는다.
자동 초기 벌은 0마리이며, 요청한 수동 추가 방식만 구현했다. 초기 집단 생성 수치나 임의의 발육 기간은 추가하지 않았다.
이번 개발 도구는 한 봉군에 여러 여왕도 배치할 수 있다. 사회적 상호작용은 아직 없다.

## 데이터와 수명
BeeState는 일반 C# 객체이며 ID/봉군 ID/계급/성별/발육 단계/건강/생존/위치를 갖는다.
일벌·여왕은 Female, 수벌은 Male로 분류한다.
알로 추가한 개체의 LaidHour는 추가한 시각이다.
유충/번데기/성충은 이미 해당 단계의 개체를 직접 주입하므로 산란 시각은 null(알 수 없음)이다.
성충의 AdultSinceHour는 이 프로토타입에서 성충으로 배치한 시각이다. 과거 연령을 임의로 추정하지 않는다.
CreatedHour는 모든 개체에 대해 배치 시각을 기록한다.
건강 감소/사망/산란/자동 발육/작업/이동 AI가 없으므로 생성된 개체는 Alive 상태를 유지한다.
한 시간 Tick으로 이 모델의 단계나 건강은 변하지 않는다.

BeePopulation은 한 실행 양봉장에 하나이며 개체와 봉군의 저장소다.
ColonyState는 HiveState와 별도 객체이며 첫 성공적인 배치 때 해당 벌통의 봉군을 생성한다.
API 실패 시 봉군과 벌을 생성하지 않는다.
BeeLocation은 Frame ID + Side, 봉충에 한해 X/Y를 가진다.
단상/양봉장 야외/외부 지역의 성충 배치와 수동 이동도 지원한다. 위치 및 조작은 PLACES.md 참고.

## 기존 이동·편집과의 연결
소비장을 옮겨도 Frame ID가 유지되어 벌의 실제 위치는 소비장을 따라간다.
현재 벌통은 설치된 Frame을 찾아 계산한다. 봉군 소속은 자동 변경하지 않는다.
다른 봉군에 편입되거나 여왕이 교체되는 규칙은 후속 작업이다.
벌이 있는 소비장은 삭제를 거부하지만 이동은 가능하다.
봉군 또는 거주 벌이 있는 벌통도 삭제를 거부해 참조가 사라지는 것을 막는다.
개체 제거·장비 보관·사망 처리는 이번 범위가 아니며 Play를 재시작하면 초기화된다.

## 표시와 검증
봉군 소속 개체 수와 소비장 거주 개체 수는 다른 값이다.
소비장 이동 후 출신 봉군과 현재 벌통이 달라질 수 있고 UI에서 둘 다 표시한다.
Previous Bee / Next Bee로 개별 벌의 상태를 확인한다.
단독 검증 20개와 기존/신규 NUnit 테스트 케이스 30개를 독립 실행기로 검증했다.
Unity 라이브러리 기준 코드 컴파일을 확인했다. Unity Test Runner/실기기 UI 실행은 별도 확인이 필요하다.

## 벌 위치 조회 인덱스
BeePopulation.TryGetBee(id,out bee): ID 사전 조회.
GetBeesOnFrame(frameId): 소비장 전체의 벌 목록.
GetBeesOnSurface(frameId,side): 해당 면의 성충과 소방 내 봉충을 모두 포함한 목록.
GetBeeInCell(frame,side,x,y): 소방의 OccupantBeeId를 통해 봉충 개체 조회. 없으면 null.
목록은 읽기 전용이고 개체를 복제하지 않는다. 이미 존재하는 목록 뷰는 신규 등록을 반영한다.
아직 등록이 없는 위치는 공용 빈 목록을 반환하므로 다음 등록 후에는 다시 조회한다.
등록은 RegisterBee 한 경로에서 ID/위치 인덱스, 전체/봉군 목록, 거주 개체 수를 함께 갱신한다.
실패한 추가 요청은 인덱스를 변경하지 않는다.
Bee.Location은 외부에서 직접 수정할 수 없다. 성충 이동은 BeePopulation.TryMoveAdult가 위치/인덱스/거주 수를 함께 갱신한다. 우화/사망은 아직 없다.
소비장 통째 이동은 Frame ID와 면이 변하지 않으므로 재등록할 필요가 없다.
FindPhysicalHive는 여전히 물리 구조를 순회하며 벌통별 중복 인덱스는 만들지 않았다.
디버그 Previous/Next Bee는 선택된 앞/뒷면의 인덱스를 사용하고 봉충 좌표의 개체도 표시한다.
