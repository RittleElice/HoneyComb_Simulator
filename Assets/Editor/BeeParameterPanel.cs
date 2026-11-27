using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only presentation. No runtime settings or serialization changes.
internal static class BeeParameterPanel
{
    sealed class Group {
        public string title,fields,formulas,note;
        public Group(string t,string f,string x,string n){title=t;fields=f;formulas=x;note=n;}
    }
    static readonly Group[] groups={
        new Group("작업에 따른 욕구 해소","workDesireEnabled|작업 해소 사용 (0=비교, 1=사용);workBuildRelief|건설 기본 작업량당 감소;workNurseRelief|육아 기본 먹이량당 감소;workHeatRelief|발열 1틱당 감소;workClearRelief|정리 기본 운반량당 감소;workLayRelief|산란 1회당 감소;workNectarRelief|화밀 기본 채집량당 감소;workPollenRelief|화분 기본 채집량당 감소","workDesireRelief","실제 처리량만 반영합니다. ratio=실제 작업량/기본 속도, actionRate=행동별 감소 계수. 육아는 당분·화분 전달량 합계. 산란은 성공한 알 1개 단위입니다. 욕구 0 또는 목표 해소 시 종료하며 기존 시간 중단도 유지합니다."),
        new Group("봉군 수요 · 기준과 소비 추정","demandEnabled|수요 방식 사용 (0=이전, 1=새 방식);demandFailureWindow|실패 이력 창 (틱);demandSugarDays|당분 최소 비축 (일);demandPollenDays|화분 최소 비축 (일);demandSugarReserveDays|당분 기회 비축 (일);demandPollenReserveDays|화분 기회 비축 (일);demandStorageDays|저장 용량 여유 (일);demandSupplyMinutes|공급 풍부도 비교 시간 (분);demandWorkerMinimum|일벌 기준 최솟값;demandWorkersPerCell|완성 소방당 일벌 기준;demandBroodPerWorker|일벌당 봉충 목표;demandBroodPerCell|완성 소방당 봉충 목표;demandEmptyMinimum|빈 소방 목표 최솟값;demandEmptyPerWorker|일벌당 빈 소방 목표;demandGrowthSugarFloor|성장 당분 하한 (일);demandGrowthSugarRange|성장 당분 증가 구간 (일);demandGrowthPollenFloor|성장 화분 하한 (일);demandGrowthPollenRange|성장 화분 증가 구간 (일);demandBuildSugarFloor|건설 요청 당분 하한 (일);demandDailyAdultSugar|성충당 하루 당분 (mg/대표 개체);demandDailyBroodSugar|봉충당 하루 당분 (mg/대표 개체);demandDailyAdultPollen|성충당 하루 화분 (mg/대표 개체);demandDailyBroodPollen|봉충당 하루 화분 (mg/대표 개체);demandForecastForagerFraction|공급 기준 채집 일벌 비율","demandDays demandRatio demandWorkerTarget demandBroodTarget demandEmptyTarget demandDailySugar demandDailyPollen demandSupplyTarget","개체 수는 대표 개체 수이며 여왕은 성충 한 개체로 포함합니다. 하루 소비=성충 수×성충 계수+봉충 수×봉충 계수. 소비 추정은 실제 기아로 줄어들지 않습니다."),
        new Group("봉군 수요 · 증가와 감소","demandFoodShortageGain|먹이 부족 자극;demandFoodOpportunityGain|공급 기회 자극;demandFailureGain|실패 추가 자극;demandBuildGain|건설 공간 부족 자극;demandClearGain|정리 공간 부족 자극;demandLayWorkerGain|산란 일벌 부족 자극;demandLayBroodGain|산란 봉충 부족 자극;demandForageDecay|봉군 채집 감소/틱;demandBuildDecay|봉군 건설 감소/틱;demandLayDecay|봉군 산란 감소/틱;demandClearDecay|봉군 정리 감소/틱","demandFoodSignal demandForageIncrease demandBuildIncrease demandClearIncrease demandLayIncrease demandGrowthSugar demandGrowthPollen demandNext demandProbability","수요는0~100, 발생 확률은 수요%. 필요 발생·작업 완료에 따른 차감 없이 자연감소합니다. 실패는 최근 창의 고유 개체 비율입니다."),
        new Group("격자 수요 · 자극과 전달 범위","demandRegionBuildPulse|격자 건설 요청 자극;demandRegionClearPulse|격자 정리 요청 자극;demandRegionBuildDecay|격자 건설 감소/틱;demandRegionClearDecay|격자 정리 감소/틱;demandRegionNurseGain|격자 육아 부족 자극;demandRegionHeatGain|격자 발열 부족 자극;demandRegionNurseDecay|격자 육아 감소/틱;demandRegionHeatDecay|격자 발열 감소/틱;demandBuildBroadcast|건설 전체 전달 기준;demandClearBroadcast|정리 전체 전달 기준;demandNurseBroadcast|육아 전체 전달 기준;demandHeatBroadcast|발열 전체 전달 기준;demandCandidateLoadScale|격자 기존 수요 억제 기준;demandCandidateWorkFloor|건설 후보 작업량 분모 최솟값;demandCandidateFoodFloor|정리 후보 먹이량 분모 최솟값","demandNurseIncrease demandHeatIncrease demandBuildCandidate demandClearCandidate demandLayCandidate demandReservedCells","임계값 이상이면 같은 봉군 전체 일벌에게 전달하고, 미만이면 목표 격자만 자극합니다. 목표 위치는 바뀌지 않습니다. 후보 수식 amount=확보 소방 수, available=작업량 또는 옮길 먹이mg, need=기존 격자 수요입니다."),
        new Group("명령 펄스 · 개체 욕구","demandCommandPulse|명령 1회 개체 욕구 자극;demandDesireDecay|개체 욕구 매 틱 감소","demandPulse demandDesireNext","명령은 한 틱만 발생합니다. 모든 누적 명령 욕구는 매 틱 먼저 감소한 뒤 새 자극을 더합니다. 같은 행동·목표에 합산하며 상한은 없습니다."),
        new Group("대표 개체 배율","representativeScale|일벌·봉충 한 개체의 실제 벌 수","","여왕은 1마리입니다. 아래 개체별 mg·J 값은 실제 한 마리 기준이며 계산 시 배율을 적용합니다. Energy·Health·발달도·수명·시간은 확대하지 않습니다. 초기 저장량은 배율 적용 후의 실제 총량입니다. 건설 비용은 이미 축소 소방 1포인트 기준이므로 배율을 다시 곱하지 않습니다."),
        new Group("이전 방식 비교 · 명령과 욕구","naturalDecay|무자극 틱 자연 감소;commandMaximum|명령 최대 자극;interruptPerElapsedMinute|경과 시간당 중단 계수","deficit commandChance commandStimulus desireNext interruptChance","수요 방식 사용=0에서만 쓰는 이전 명령 규칙입니다. 새 방식의 자극과 매 틱 감소는 명령 펄스 항목을 사용합니다."),
        new Group("섭식","feedWeight|섭식 욕구 강도;reserveTarget|체내 당분 목표 (mg);feedRate|섭식 속도 (mg/분);feedDevelopment|섭식 발달도 증가 (/분)","feedDesire feedAmount","보유 당분을 체내 당분으로 옮깁니다. 당분 획득 속도는 자원 획득·운반 항목을 사용합니다."),
        new Group("자리 이동 · 온도와 밀집","relocateEnabled|사용 (0/1);relocateColdStart|저온 이동 시작 (℃);relocateColdStop|저온 해소 (℃);relocateHotStop|고온 해소 (℃);relocateHotStart|고온 이동 시작 (℃);relocateDensityStart|밀집 이동 시작 (실제 성충/mL);relocateDensityStop|밀집 해소 (실제 성충/mL);relocateTemperatureWeight|온도 욕구 가중치;relocateCrowdingWeight|밀집 욕구 가중치;relocateTemperatureScale|온도 욕구 정규화 폭 (℃);relocateDistanceMm|목적지 거리 기준 (mm);relocateMinImprovement|최소 불편 점수 개선;relocateMinEnergyFraction|이동 시작 최소 Energy 비율","relocateTemperatureDesire relocateCrowdingDesire relocateDestination","내부 성충이 대기·휴식 중일 때 재배치를 시작합니다. 명령 누적 욕구와 별개인 상태 욕구입니다. 온도는 현재 공기 온도, 밀집은 같은 공기 격자의 실제 대표 성충 수/공기 mL이며 이동 목적지 예약도 반영합니다. 이동 가능한 인접 격자에서 온도·밀집의 합산 불편이 줄어드는 칸을 고릅니다. 목적지 비교에서는 욕구 상한을 넘은 불편도 구분하고, 도착·예약 인원을 포함한 밀집도가 현재와 해소 기준보다 모두 높아지는 칸은 제외합니다. 도착할 때마다 다시 판단하며, 더 나은 이웃이 없거나 불편이 해소되면 휴식으로 돌아갑니다. 작업 중 임의 이동과 냉각 행동은 추가하지 않습니다."),
        new Group("휴식","restWeight|휴식 욕구 강도;maxEnergy|최대 Energy;restRate|Energy 회복 속도 (/분);energyPerSugar|당분당 Energy (Energy/mg);restDevelopment|휴식 발달도 증가 (/분)","restDesire restAmount","체내 당분을 소비해 Energy를 회복합니다."),
        new Group("건강 회복","recoverWeight|회복 욕구 강도;maxHealth|최대 Health;recoveryRate|Health 회복 속도 (/분);recoveryEnergy|회복당 Energy 비용;recoverDevelopment|회복 발달도 증가 (/분)","recoverDesire recoverAmount","Energy를 소비해 Health를 회복합니다."),
        new Group("양봉장 이동 · 방향과 귀환","directionRoutingEnabled|공유 방향표 사용 (0/1);apiaryMovementEnabled|양봉장 이동 사용 (0/1);apiaryWaitPercent|제자리 대기 (%);apiaryFollowDirectionPercent|목적지 안내 방향 선택 (%);apiaryOtherDirectionPercent|다른 방향 선택 (%);apiaryArrivalStopPercent|귀환 거리 추첨 종료 확률 (%);apiaryCellMeters|양봉장 한 칸 크기 (m);apiaryFlightMetersPerSecond|비행 속도 (m/초)","","이동 확률 합계는 100%입니다. 상하좌우 이동하며 한 칸 이동 또는 대기 후 다시 선택합니다. 귀환은 거리 0부터 종료 확률을 적용한 택시거리의 칸 중 균등 선택하고 최대 거리로 제한합니다. 종료 확률은 0 초과 100 이하입니다. 보관 중 장비의 벌은 같은 소비장 안에서만 이동합니다."),
        new Group("이동","speed|이동 속도 (mm/초);movementEnergy|이동 Energy 비용 (/분);moveDevelopment|이동 발달도 증가 (/분);routeCacheEntries|경로 캐시 항목 수","moveDistance movementEnergyCost","다른 Task의 하위 행동입니다. 수식 time은 해당 틱에서 사용하는 시간 비율입니다."),
        new Group("자원 획득 · 운반","carryCapacity|화밀 유래 당분 용량 (mg);pollenCarryCapacity|화분 용량 (mg);acquireRate|자원 획득 속도 (mg/분);acquireDevelopment|획득 발달도 증가 (/분)","acquireAmount","당분과 화분은 독립 용량입니다. 화밀 수분은 획득 시 폐기합니다. 획득 속도는 정리 행동에서도 공유합니다."),
        new Group("외부 채집 · 적재량 귀환","stockSugarPerBee|성충당 당분 비축 목표 (mg);stockPollenPerBee|성충당 화분 비축 목표 (mg);forageLoadBased|적재량 방식 사용 (0/1);forageSugarRate|당분 수집 속도 (mg/분/실제 벌);foragePollenRate|화분 수집 속도 (mg/분/실제 벌);forageReturnExponent|귀환 확률 지수;forageShortageReturnRate|완전 부족 시 추가 귀환 확률 (/분);forageDepartureExponent|적재량에 따른 출발 억제 지수;forageDevelopment|채집 발달도 증가 (/분)","forageSourceWeight forageCollectSugar forageCollectPollen forageReturnChance forageShortageReturnChance forageDepartureWeight","매분 실제 밀원을 요구량 비례 배분한 뒤 해당 자원의 적재율로 귀환을 판단합니다. 가득 차면 귀환합니다. 요구량 대비 실제 확보량의 부족률×20%를 기본 추가 귀환 확률로 사용합니다. 귀환하지 않으면 대체 밀원을 찾고, 없으면 밖에서 다음 틱에 다시 탐색합니다. 외부에서는 시간 기반 무작위 중단을 적용하지 않습니다. 누적 명령 욕구는 보존하고 채집 선택 가중치만 적재율로 줄입니다."),
        new Group("외부 채집 · 이전 시간 방식 비교","forageMinutes|이전 방식 외부 채집 시간 (분);forageSugar|이전 방식 당분 요청 상한 (mg);foragePollen|이전 방식 화분 요청 상한 (mg)","forageDuration forageSugarAmount foragePollenAmount","적재량 방식 사용=0일 때만 적용합니다. 기본 적재량 방식에서는 이 값으로 귀환하거나 한꺼번에 채집하지 않습니다."),
        new Group("저장","storeWeight|저장 욕구 강도;storeRate|저장 속도 (합계 mg/분);storageHeightBias|높이 선호 강도;storageHeightScale|높이 정규화 기준;storageDistanceMm|거리 정규화 기준 (mm);storeDevelopment|저장 발달도 증가 (/분)","storeDesire storagePreference storeAmount","저장 욕구 ratio는 두 자원 적재율의 최댓값입니다. 운반 용량은 자원 획득·운반 항목과 공유합니다."),
        new Group("건설","desiredEmptyCellsPerBee|성충당 목표 빈 소방 수;buildPoints|건설 속도 (포인트/분);sugarPerBuildPoint|건설 비용 (당분 mg/포인트);buildStockSugarPerBee|신규 명령 비축 기준 (mg/성충);buildDevelopment|건설 발달도 증가 (/분)","buildPreference buildCellWeight buildAmount","이전 방식에서만 신규 명령에 저장 당분 > 성충 수 × 비축 기준을 검사합니다. 새 방식은 봉군 수요의 건설 당분 비축 일수를 사용합니다. 기존 명령은 이 조건으로 종료하지 않습니다. 완성 판정 80, 남은 작업량·배분 상한 100은 기존 소방 규칙입니다."),
        new Group("산란","queenLayPerMinute|산란 기본 확률 (/분);broodTarget|목표 봉충 수;layDevelopment|산란 발달도 증가 (/분)","layChance layCellWeight","봉충 목표 수는 정리 명령도 공유합니다. 온도 조건은 봉충 발육·육아 항목의 broodTemperature를 사용합니다."),
        new Group("봉충 발육 · 육아","broodTick|기본 봉충 발달도 증가 (/분);eggEnd|알 단계 종료 발달도;larvaEnd|애벌레 단계 종료 발달도;adultStart|성충 우화 발달도;broodMinTemperature|최저 발육 온도 (℃);broodIdealTemperature|최적 발육 온도 (℃);broodMaxTemperature|최고 발육 온도 (℃);larvaSugarPerTick|애벌레 당분 요구 (mg/분);larvaPollenPerTick|애벌레 화분 요구 (mg/분);larvaEnergyPerTick|애벌레 Energy 요구 (/분);broodReserveTicks|애벌레 먹이 비축 기준 (틱);nurseRate|육아 공급 속도 (mg/분);nurseDevelopment|육아 발달도 증가 (/분)","broodTemperature broodDevelopment larvaDevelopmentFactor nurseNeed nurseAmount","당분과 화분은 각각 부족량에 비례해 분배합니다. 각 단계의 발달도와 온도 수식은 초기 봉충 설정에도 연결됩니다."),
        new Group("가온 · 기초 발열","basalHeatJ|성충 기초 발열 (J/분);eggBasalHeatJ|알 기초 발열 (J/분);larvaBasalHeatJ|유충 기초 발열 (J/분);pupaBasalHeatJ|번데기 기초 발열 (J/분);broodThermalCoupling|봉충 소비장 결합 (0=기존 공기, 1=소비장);extraHeatJ|가온 추가 발열 (J/분);heatEnergy|가온 Energy 비용 (/분);heatDeficitScale|온도 부족 정규화 (℃);heatDevelopment|가온 발달도 증가 (/분)","basalHeat broodBasalHeat additionalHeat heatNeed","가온 목표 온도는 봉충 발육의 최적 발육 온도와 공유합니다. 기초 발열은 가온 행동과 별도로 적용됩니다. 봉충 발열은 단계별 임시 평균값이며, 소비장 결합 시 봉충·가온 열을 소비장 격자에 공급하고 같은 격자의 온도로 발육·가온 명령을 판단합니다. 성충 발열은 주변 공기로 공급합니다. 봉충 추가 열용량은 임시로 0이며 발열은 유지합니다."),
        new Group("저장 자원 정리","clearDevelopment|정리 발달도 증가 (/분);acquireRate|획득 속도 (공유 mg/분);broodTarget|봉충 목표 수 (공유)","","산란 공간 확보용 정리입니다. 획득한 자원의 재저장은 별도 저장 Task가 담당합니다."),
        new Group("기초 대사 · 발달 · 사망","basalSugar|기초 당분 소비 (mg/분);basalEnergy|기초 Energy 소비 (/분);starvationDamage|기아 Health 손상 (/분);maxDevelopment|일벌 사망 발달도;queenMaxDevelopment|여왕벌 사망 발달도;idleDevelopment|Idle 발달도 증가 (/분)","basalSugarCost basalEnergyCost starvation development","행동별 발달 속도는 각 행동 항목에 있습니다. Health 0 또는 최대 발달도 도달 시 사망합니다."),
        new Group("새 게임 초기 상태","demoWorkers|초기 일벌 수;demoEggs|초기 알 수;demoLarvae|초기 애벌레 수;demoPupae|초기 번데기 수;demoFrames|초기 소비장 수;demoConstruction|초기 미완성 소방 건설도;demoSugar|초기 저장 당분 (mg);demoWater|초기 저장 수분 (mg);demoPollen|초기 저장 화분 (mg);forageSourceId|초기 먹이 화원 ID","","새 게임 생성에만 사용합니다. 진행 중 월드에 적용해도 개체·먹이를 추가하거나 소방을 재설정하지 않습니다."),
        new Group("호환용 · 현재 사용하지 않음","forageWater|구형 고정 채집 수분 (미사용)","forageWaterAmount","기존 저장·JSON 호환용 값입니다. 현재 채집에서는 실제 밀원 수분 비율을 사용하므로 편집을 비활성화했습니다.")
    };
    static readonly Dictionary<string,bool> expanded=new Dictionary<string,bool>();
    static string search="";
    static bool Match(string text)=>string.IsNullOrWhiteSpace(search)||text.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0;
    public static void Draw(BeeCycleSettings draft)
    {
        float oldWidth=EditorGUIUtility.labelWidth;
        try{EditorGUIUtility.labelWidth=Mathf.Clamp(EditorGUIUtility.currentViewWidth*0.55f,150f,300f);DrawContents(draft);}
        finally{EditorGUIUtility.labelWidth=oldWidth;}
    }
    static void DrawContents(BeeCycleSettings draft)
    {
        search=EditorGUILayout.TextField("검색 (행동·항목·수식 이름)",search);
        using(new EditorGUILayout.HorizontalScope()){
            if(GUILayout.Button("모두 펼치기"))foreach(var g in groups)expanded[g.title]=true;
            if(GUILayout.Button("모두 접기"))foreach(var g in groups)expanded[g.title]=false;
        }
        var seenFields=new HashSet<string>();var seenFormulas=new HashSet<string>();
        foreach(var g in groups){
            var fields=g.fields.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries);
            var formulas=g.formulas.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            foreach(var entry in fields)seenFields.Add(entry.Split('|')[0]);foreach(var name in formulas)seenFormulas.Add(name);
            bool whole=Match(g.title);
            if(!whole&&!fields.Any(Match)&&!formulas.Any(Match))continue;
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox)){
                if(!expanded.TryGetValue(g.title,out bool open))open=g==groups[0];
                bool searching=!string.IsNullOrWhiteSpace(search);
                open=EditorGUILayout.Foldout(searching||open,g.title,true);expanded[g.title]=open;
                if(!open)continue;
                EditorGUILayout.HelpBox(g.note,MessageType.None);
                using(new EditorGUI.DisabledScope(g==groups[groups.Length-1])){
                    foreach(var entry in fields)if(whole||Match(entry)){var parts=entry.Split('|');DrawField(draft,parts[0],parts[1]);}
                    foreach(var name in formulas)if(whole||Match(name))DrawFormula(draft,name);
                }
            }
        }
        // Newly added or custom settings must never disappear from the editor.
        var otherFields=typeof(BeeCycleSettings).GetFields().Where(f=>(f.FieldType==typeof(double)||f.FieldType==typeof(int))&&!seenFields.Contains(f.Name)&&f.Name!="apiaryReturnRadius"&&f.Name!="apiaryReturnDistanceExponent").ToArray();
        var otherFormulas=draft.formulas.Where(f=>!seenFormulas.Contains(f.name)).ToArray();
        if(otherFields.Length+otherFormulas.Length>0){
            EditorGUILayout.LabelField("기타 · 추가 항목",EditorStyles.boldLabel);
            foreach(var f in otherFields)if(Match(f.Name))DrawField(draft,f.Name,f.Name);
            foreach(var f in otherFormulas)if(Match(f.name))DrawFormula(draft,f.name);
        }
    }
    static void DrawField(BeeCycleSettings draft,string name,string label){
        var field=typeof(BeeCycleSettings).GetField(name);
        if(field==null){EditorGUILayout.HelpBox("설정 항목 없음: "+name,MessageType.Warning);return;}
        var caption=new GUIContent(label,name);
        if(field.FieldType==typeof(double))field.SetValue(draft,EditorGUILayout.DoubleField(caption,(double)field.GetValue(draft)));
        else if(field.FieldType==typeof(int))field.SetValue(draft,EditorGUILayout.IntField(caption,(int)field.GetValue(draft)));
    }
    static void DrawFormula(BeeCycleSettings draft,string name){
        var f=draft.formulas.Find(x=>x.name==name);
        if(f==null){EditorGUILayout.HelpBox("수식 없음: "+name,MessageType.Warning);return;}
        EditorGUILayout.LabelField("수식 · "+name,EditorStyles.miniBoldLabel);
        f.expression=EditorGUILayout.TextField(f.expression);
    }
}
