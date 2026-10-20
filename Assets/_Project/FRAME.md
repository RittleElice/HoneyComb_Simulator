# 소비장 소방: 건설도 + 내용물

CellState는 readonly struct이며 Construction(byte 0~100), Content(byte enum), OccupantBeeId(long, 0=없음)를 갖는다.
기본값(default)은 Construction=0, Content=Empty. 새 소비장 7200칸은 모두 소초 상태다.
각 면의 CellState[] 배열과 90x40 해상도를 유지한다. 소방별 객체는 만들지 않는다.
벌 ID 필드 추가로 값 배열의 원소 크기가 늘었다. 실제 크기에는 런타임 정렬이 반영되며 소방별 참조 객체는 여전히 생성하지 않는다.

Construction 100 미만은 비어 있어야 한다. 생성자가 모순된 상태 생성을 거부한다.
Content는 Empty/Brood/Honey/Pollen/Nectar이며 Brood 소방의 OccupantBeeId는 별도 BeeState를 가리킨다. 내용물 시뮬레이션과 봉개는 미구현이다.
100%에서만 비어 있지 않은 내용물을 허용한다는 것은 이번 게임의 단순화된 규칙이다.
봉충의 개체/발육 상태는 아직 CellState에 넣지 않는다.

GetCell/TryGetCell은 값 복사본. TrySetCell은 값 전체를 교체하되 봉충 점유가 있거나 새 점유를 넣는 일반 편집을 거부한다. 봉충 생성은 BeePopulation을 통해서만 연결한다.
TrySetConstruction은 내용물을 유지하며, 내용물이 있는 칸을 100 미만으로 낮추는 요청을 거부한다.
TrySetContent는 건설도 유지. 미완성 칸에 비어 있지 않은 내용물을 넣으려 하면 false다.
Frame.TrySetAllConstruction은 앞뒤 양면을 먼저 검사한 뒤 적용하므로 실패 시 모두 원상 유지다.
FrameSurface의 같은 메서드는 해당 면만 조작한다.
CompletedCellCount와 AverageConstruction은 변경 시 합계를 갱신하며 UI에서 매번 전수 계산하지 않는다.
벌통 간 소비장 이동에서도 건설도/내용물/통계가 그대로 유지된다.

Play 재시작 → 벌통 → 계상 → 소비장 슬롯 선택 → CELL CONSTRUCTION.
Front/Back, X/Y, 0~100 입력 후 Apply to selected Cell로 한 칸 변경.
Apply to ALL Cells (both sides)는 소비장 양면 전체에 적용한다.
Complete는 건설도 100인 칸 수, Average는 미완성 칸도 포함한 전체 산술 평균이다.
이 개발 도구는 자원 없이 값을 직접 바꾼다. 시간별 건설/일벌/밀랍 소모는 아직 구현하지 않는다.
기존 ApiaryConfig의 크기 설정은 유지한다. 시작 건설도 0은 이번 요구의 고정 생성 규칙이다.
