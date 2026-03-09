using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시스템 간 의존성 없이 통신하는 이벤트 버스
///
/// 사용법:
///   발행: EventBus.Publish(new OnUnitDied { unit = this });
///   구독: EventBus.Subscribe<OnUnitDied>(OnUnitDiedHandler);
///   해제: EventBus.Unsubscribe<OnUnitDied>(OnUnitDiedHandler);
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers
        = new Dictionary<Type, List<Delegate>>();

    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();
        _handlers[type].Add(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type)) return;

        var snapshot = new List<Delegate>(_handlers[type]);
        foreach (var handler in snapshot)
        {
            try { (handler as Action<T>)?.Invoke(eventData); }
            catch (Exception e)
            { Debug.LogError($"[EventBus] {typeof(T).Name} 처리 중 에러: {e}"); }
        }
    }

    public static void Clear() => _handlers.Clear();
}

// ================================================================
// 이벤트 구조체 정의
// ================================================================

// --- 게임 상태 ---
public struct OnGameStateChanged        { public GameState state; }
public struct OnGameOver                { }
public struct OnStageClear              { }

// --- 페이즈 ---
public struct OnPhaseChanged            { public PhaseType phase; }
public struct OnWaveStartRequested      { }
public struct OnResetRequested          { }

// --- 자원 ---
public struct OnResourceChanged         { public ResourceType type; public int current; public int delta; }
public struct OnResourceNodeClicked     { public ResourceNode node; }

// --- 일꾼 ---
public struct OnWorkerAssigned          { public WorkerUnit worker; public ResourceNode node; }
public struct OnWorkerEnergyDepleted    { public WorkerUnit worker; }
public struct OnWorkerBecameIdle        { public WorkerUnit worker; }

// --- 건물 ---
public struct OnBuildingPlaced          { public BuildingBase building; }
public struct OnBuildingDestroyed       { public BuildingBase building; }
public struct OnMaintenanceToggled      { public BuildingBase building; public bool active; }
public struct OnHeadquartersDestroyed   { }

// --- 웨이브 ---
public struct OnWaveStarted             { public int waveIndex; }
public struct OnWaveCleared             { public int waveIndex; }
public struct OnAllWavesCleared         { }

// --- 유닛 ---
public struct OnUnitSpawned             { public UnitBase unit; }
public struct OnUnitDied                { public UnitBase unit; }

// --- 방어 ---
public struct OnDefensePointSet         { public Vector3 position; }

// --- 점수 ---
public struct OnScorePenalty            { public ScoreType scoreType; public int amount; }

// --- 층 ---
public struct OnFloorChanged            { public GameObject obj; public FloorType floor; }