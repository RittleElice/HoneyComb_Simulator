# 벌 저장소·발육·우화·개발용 산란

- BeeState는 값 데이터(struct), BeeStore의 배열에 원본을 보관합니다. 위치·봉군 목록은 ID만 보관하고 읽을 때 최신 값을 조회합니다. TryGetBee/Bees에서 읽은 값은 스냅샷이며 다음 시간의 상태를 보려면 다시 조회합니다.
- 발육량은 double이며 단계는 품종별 기준으로 계산합니다. 1시간 Tick마다 발육량을 추가합니다. 나이(산란 이후 경과 시간)와 발육량은 별개입니다.
- 알로 생성한 벌은 LaidHour가 있습니다. 개발용으로 유충/번데기/성충을 바로 넣으면 산란 시점은 알 수 없으므로 null입니다. 해당 단계 시작 발육량으로 생성됩니다.
- 우화 시 ID/ColonyId 유지, OccupantBeeId 해제, 소방 Empty 전환, 같은 소비장 같은 면의 표면으로 이동합니다. 소비장 이동 후에도 현재 소비장을 따라갑니다.
- 현재 소속 봉군은 자동 변경하지 않습니다. 입양, 유전, 사망, 먹이, 온도 영향과 봉개는 이번 범위에 없습니다.

## 마스터데이터
GameMasterData > Bee Defaults 아래 Worker/Queen/Drone의 Egg Hours, Larva Hours, Pupa Hours와 Development Per Hour를 변경합니다. Play 시작 시 복사되므로 변경 후 Play를 다시 시작합니다.

|종류|알|유충|번데기|합계|
|---|---:|---:|---:|---:|
|일벌|72시간|132시간|300시간|504시간(21일)|
|여왕|72시간|120시간|192시간|384시간(16일)|
|수벌|72시간|156시간|348시간|576시간(24일)|

기본값 출처: Virginia Tech, Rick Fell, Honey Bee Biology, 8페이지의 Egg-to-Adult 도표.
https://carroll.ext.vt.edu/content/dam/carroll_ext_vt_edu/fell_class1_hb_biology_basics.pdf
발육 일수의 대표값을 사용한 단순 모델이며 영양·온도·왕대 크기는 아직 반영하지 않습니다.

## 확인 방법
1. Play 시작 후 자동 시간을 일시정지합니다.
2. 소비장 소방 하나를 100% 건설합니다. Bees 화면에서 Worker/Egg로 그 소방에 벌을 추가합니다.
3. 시간을 72시간 진행하면 Larva, 총 204시간이면 Pupa, 총 504시간이면 Adult입니다.
4. 같은 Bee ID와 봉군을 유지하면서 소방이 비고 소비장 표면 목록에 성충이 표시되는지 확인합니다.

## 여왕 산란
- 성충 Queen 추가 후 상세 화면에서 Enable prototype laying (assume mated)를 누릅니다. 이는 개발용 교미 완료 가정이며 실제 교미 구현이 아닙니다. 새로 우화한 여왕은 자동 활성화되지 않습니다.
- Lay 1 worker egg 버튼: 현재 여왕이 있는 벌통의 완성된 빈 소방에 일벌 알 하나를 생성합니다. 아래 계상부터 슬롯 순서, Front/Back, Y/X 순서로 탐색합니다.
- 자동으로 낳게 하려면 Play 전에 GameMasterData의 Automatic Queen Laying도 켭니다. Eggs Per Queen Hour 기본값 1은 개발용 수치입니다. 식량·계절·여왕 나이는 아직 반영하지 않습니다.
- 외부의 여왕, 비활성 여왕, 빈 소방이 없는 벌통은 산란하지 않습니다. 다른 벌통으로 이동한 여왕의 알은 여왕의 기존 봉군 소속으로 생성됩니다.
- 새 알은 생성된 Tick에는 발육하지 않고 다음 Tick부터 발육합니다.

## 검증
순수 C# 회귀 검증 52개 통과. 단계 경계, 소수/정지 속도, 종류별 발육, 우화 점유 해제, 이송 후 우화, 산란 제한, Tick 순서, 초기 시간, 1,000개체 동시 우화 포함.
Unity 참조 어셈블리 기반 코드 컴파일 통과. Unity 에디터 실제 화면·Android 기기 실행 검증은 별도입니다.
