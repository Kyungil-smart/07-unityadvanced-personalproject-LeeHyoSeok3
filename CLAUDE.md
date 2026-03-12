# CLAUDE.md

## 규칙
- 모든 응답은 반드시 한국어로 작성할 것
- 코드 주석도 한국어로 작성할 것
- 파일 수정/생성 시 설명도 한국어로 할 것
- 지시사항을 받으면 구현 계획/설계를 먼저 제안하고, 사용자 승인 후에 실제 구현 진행할 것

## 프로젝트 개요

**게임 타입**: 2D 탑다운 타워디펜스 / 자원관리 하이브리드 RTS
**플랫폼**: Unity (Windows Standalone)
**언어**: C# (주석은 한국어)

### 게임 루프
1. **준비 페이즈(Prepare)**: 자원 수집(나무/금/고기), 건물 건설, 워커 배치
2. **전투 페이즈(Combat)**: 적 웨이브 방어, 건물 유지비 지불
3. **승리/패배**: Castle 파괴 → GameOver, 모든 웨이브 클리어 → Clear

---

## 아키텍처

### 핵심 패턴 (2가지)

**1. Event Bus (발행-구독)**
```csharp
// 발행
EventBus.Publish(new OnResourceChanged { type = ResourceType.Wood, current = 50, delta = 10 });
// 구독
EventBus.Subscribe<OnResourceChanged>(handler);
EventBus.Unsubscribe<OnResourceChanged>(handler);
```
- 시스템 간 직접 의존성 제거
- 20+ 이벤트 타입 정의 (EventBus.cs)

**2. Service Locator (의존성 컨테이너)**
```csharp
ServiceLocator.Register<ResourceInventory>(this);
var inventory = ServiceLocator.Get<ResourceInventory>();
```
- 등록된 서비스: GameManager, PhaseManager, ResourceInventory, BuildingPlacer, PathfindingGrid, WorkerAssigner, BuildingMaintenance, ConstructionAssigner, Castle, PhaseResetter

### 추가 패턴
- **Singleton**: GameManager, PhaseManager
- **State Machine**: UnitStateMachine (18개 상태)
- **Template Method**: BuildingBase, ResourceNode, UnitBase (abstract 훅 메서드)
- **ScriptableObject**: BuildingData, UnitData (설정 데이터)
- **Component 조합**: BuildingConstruction, WorkerEnergy, FloorObject

---

## 시스템별 주요 파일

### 게임 상태 (Core/)
| 파일 | 역할 |
|------|------|
| `GameManager.cs` | 게임 상태 머신 (Ready→Playing→GameOver/Clear) |
| `PhaseManager.cs` | Prepare↔Combat 페이즈 전환 |
| `GameState.cs` | GameState, PhaseType, ScoreType 열거형 |
| `EventBus.cs` | 전역 이벤트 디스패처 |
| `ServiceLocator.cs` | 의존성 컨테이너 |

### 자원 (Resource/)
| 파일 | 역할 |
|------|------|
| `ResourceInventory.cs` | 플레이어 자원 관리 (Wood/Gold/Meat, Meat는 용량 제한) |
| `ResourceNode.cs` | 자원 노드 추상 기반 (최대 워커 1명) |
| `TreeNode.cs` / `GoldNode.cs` / `AnimalNode.cs` | 자원 노드 구현체 |
| `AnimalNode.cs` | AI 이동 (배회→풀뜯기→도주), 물리 기반 |
| `DroppedResource.cs` | 수확 후 워커가 수거하는 중간 자원 |

### 건물 (Building/)
| 파일 | 역할 |
|------|------|
| `BuildingBase.cs` | 건물 추상 기반 (HP, 유지비, 파괴) |
| `BuildingData.cs` | ScriptableObject (건설비용, 유지비, 생산 설정) |
| `BuildingConstruction.cs` | 건설 진행 컴포넌트 (워커 자동 배정, 시간 진행) |
| `BuildingPlacer.cs` | 건물 배치 프리뷰 및 검증 (5점 샘플링) |
| `BuildingMaintenance.cs` | 웨이브 시작 시 유지비 징수 |
| `ProductionBuilding.cs` | 유닛 생산 건물 |
| `Castle.cs` | 본부 (HP슬라이더, 파괴→GameOver) |
| `Cluster.cs` | 초기 워커 스포너 (파괴 불가) |
| `ConstructionAssigner.cs` | 건물 배치 시 최근접 워커 배정 |
| `PhaseResetter.cs` | 전략 전체 초기화 (준비 페이즈에서만) |

### 유닛 (Unit/)
| 파일 | 역할 |
|------|------|
| `UnitBase.cs` | 유닛 추상 기반 (이동, 데미지, 사망) |
| `UnitStateMachine.cs` | FSM (Dead 상태에서 전환 차단) |
| `UnitData.cs` | ScriptableObject (HP, 속도, 에너지, 수확 쿨다운) |
| `WorkerUnit.cs` | 워커 행동 (수확→수거→납품 / 건설 / 경로탐색 / 막힘 감지) |
| `WorkerEnergy.cs` | 워커 에너지 (고갈 시 사망) |
| `WorkerAssigner.cs` | 자원 노드 클릭 → 최근접 유휴 워커 배정 |

### 경로탐색 (PathFinding/)
| 파일 | 역할 |
|------|------|
| `PathfindingGrid.cs` | 다층 타일맵 격자 (계단 연결 로직 포함) |
| `TilemapPathfinder.cs` | A* 구현 |

#### 타일맵 레이어 우선순위
```
walkableTilemaps[0] = Elevated_2 (최상층)
walkableTilemaps[1] = Elevated_1
walkableTilemaps[2] = FlatGround (최하층)
stairTilemaps      = 계단
blockedTilemaps    = 절벽 (통행 불가)
```

### 층/레이어 (Level/)
| 파일 | 역할 |
|------|------|
| `FloorObject.cs` | 정렬 레이어 + 물리 레이어 일괄 변경 |
| `FloorType.cs` | Floor1/2/3 열거형 + 레이어명 확장 |
| `FloorDetector.cs` | 타일맵으로 현재 층 감지 |

---

## 코딩 컨벤션

### 네이밍
```csharp
private int _privateField;       // 언더스코어 접두사
public int PublicProperty;       // PascalCase
private const int MAX_VALUE = 5; // 대문자 언더스코어
private void methodName() {}     // 훅 메서드는 camelCase
```

### 구조
- `[Header("섹션명")]` - SerializeField 그룹화
- `[DefaultExecutionOrder(-50)]` - 초기화 순서 제어
- `// ---` 구분선으로 코드 섹션 분리
- 보호 가상 훅 메서드: `OnBuilt()`, `OnDamaged()`, `OnDestroyed()` 등

### 의존성 해소 원칙
1. **ServiceLocator** 우선 사용
2. `FindFirstObjectByType` 는 ServiceLocator 사용 불가 시 폴백으로만
3. 직접 `GetComponent` 체인 지양

---

## 주요 이벤트 목록 (EventBus)

```csharp
OnGameStateChanged      // 게임 상태 변경
OnPhaseChanged          // Prepare↔Combat 전환
OnResourceChanged       // 자원량 변동
OnBuildingPlaced        // 건물 설치 완료
OnBuildingDestroyed     // 건물 파괴
OnUnitSpawned           // 유닛 생성
OnUnitDied              // 유닛 사망
OnWaveStarted           // 웨이브 시작
OnAllWavesCleared       // 전체 웨이브 클리어
OnHeadquartersDestroyed // Castle 파괴
OnResetRequested        // 전략 초기화 요청
OnWorkerEnergyDepleted  // 워커 에너지 소진
OnScorePenalty          // 점수 패널티
```

---

## 미구현 / TODO

- **적 AI**: 이벤트/인프라 존재하나 적 유닛 AI 미구현
- **전투 유닛**: ProductionBuilding 스폰은 있으나 전투 로직 없음
- **점수 UI**: OnScorePenalty 이벤트 발행되나 표시 UI 없음
- **방어 포인트 자동이동**: ProductionBuilding.cs 주석 참고 (todo 태그)

---

## 테스트 작성 시 참고

Unity Test Framework 사용 (EditMode / PlayMode).

**단위 테스트 가능 항목** (MonoBehaviour 의존 없음):
- `ResourceInventory` - 자원 증감, CanAfford, Snapshot
- `EventBus` - Subscribe/Publish/Unsubscribe
- `ServiceLocator` - Register/Get/중복 등록 경고
- `UnitStateMachine` - 상태 전환, Dead 락

**PlayMode 테스트 필요 항목**:
- `WorkerUnit` 경로탐색 및 상태 전환
- `BuildingPlacer` 배치 검증
- `PathfindingGrid` 다층 경로 계산
