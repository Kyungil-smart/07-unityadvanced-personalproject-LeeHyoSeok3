using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Worker 방향키 디버그 이동 스크립트 (New Input System)
/// </summary>
public class DebugWorkerMover : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 3f;

    [Header("디버그 정보 표시")]
    public bool showFloorInfo = true;

    private Rigidbody2D _rb;
    private FloorObject _floorObject;
    private Vector2     _input;

    void Awake()
    {
        _rb          = GetComponent<Rigidbody2D>();
        _floorObject = GetComponent<FloorObject>();
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        _input = Vector2.zero;

        if (keyboard.upArrowKey.isPressed)    _input.y =  1f;
        if (keyboard.downArrowKey.isPressed)  _input.y = -1f;
        if (keyboard.leftArrowKey.isPressed)  _input.x = -1f;
        if (keyboard.rightArrowKey.isPressed) _input.x =  1f;

        _input = _input.normalized;
    }

    void FixedUpdate()
    {
        if (_rb != null)
            _rb.linearVelocity = _input * moveSpeed;
        else
            transform.Translate(_input * moveSpeed * Time.fixedDeltaTime);
    }

    void OnGUI()
    {
        if (!showFloorInfo) return;

        string floor = _floorObject != null
            ? _floorObject.CurrentFloor.ToString()
            : "FloorObject 없음";

        GUI.Label(new Rect(10, 10, 400, 25),
            $"Floor: {floor}  Pos: {transform.position}");
    }
}