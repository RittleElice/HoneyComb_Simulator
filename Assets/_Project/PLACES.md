# 벌 위치: 단상 / 양봉장 야외 / 외부 지역

위치 종류:
- HiveBase: HiveId. 출입구가 있는 단상. 실제 출입 경로/이동 시간은 아직 없다.
- ApiaryOutdoor: ApiaryId + X/Y. 양봉장 격자 안의 야외 위치. 벌통과 같은 좌표여도 내부 위치와 별개다.
- Outside: RegionId. 양봉장 밖의 환경 지역 하나. 좌표는 없다.
- FrameSurface: FrameId + Side. 성충의 소비장 표면 위치.
- BroodCell: FrameId + Side + X/Y. 봉충 위치.

ApiaryConfig의 External Region Id / Name이 양봉장 외부 지역의 마스터데이터다.
기존 EnvironmentState는 기온/습도 등 조건이다. ExternalRegionState는 장소 정체성이며 별도로 유지한다.
현재 자원량, 채집, 날씨 차이, 거리, 비행 경로/시간을 구현하지 않는다.

UI: Play 재시작 → 양봉장 격자 아래 BEE PLACES / OUTDOOR.
Hive Base는 선택한 벌통 단상, Apiary X/Y는 현재 선택 좌표 야외, Outside는 외부 지역이다.
Add 1 Adult Bee here: 먼저 격자에서 소속 본거지 벌통을 선택한다. 빈 좌표로 바꿔도 마지막 본거지 선택은 유지된다.
이후 생성되는 벌의 봉군은 화면에 표시된 New Bee's colony home 기준이다. 외부 배치가 무소속 벌을 만들지는 않는다.
일벌/여왕/수벌 선택 가능. 건강은 기존 GameMasterData의 벌 생성 기본값에서 가져온다.
각 위치에 벌 수와 Previous/Next 개체 확인 버튼이 있다.
Select this Bee to move → 목적지 탭/격자/소비장 선택 → Move selected Bee here.
소비장 화면도 동일한 이동 선택을 공유하므로 단상/야외/외부와 왕복 이동할 수 있다.
봉충은 기존 소방에만 생성되고 이 수동 이동 기능을 사용할 수 없다.

BeePopulation이 위치 변경의 단일 진입점이다.
TryMoveAdult는 벌, 단계, 출발지, 목적지를 모두 검사한 뒤 기존 인덱스 제거→위치 변경→새 인덱스 등록을 수행한다.
실패 시 위치/소속/목록/거주 수를 바꾸지 않는다. 같은 위치로 이동은 성공하는 무변경 처리다.
생성은 TryAddAdult 또는 기존 TryAddBee를 사용한다. 모든 생성이 같은 저장·위치 등록 경로를 거친다.
성충의 봉군 소속/건강/생성 시각은 이동으로 바뀌지 않는다.

조회:
TryGetBee(id): ID 조회.
GetBeesAt(location): 정확한 논리 위치의 목록(소비장 표면이면 성충만).
GetBeesInBase(hiveId): 단상.
GetBeesOutdoors(x,y): 현재 양봉장의 야외 격자 좌표.
GetBeesOutside(): 외부 지역.
GetBeesOnFrame/GetBeesOnSurface: 소비장 전체/면의 성충과 봉충을 함께 집계.
GetBeeInCell: 봉충 개체 조회.
목록은 읽기 전용이며 비어 있던 위치는 생성/이동 후 다시 조회한다.

거주 벌이 있는 단상/소비장은 벌통 삭제로 유실되지 않도록 제거를 막는다.
야외 좌표의 벌은 해당 좌표의 벌통을 이동해도 원래 야외 좌표에 남는다.
단상의 벌은 HiveId로 위치를 지정하므로 벌통이 격자에서 이동해도 따라간다.
소비장 이동도 FrameId를 유지하므로 해당 벌은 그대로 따라간다.
외부 출역 후에도 원래 봉군 소속은 유지되며 합봉/편입 기능은 아직 없다.
