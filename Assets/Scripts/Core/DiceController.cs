using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // New Input System

public class DiceController : MonoBehaviour
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    [Header("Settings")]
    public float rollSpeed = 300f;
    
    // 스와이프 감지용 변수
    public float minSwipeDistance = 50f; 
    private Vector2 touchStartPos;
    private bool isSwiping = false; 

    private bool isRolling = false;
    private Vector2Int currentGridPos = new Vector2Int(3, 3);
    
    private DiceLogic diceLogic;

    void Start()
    {
        diceLogic = GetComponent<DiceLogic>();
    }

    void Update()
    {
        if (isRolling || GameManager.Instance.isGameOver) return;

        // 1. 키보드 입력
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame) TryRoll(Vector3.forward, Vector2Int.up);
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame) TryRoll(Vector3.back, Vector2Int.down);
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame) TryRoll(Vector3.left, Vector2Int.left);
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) TryRoll(Vector3.right, Vector2Int.right);
        }

        // 2. 터치 입력
        HandleTouchInput();
    }

    void HandleTouchInput()
    {
        if (Touchscreen.current == null) return;

        var touch = Touchscreen.current.primaryTouch;

        if (touch.press.isPressed)
        {
            if (touch.press.wasPressedThisFrame)
            {
                touchStartPos = touch.position.ReadValue();
                isSwiping = true;
            }
        }
        else if (touch.press.wasReleasedThisFrame && isSwiping)
        {
            isSwiping = false;
            Vector2 touchEndPos = touch.position.ReadValue();
            Vector2 swipeDelta = touchEndPos - touchStartPos;

            if (swipeDelta.magnitude > minSwipeDistance)
            {
                if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
                {
                    if (swipeDelta.x > 0) TryRoll(Vector3.right, Vector2Int.right);
                    else TryRoll(Vector3.left, Vector2Int.left);
                }
                else
                {
                    if (swipeDelta.y > 0) TryRoll(Vector3.forward, Vector2Int.up);
                    else TryRoll(Vector3.back, Vector2Int.down);
                }
            }
        }
    }
    
    void TryRoll(Vector3 direction, Vector2Int gridChange)
    {
        Vector2Int targetPos = currentGridPos + gridChange;
        if (!GameManager.Instance.IsInsideBoard(targetPos)) return;

        Enemy targetEnemy = GameManager.Instance.GetEnemyAt(targetPos);

        if (targetEnemy != null)
        {
            HandleCombat(targetEnemy, direction, targetPos);
        }
        else
        {
            diceLogic.UpdateFaces(gridChange, false); 
            currentGridPos = targetPos;
            GameManager.Instance.OnPlayerMove(targetPos, false); 
            StartCoroutine(Roll(direction));
        }
    }

    void HandleCombat(Enemy enemy, Vector3 direction, Vector2Int targetPos)
    {
        DiceFace futureBottomFace = diceLogic.GetFutureBottomFace(new Vector2Int((int)direction.x, (int)direction.z));
        bool isColorMatch = (futureBottomFace.color == enemy.myColor);
        bool isPowerEnough = (futureBottomFace.charge >= enemy.currentHP);

        if (isColorMatch && isPowerEnough)
        {
            // 🔥 [복구됨] 오버킬 계산 및 게이지 회복 로직 🔥
            int overkill = futureBottomFace.charge - enemy.currentHP;
            
            // 1. 차지 소모 (적 체력만큼)
            diceLogic.ConsumeCharge(new Vector2Int((int)direction.x, (int)direction.z), enemy.currentHP);
            
            // 2. 오버킬이 있다면 게이지 회복 (Rewind)
            if (overkill > 0)
            {
                GameManager.Instance.RewindGauge(overkill);
                Debug.Log($"Overkill Bonus! Rewind: {overkill}");
            }

            // 3. 적 제거
            GameManager.Instance.RemoveEnemy(enemy);
            
            // 4. 주사위 업데이트 및 이동
            diceLogic.UpdateFaces(new Vector2Int((int)direction.x, (int)direction.z), true);
            currentGridPos = targetPos;
            GameManager.Instance.OnPlayerMove(targetPos, true);
            StartCoroutine(Roll(direction));
        }
        else
        {
            if (!isColorMatch) Debug.Log("색상 불일치");
            else if (!isPowerEnough) Debug.Log("차지 부족");
        }
    }

    IEnumerator Roll(Vector3 direction)
    {
        isRolling = true;
        float remainingAngle = 90f;
        Vector3 rotationCenter = transform.position + (Vector3.down * 0.5f) + (direction * 0.5f);
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, direction);

        while (remainingAngle > 0)
        {
            float rotationAngle = Mathf.Min(Time.deltaTime * rollSpeed, remainingAngle);
            transform.RotateAround(rotationCenter, rotationAxis, rotationAngle);
            remainingAngle -= rotationAngle;
            yield return null;
        }
        SnapToGrid();
        isRolling = false;
    }

    void SnapToGrid()
    {
        Vector3 vec = transform.position;
        vec.x = Mathf.Round(vec.x);
        vec.z = Mathf.Round(vec.z);
        vec.y = 0.5f;
        transform.position = vec;
        transform.eulerAngles = new Vector3(Mathf.Round(transform.eulerAngles.x / 90) * 90, 
                                            Mathf.Round(transform.eulerAngles.y / 90) * 90, 
                                            Mathf.Round(transform.eulerAngles.z / 90) * 90);
    }
    
    public Vector2Int GetCurrentPosition() => currentGridPos;

    public bool CheckIfTrapped()
    {
        foreach (var dir in Directions)
        {
            Vector2Int checkPos = currentGridPos + dir;
            if (!GameManager.Instance.IsInsideBoard(checkPos)) continue;
            Enemy enemy = GameManager.Instance.GetEnemyAt(checkPos);
            if (enemy == null) return false; 
            else
            {
                DiceFace futureFace = diceLogic.GetFutureBottomFace(dir);
                bool isMatch = (futureFace.color == enemy.myColor);
                bool isEnough = (futureFace.charge >= enemy.currentHP);
                if (isMatch && isEnough) return false;
            }
        }
        return true; 
    }
}
