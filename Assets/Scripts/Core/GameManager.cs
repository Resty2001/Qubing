using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private const int BoardSize = 7;
    private const int DiceFaceCount = 6;

    public static GameManager Instance;

    [Header("Game Settings")]
    public int maxTurnGauge = 6;
    private int currentTurnGauge = 0;
    
    [Header("Balance Settings")]
    public float difficultyMultiplier = 0.7f; 
    public int cumulativeEnemyHP = 0; 
    
    [Header("Progression Info")]
    public int totalTurns = 0;      
    public int currentScore = 0;    
    public int currentCombo = 1;    
    public bool isGameOver = false;

    private bool killHappenedThisTurn = false; 

    [Header("UI References")]
    public Image[] gaugeSlots;          
    public TextMeshProUGUI txtScore;    
    public GameObject gameOverPanel;    
    public TextMeshProUGUI txtFinalScore; 
    public GameObject settingsPanel;    

    [Header("Game Objects")]
    public GameObject enemyPrefab;
    public GameObject explosionPrefab;
    public DiceController playerController;
    [SerializeField] private DiceLogic diceLogic;

    [Header("Soft Lock")]
    [SerializeField] private int softLockCheckInterval = 4;
    [SerializeField] private int softLockWarningMaxActions = 3;
    [SerializeField] private GameObject softLockWarningPanel;
    [SerializeField] private TextMeshProUGUI txtSoftLockWarning;

    private SoftLockDetector _softLockDetector;
    private int[] _softLockChargeBuffer;
    private List<SoftLockEnemySnapshot> _softLockEnemyBuffer;
    private SoftLockWarningState _softLockWarningState;
    private System.Func<bool> _evaluateSoftLockCallback;

    [Header("Board Settings")]
    public GameObject tilePrefab; 
    public Transform tileParent;  
    private readonly GameObject[,] boardTiles = new GameObject[BoardSize, BoardSize];
    
    public List<Enemy> activeEnemies = new List<Enemy>();

    public bool IsSoftLockWarningActive =>
        _softLockWarningState != null && _softLockWarningState.IsWarningActive;

    public int SoftLockWarningActionsRemaining =>
        _softLockWarningState != null
            ? _softLockWarningState.WarningActionsRemaining
            : 0;

    public int SoftLockCheckActionCounter =>
        _softLockWarningState != null
            ? _softLockWarningState.CheckActionCounter
            : 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (diceLogic == null && playerController != null)
        {
            diceLogic = playerController.GetComponent<DiceLogic>();
        }

        _softLockDetector = new SoftLockDetector();
        _softLockChargeBuffer = new int[DiceFaceCount];
        _softLockEnemyBuffer = new List<SoftLockEnemySnapshot>(48);
        _softLockWarningState = new SoftLockWarningState(
            Mathf.Max(1, softLockCheckInterval),
            Mathf.Max(1, softLockWarningMaxActions));
        _evaluateSoftLockCallback = EvaluateSoftLock;
    }

    void Start()
    {
        GenerateBoard();
        SpawnInitialEnemies(); // 초기 적 소환 (밸런스 카운트 제외됨)
        UpdateUI(); 

        if(gameOverPanel != null) gameOverPanel.SetActive(false);
        if(settingsPanel != null) settingsPanel.SetActive(false);
        UpdateSoftLockWarningUI();
    }

    public void OnPlayerMove(Vector2Int futurePos, bool isCombat)
    {
        if (isGameOver) return;

        bool successfulKill = killHappenedThisTurn;

        // 콤보 로직
        if (killHappenedThisTurn) killHappenedThisTurn = false; 
        else currentCombo = 1;

        // 전체 진행 시간 증가
        totalTurns++;

        // 비전투 이동일 때만 게이지 충전
        if (!isCombat)
        {
            currentTurnGauge++;
        }

        bool waveSpawnedThisAction = false;
        if (currentTurnGauge >= maxTurnGauge)
        {
            waveSpawnedThisAction = SpawnEnemyWave(futurePos);
            currentTurnGauge = 0;
        }
        
        UpdateUI();

        if (isGameOver)
        {
            return;
        }

        bool hardLocked =
            playerController != null && playerController.CheckIfTrapped();
        bool warningWasActive = IsSoftLockWarningActive;
        int previousWarningActions = SoftLockWarningActionsRemaining;

        SoftLockActionOutcome outcome = _softLockWarningState.ProcessAction(
            successfulAction: true,
            successfulKill: successfulKill,
            waveChangedBoard: waveSpawnedThisAction,
            hardLocked: hardLocked,
            gameOverAlready: isGameOver,
            evaluateSoftLock: _evaluateSoftLockCallback);

        LogSoftLockStateChange(warningWasActive, previousWarningActions);
        UpdateSoftLockWarningUI();

        if (outcome == SoftLockActionOutcome.TriggerTrappedGameOver)
        {
            TriggerGameOver("TRAPPED!");
            return;
        }

        if (outcome == SoftLockActionOutcome.TriggerSoftLockGameOver)
        {
            Debug.Log("[SoftLock] Final check failed - game over");
            TriggerGameOver("SOFT LOCK");
        }
    }

    public void RemoveEnemy(Enemy enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            int scoreGain = enemy.maxHP * 10 * currentCombo;
            currentScore += scoreGain;

            currentCombo++;
            killHappenedThisTurn = true;
            ClearSoftLockWarning();

            if (explosionPrefab != null) {
                GameObject vfx = Instantiate(explosionPrefab, enemy.transform.position, Quaternion.identity);
                Color targetColor = Color.white;
                switch (enemy.myColor) {
                    case DiceColor.Red: targetColor = Color.red; break;
                    case DiceColor.Green: targetColor = Color.green; break;
                    case DiceColor.Blue: targetColor = Color.blue; break;
                }
                var ps = vfx.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = targetColor; 
                }
                else
                {
                    Debug.LogWarning("[VFX] Explosion prefab is missing a ParticleSystem.");
                }
            }

            activeEnemies.Remove(enemy);
            Destroy(enemy.gameObject);

            UpdateUI();
        }
    }

    public void RewindGauge(int amount) {
        currentTurnGauge -= amount;
        if (currentTurnGauge < 0) currentTurnGauge = 0;
        UpdateUI();
    }

    void SpawnInitialEnemies() {
        // [수정됨] 초기 적 3마리는 밸런스 카운트(addToBalance)를 false로 설정하여 제외
        SpawnEnemyAt(GetRandomSpawnPos(new Vector2Int(3,3)), 2, DiceColor.Red, false);
        SpawnEnemyAt(GetRandomSpawnPos(new Vector2Int(3,3)), 2, DiceColor.Green, false);
        SpawnEnemyAt(GetRandomSpawnPos(new Vector2Int(3,3)), 2, DiceColor.Blue, false);
    }

    bool SpawnEnemyWave(Vector2Int avoidPos)
    {
        float progress = Mathf.Clamp01((float)totalTurns / 100f);
        difficultyMultiplier = Mathf.Lerp(0.7f, 1.3f, progress);

        int maxWaveCount = 2; 
        if (totalTurns >= 60) maxWaveCount = 4;
        else if (totalTurns >= 30) maxWaveCount = 3;

        float targetTotalHP = totalTurns * difficultyMultiplier;
        int spawnBudget = Mathf.RoundToInt(targetTotalHP - cumulativeEnemyHP);

        if (spawnBudget < 1) spawnBudget = 0; 
        if (spawnBudget == 0 && activeEnemies.Count < 2) spawnBudget = 2;

        Debug.Log($"[Wave] Turn: {totalTurns}, Mul: {difficultyMultiplier:F2}, Budget: {spawnBudget}");

        int currentWaveSpawnCount = 0;

        while (spawnBudget > 0 && currentWaveSpawnCount < maxWaveCount)
        {
            Vector2Int pos = GetRandomSpawnPos(avoidPos);
            if (pos.x == -1) 
            {
                TriggerGameOver("MAP FULL!");
                return currentWaveSpawnCount > 0;
            }

            int maxPossibleHP = Mathf.Min(6, spawnBudget);
            int enemyHP = Random.Range(1, maxPossibleHP + 1);

            if (totalTurns > 50 && enemyHP < 2 && spawnBudget >= 2) enemyHP = 2;

            DiceColor randomColor = (DiceColor)Random.Range(0, 3);
            
            // 웨이브로 소환되는 적은 밸런스에 포함(true)
            SpawnEnemyAt(pos, enemyHP, randomColor, true);

            spawnBudget -= enemyHP;
            currentWaveSpawnCount++; 
        }

        return currentWaveSpawnCount > 0;
    }

    // [수정됨] addToBalance 파라미터 추가 (기본값 true)
    void SpawnEnemyAt(Vector2Int pos, int hp, DiceColor color, bool addToBalance = true) {
        Vector3 worldPos = new Vector3(pos.x, 0.02f, pos.y);
        GameObject newEnemyObj = Instantiate(enemyPrefab, worldPos, Quaternion.Euler(90, 0, 0));
        Enemy enemyScript = newEnemyObj.GetComponent<Enemy>();
        enemyScript.Initialize(hp, color, pos);
        
        activeEnemies.Add(enemyScript);

        // [수정됨] 플래그가 true일 때만 누적 HP에 합산
        if (addToBalance)
        {
            cumulativeEnemyHP += hp;
        }
    }

    void UpdateUI()
    {
        if (gaugeSlots != null)
        {
            for (int i = 0; i < gaugeSlots.Length; i++)
            {
                if (gaugeSlots[i] != null) gaugeSlots[i].enabled = (i < currentTurnGauge);
            }
        }

        if (txtScore != null) txtScore.text = $"SCORE <color=yellow>{currentScore:D5}</color>";
    }

    public void ToggleSettings()
    {
        if (isGameOver) return;
        if (settingsPanel == null) return;

        bool isActive = settingsPanel.activeSelf;
        settingsPanel.SetActive(!isActive); 
        Time.timeScale = !isActive ? 0 : 1; 
    }

    public void RestartGame()
    {
        Time.timeScale = 1; 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Application.Quit(); 
    }

    void TriggerGameOver(string reason)
    {
        isGameOver = true;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (txtFinalScore != null) txtFinalScore.text = $"SCORE\n<color=yellow>{currentScore:D5}</color>";
        }
    }

    private bool EvaluateSoftLock()
    {
        if (isGameOver)
        {
            return false;
        }

        if (playerController == null || diceLogic == null)
        {
            Debug.LogWarning(
                "[SoftLock] Check skipped because playerController or diceLogic is not assigned.");
            return false;
        }

        Debug.Log("[SoftLock] Check start");

        for (int face = 0; face < DiceFaceCount; face++)
        {
            _softLockChargeBuffer[face] = diceLogic.GetCharge((DiceFaceId)face);
        }

        _softLockEnemyBuffer.Clear();
        for (int index = 0; index < activeEnemies.Count; index++)
        {
            Enemy enemy = activeEnemies[index];
            if (enemy == null)
            {
                continue;
            }

            _softLockEnemyBuffer.Add(
                new SoftLockEnemySnapshot(
                    enemy.gridPos,
                    enemy.myColor,
                    enemy.currentHP));
        }

        bool softLocked = _softLockDetector.IsSoftLocked(
            playerController.GetCurrentPosition(),
            diceLogic.GetCurrentOrientationIndex(),
            _softLockChargeBuffer,
            _softLockEnemyBuffer);

        Debug.Log(softLocked
            ? "[SoftLock] Current static board is softlocked"
            : "[SoftLock] Escape route exists");
        return softLocked;
    }

    private void ClearSoftLockWarning()
    {
        if (_softLockWarningState == null)
        {
            return;
        }

        bool warningWasActive = _softLockWarningState.IsWarningActive;
        _softLockWarningState.ClearAfterKill();
        if (warningWasActive)
        {
            Debug.Log("[SoftLock] Warning cleared");
        }

        UpdateSoftLockWarningUI();
    }

    private void UpdateSoftLockWarningUI()
    {
        bool warningActive = IsSoftLockWarningActive;
        if (softLockWarningPanel != null)
        {
            softLockWarningPanel.SetActive(warningActive);
        }

        if (txtSoftLockWarning != null)
        {
            txtSoftLockWarning.text = warningActive
                ? $"DANGER\nDefeat an enemy within {SoftLockWarningActionsRemaining} actions"
                : string.Empty;
        }
    }

    private void LogSoftLockStateChange(
        bool warningWasActive,
        int previousWarningActions)
    {
        if (!warningWasActive && IsSoftLockWarningActive)
        {
            Debug.Log(
                $"[SoftLock] Warning started: {SoftLockWarningActionsRemaining} actions");
            return;
        }

        if (warningWasActive && !IsSoftLockWarningActive)
        {
            Debug.Log("[SoftLock] Warning cleared");
            return;
        }

        if (IsSoftLockWarningActive &&
            previousWarningActions != SoftLockWarningActionsRemaining)
        {
            Debug.Log(
                $"[SoftLock] Warning remaining: {SoftLockWarningActionsRemaining}");
        }
    }

    Vector2Int GetRandomSpawnPos(Vector2Int avoidPos) {
        Vector2Int playerPos = new Vector2Int(3, 3);
        if (playerController != null) playerPos = playerController.GetCurrentPosition();
        for (int i = 0; i < 100; i++) {
            int x = Random.Range(0, BoardSize);
            int y = Random.Range(0, BoardSize);
            Vector2Int candidate = new Vector2Int(x, y);
            if (IsLegalSpawnPos(candidate, avoidPos, playerPos)) return candidate;
        }

        for (int x = 0; x < BoardSize; x++) {
            for (int y = 0; y < BoardSize; y++) {
                Vector2Int candidate = new Vector2Int(x, y);
                if (IsLegalSpawnPos(candidate, avoidPos, playerPos)) return candidate;
            }
        }

        return new Vector2Int(-1, -1);
    }

    bool IsLegalSpawnPos(Vector2Int candidate, Vector2Int avoidPos, Vector2Int playerPos) {
        if (!IsInsideBoard(candidate)) return false;
        if (candidate == avoidPos || candidate == playerPos) return false;
        if (GetEnemyAt(candidate) != null) return false;
        return true;
    }

    public bool IsInsideBoard(Vector2Int pos) {
        return pos.x >= 0 && pos.x < BoardSize && pos.y >= 0 && pos.y < BoardSize;
    }

    public GameObject GetBoardTile(Vector2Int pos) {
        if (!IsInsideBoard(pos)) return null;
        return boardTiles[pos.x, pos.y];
    }
    
    public Enemy GetEnemyAt(Vector2Int pos) {
        foreach (Enemy enemy in activeEnemies) if (enemy.gridPos == pos) return enemy;
        return null;
    }

    void GenerateBoard()
    {
        for (int x = 0; x < BoardSize; x++)
        {
            for (int y = 0; y < BoardSize; y++)
            {
                Vector3 pos = new Vector3(x, -0.005f, y); 
                GameObject tile = Instantiate(tilePrefab, pos, Quaternion.Euler(90, 0, 0));
                if(tileParent != null) tile.transform.parent = tileParent;
                boardTiles[x, y] = tile;
            }
        }
    }
}
