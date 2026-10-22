# 양봉장 + 최소 벌통

Play를 종료하고 HoneyComb > Setup Apiary System을 실행한다.
Settings/ApiaryConfig.asset이 생성되고 GameMasterData.apiaryConfig에 연결된다.
기존 연결과 값은 재실행해도 유지된다. 초기 설정은 Play 시작 시에만 적용한다.

ApiaryConfig: apiaryId=apiary-1, apiaryName=Test Apiary, hiveCapacity=10, initialHiveCount=1.
초기 벌통 수는 0 이상 수용량 이하이다. 수용량 0도 허용한다.
ApiaryState는 Unity에 의존하지 않으며 Tick을 구독하지 않는다.
양봉장에는 벌, 먹이, 내부 온도를 넣지 않는다. 벌통 내부와 Colony는 이후 별도로 모델링한다.
HiveState의 Active는 설치된 물리적 벌통을 뜻하고 봉군 생존을 뜻하지 않는다.

Add Hive는 수용량까지 생성한다. Remove Last Hive는 목록의 마지막 벌통을 삭제한다.
빈 목록과 수용량 한도에서는 해당 버튼이 비활성화된다.
삭제는 ID로 수행하며 다른 벌통의 ID는 바뀌지 않고 삭제된 ID도 재사용하지 않는다.
ID는 양봉장 ID/벌통 번호 형태다. 향후 여러 양봉장의 ID는 World 생성 시 유일성을 검증해야 한다.
저장 기능 구현 시 다음 벌통 번호도 보존해야 한다. 현재 저장 기능은 없다.
외부 코드에는 읽기 전용 목록을 노출해 수용량 검사 없이 목록을 변경하지 못하게 한다.

현재 실행에서는 Runner가 환경과 양봉장을 함께 소유한다. 외부 환경 원본은 기존 EnvironmentSystem 하나다.
Apiary에 환경값을 중복 저장하지 않는다. 향후 Hive Tick을 연결할 때 이 환경 스냅샷을 입력으로 전달한다.
벌통의 시간 계산은 아직 없으며 Clock과 Environment의 처리 흐름은 유지된다.

화면 아래 APIARY DEBUG에서 수용량, 개수, 벌통 ID와 상태를 확인한다.
패널 전체에 스크롤이 적용되어 긴 벌통 목록을 확인할 수 있다.
목록 1→10 증가, 한도에서 추가 차단, 10→0 감소, 빈 목록에서 삭제 차단을 확인한다.
Play 재시작 시 마스터데이터 초기 상태로 돌아간다.
