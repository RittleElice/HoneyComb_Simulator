# 벌통의 물리 구조

새 벌통은 단상 1개, 계상 0개다. 단상은 항상 존재하며 Entrance가 Outside 연결을 나타낸다.
아직 벌 이동 로직은 없다. 계상은 AddSuper로 맨 위에 추가한다.
Supers 목록은 아래→위 순서다. 빈 계상은 어느 층이든 제거할 수 있고 나머지의 상대 순서와 ID는 유지한다.
각 계상은 정확히 10개 슬롯(0~9)을 가진다. 10은 이 물리 규격의 고정값이다.
초기 계상/소비장은 없으며 자동 생성에 사용할 밸런스 수치를 추가하지 않았다.
FrameSlot.CanHostBees는 Frame이 있을 때만 true다. 단상은 항상 사용 가능하다.
Frame은 ID와 배치 소유권만 갖는다. HiveBase/Entrance/FrameSlot은 미래 논리 위치를 나타내는 공간이다.
Colony/Bee/Comb/소방 Cell 및 실제 경로 계산은 구현하지 않는다.

InsertFrame/RemoveFrame/MoveFrame은 계상 ID와 슬롯 index로 호출한다.
잘못된 ID/index, 찬 목적지, 빈 출발지, 중복 배치를 false로 거부한다.
MoveFrame은 같은 벌통 내부의 서로 다른 계상 사이도 지원한다. 같은 칸 이동은 false다.
계상을 제거하려면 모든 슬롯이 비어 있어야 한다.
TryCreateFrame으로 만든 Frame ID와 계상 ID는 삭제 후 재사용하지 않는다.
제거한 Frame의 참조를 보유한 경우 InsertFrame으로 재삽입할 수 있다.
Frame을 직접 수정해서 칸을 변경할 수 없으며 HiveState API가 소유권을 관리한다.

Unity: Play를 재시작하고 APIARY GRID에서 H가 있는 칸을 선택한다.
아래 HIVE STRUCTURE에서 Add Super on top → 계상 슬롯 클릭 → Insert new Frame.
F는 소비장 존재, . 은 빈 슬롯. Bee space 상태가 함께 표시된다.
이동은 소비장 슬롯 선택 → Move: select this Frame → 빈 슬롯 선택 → Move Frame here.
Remove Frame으로 모두 비우면 Remove selected empty Super 버튼을 사용할 수 있다.
벌통 선택을 바꿔도 소비장 이동 원본은 유지된다. 원본 벌통/소비장이 제거되거나 바뀌면 취소된다.
격자 내 벌통 이동은 동일한 Hive 객체를 옮기므로 계상/소비장도 그대로 유지된다.
현재 격자의 Remove Hive는 단상 및 내부 구조를 포함한 벌통을 양봉장 목록에서 제거한다. 저장/장비 보관은 아직 없다.

다른 벌통 이동: 원본 슬롯 → Move: select this Frame → 격자에서 다른 벌통 → 빈 슬롯 → Move Frame here.
MoveFrameTo는 목적지를 먼저 검증하고 양쪽 슬롯을 함께 갱신한다. 실패하면 원본을 유지한다.
소비장 ID, 크기, 앞뒤 상태 배열은 그대로 유지한다. ID의 원래 벌통 접두어는 현재 위치를 뜻하지 않는다.

벌 모델 추가: 벌이 있는 Frame의 RemoveFrame과 봉군/거주 벌이 있는 Hive의 RemoveHive는 참조 보존을 위해 거부한다. 소비장 이동은 가능하며 개체는 같은 Frame 위치를 유지한다. 자세한 규칙은 BEES.md 참고.
