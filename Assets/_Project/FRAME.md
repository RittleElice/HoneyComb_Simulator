# 소비장 소방: 값 배열 저장

한 소비장은 Front/Back 각 90x40, 총 7200개 논리 소방을 유지한다.
각 FrameSurface는 CellState[] 하나를 소유한다. 인덱스는 y*Width+x다.
CellState는 readonly struct이며 Content(byte enum) 하나만 가진다. X/Y/Side 또는 참조형 필드는 없다.
기본값은 Empty. Brood/Honey/Pollen은 최소 분류 이름만 정의했고 발육/먹이/봉개/조소 로직은 없다.
최종 상태 모델은 별도 설계 후 확장한다.

GetCell(side,x,y): 유효하면 nullable 값 복사본, 범위/면 오류면 null.
TryGetCell(side,x,y,out state): 성공 여부와 값 복사본 반환.
TrySetCell(side,x,y,state): 유효한 좌표에 복사 저장하고 true, 잘못된 주소면 false.
FrameSurface에도 좌표 기반 GetCell/TryGetCell/TrySetCell이 있다.
조회 결과는 참조가 아니므로 변경하려면 새 CellState를 TrySetCell로 저장해야 한다.
양면 배열은 독립적이다. 소비장 이동과 재삽입은 기존 Frame을 유지하므로 상태도 유지한다.

기본 Frame 생성 객체 수(ID 문자열 제외): Frame 1 + FrameSurface 2 + 상태 배열 2 = 5.
이전에는 위 5개에 CombCell 참조 객체 7200개가 추가되었다.
현재 소방 배열의 값 데이터는 7200바이트(약 7.03 KiB)이며 배열/객체 헤더는 별도다.
실제 Android 힙 사용량은 런타임별로 다르므로 프로파일러에서 측정해야 한다.
소비장 10장 기준 소방 저장 값은 72000바이트, Frame 관련 주요 객체는 50개다.
벌통/계상/슬롯/목록/문자열 객체는 이 수치에 포함하지 않았다.

기존 ApiaryConfig의 Frame Cell Width/Height 설정, 양면 표시, 계상/슬롯 UI는 유지된다.
Play 재시작 후 기존과 같은 소비장 면별 개수를 확인할 수 있다. 저장 데이터는 아직 없으므로 마이그레이션은 필요 없다.
