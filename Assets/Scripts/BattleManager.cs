using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class BattleManager : MonoBehaviour
{
    public enum GameState { Title, Battle, Result }
    public enum Difficulty { Normal, Hard }

    public enum UnitType
    {
        Soldier,
        Archer,
        Tank,
        Wyvern,   // 追加：飛行兵
        Priest,   // 追加：回復支援兵
        Boss
    }

    public enum WaveState { Wave1, Wave2, FinalWave }

    public static GameState CurrentGameState { get; private set; } = GameState.Title;
    public static Difficulty SelectedDifficulty { get; private set; } = Difficulty.Normal;
    private static int enemyKillCount = 0;
    private static BattleManager instance;

    [Header("Unit Prefabs (Optional)")]
    [SerializeField] private GameObject playerUnitPrefab;
    [SerializeField] private GameObject enemyUnitPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform enemySpawnPoint;

    [Header("Towers")]
    [SerializeField] private GameObject playerTower;
    [SerializeField] private GameObject enemyTower;
    [Header("Title Screen Visuals")]
    [SerializeField] private Texture2D titleBgImage; // インスペクターから画像を割り当て

    [Header("Mana Settings")]
    [SerializeField] private float maxMana = 10f;
    [SerializeField] private float baseManaRecoveryRate = 1.3f;

    // コスト定義
    public const float CostSoldier = 2f;
    public const float CostArcher = 3f;
    public const float CostTank = 4f;
    public const float CostWyvern = 5f;
    public const float CostPriest = 3f;
    public const float CostMeteor = 6f;

    private float currentMana = 3f;
    private float currentManaRecoveryRate = 1.3f;
    private float enemySpawnTimer = 0f;
    private float gameTime = 0f;
    private string gameResultText = "";
    private int finalScore = 0;

    private int currentGold = 45;
    private int manaUpgradeLevel = 1;
    private int towerUpgradeLevel = 1;

    private int ManaUpgradeCost => 40 + (manaUpgradeLevel - 1) * 35;
    private int TowerUpgradeCost => 50 + (towerUpgradeLevel - 1) * 40;

    private WaveState currentWave = WaveState.Wave1;
    private float currentSpawnInterval = 3.8f;
    private string waveBannerText = "WAVE 1 START!";
    private float waveBannerTimer = 3.0f;
    private bool bossSpawned = false;

    private bool isAimingMeteor = false;
    private float lastPlayerSpawnTime = -1f;
    private const float SpawnCooldown = 0.22f;
    // フェーズ1：快適性＆テンポ制御
    public static bool IsGamePaused { get; private set; } = false;
    public static float GameSpeedMultiplier { get; private set; } = 1.0f; // 1.0f または 2.0f
    public static void RegisterEnemyKill(bool isBoss = false)
    {
        enemyKillCount++;
        if (instance != null)
        {
            int reward = isBoss ? 150 : 20;
            instance.currentGold += reward;
        }
    }

    private void Awake()
    {
        instance = this;

        // タワーとスポーン位置を最優先で新フィールド(Z = ±20)へ整列
        SetupSpawnPoints();

        if (FindFirstObjectByType<StageBuilder>() == null)
        {
            GameObject stageObj = new GameObject("StageBuilder");
            stageObj.AddComponent<StageBuilder>();
        }

        if (FindFirstObjectByType<SoundManager>() == null)
        {
            GameObject soundObj = new GameObject("SoundManager");
            soundObj.AddComponent<SoundManager>();
        }

        if (FindFirstObjectByType<VisualEffectManager>() == null)
        {
            GameObject vfxObj = new GameObject("VisualEffectManager");
            vfxObj.AddComponent<VisualEffectManager>();
        }
    }

    private void Start()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (cam.GetComponent<CameraController>() == null)
            {
                cam.gameObject.AddComponent<CameraController>();
            }
            cam.transform.position = new Vector3(0f, 26f, -14f);
            cam.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            cam.fieldOfView = 58f;
        }

        CurrentGameState = GameState.Title;
        enemyKillCount = 0;
    }

    private void Update()
    {
        switch (CurrentGameState)
        {
            case GameState.Title:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    StartBattle();
                if (Input.GetKeyDown(KeyCode.D))
                    ToggleDifficulty();
                break;

            case GameState.Battle:
                // ポーズ切り替え（Pキー）
                if (Input.GetKeyDown(KeyCode.P))
                {
                    TogglePause();
                }

                // 倍速切り替え（Tabキー）
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    ToggleSpeed();
                }

                if (!IsGamePaused)
                {
                    UpdateBattleLoop();
                }
                break;

            case GameState.Result:
                if (IsRetryKeyPressed())
                    RestartGame();
                break;
        }
    }

    public void ToggleDifficulty()
    {
        SelectedDifficulty = (SelectedDifficulty == Difficulty.Normal) ? Difficulty.Hard : Difficulty.Normal;
        if (SoundManager.Instance != null) SoundManager.Instance.Play(SoundManager.SoundType.Spawn, 0.6f);
    }

    public void StartBattle()
    {
        CurrentGameState = GameState.Battle;
        currentMana = CostSoldier;
        currentManaRecoveryRate = baseManaRecoveryRate;
        currentGold = 45;
        manaUpgradeLevel = 1;
        towerUpgradeLevel = 1;
        enemySpawnTimer = 0f;
        gameTime = 0f;
        enemyKillCount = 0;
        currentWave = WaveState.Wave1;

        currentSpawnInterval = (SelectedDifficulty == Difficulty.Hard) ? 3.0f : 3.8f;
        waveBannerText = (SelectedDifficulty == Difficulty.Hard) ? "HARD MODE: WAVE 1 START!" : "WAVE 1 START!";
        waveBannerTimer = 3.0f;
        bossSpawned = false;
        IsGamePaused = false;
        GameSpeedMultiplier = 1.0f;
        Time.timeScale = 1.0f;
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.Spawn, 0.7f);
            SoundManager.Instance.PlayBattleBGM();
        }
    }

    private void UpdateBattleLoop()
    {
        gameTime += Time.deltaTime;
        currentGold += Mathf.RoundToInt(2f * Time.deltaTime);

        if (waveBannerTimer > 0f) waveBannerTimer -= Time.deltaTime;

        UpdateWaveProgress();

        if (currentMana < maxMana)
        {
            currentMana = Mathf.Min(maxMana, currentMana + currentManaRecoveryRate * Time.deltaTime);
        }

        if (isAimingMeteor)
        {
            HandleMeteorAiming();
        }
        else
        {
            HandleKeyboardInput();
        }

        if (Input.GetKeyDown(KeyCode.U)) TryUpgradeMana();
        if (Input.GetKeyDown(KeyCode.I)) TryUpgradeTower();

        enemySpawnTimer += Time.deltaTime;
        if (enemySpawnTimer >= currentSpawnInterval)
        {
            enemySpawnTimer = 0f;
            SpawnWaveEnemy();
        }

        if (gameTime > 2.0f)
        {
            CheckGameOutcome();
        }
    }

    private void UpdateWaveProgress()
    {
        if (currentWave == WaveState.Wave1 && gameTime >= 25f)
        {
            currentWave = WaveState.Wave2;
            currentSpawnInterval = (SelectedDifficulty == Difficulty.Hard) ? 2.1f : 2.7f;
            waveBannerText = "WAVE 2 START!";
            waveBannerTimer = 3.0f;
        }
        else if (currentWave == WaveState.Wave2 && gameTime >= 55f)
        {
            currentWave = WaveState.FinalWave;
            currentSpawnInterval = (SelectedDifficulty == Difficulty.Hard) ? 1.5f : 1.9f;
            waveBannerText = "WARNING!! FINAL WAVE!!";
            waveBannerTimer = 4.0f;

            if (!bossSpawned)
            {
                bossSpawned = true;
                SpawnUnit(Unit.Team.Enemy, UnitType.Boss, enemySpawnPoint.position);
            }
        }
    }

    private void SpawnWaveEnemy()
    {
        float roll = Random.value;
        UnitType type = UnitType.Soldier;

        switch (currentWave)
        {
            case WaveState.Wave1:
                if (roll > 0.7f) type = UnitType.Archer;
                break;

            case WaveState.Wave2:
                if (roll > 0.8f) type = UnitType.Wyvern; // 敵ワイバーン出現
                else if (roll > 0.6f) type = UnitType.Tank;
                else if (roll > 0.3f) type = UnitType.Archer;
                break;

            case WaveState.FinalWave:
                if (roll > 0.75f) type = UnitType.Wyvern;
                else if (roll > 0.5f) type = UnitType.Tank;
                else if (roll > 0.25f) type = UnitType.Archer;
                break;
        }

        SpawnUnit(Unit.Team.Enemy, type, enemySpawnPoint.position);
    }

    private void HandleKeyboardInput()
    {
        if (Time.time - lastPlayerSpawnTime < SpawnCooldown) return;

        if (IsKeyDown(KeyCode.Alpha1, KeyCode.Keypad1)) TrySpawnUnit(UnitType.Soldier, CostSoldier);
        else if (IsKeyDown(KeyCode.Alpha2, KeyCode.Keypad2)) TrySpawnUnit(UnitType.Archer, CostArcher);
        else if (IsKeyDown(KeyCode.Alpha3, KeyCode.Keypad3)) TrySpawnUnit(UnitType.Tank, CostTank);
        else if (IsKeyDown(KeyCode.Alpha4, KeyCode.Keypad4)) TrySpawnUnit(UnitType.Wyvern, CostWyvern);
        else if (IsKeyDown(KeyCode.Alpha5, KeyCode.Keypad5)) TrySpawnUnit(UnitType.Priest, CostPriest);
        else if (IsKeyDown(KeyCode.Alpha6, KeyCode.Keypad6)) ToggleMeteorMode();
    }

    private bool IsKeyDown(KeyCode k1, KeyCode k2) => Input.GetKeyDown(k1) || Input.GetKeyDown(k2);

    public void ToggleMeteorMode()
    {
        if (CurrentGameState != GameState.Battle) return;
        if (currentMana >= CostMeteor)
        {
            isAimingMeteor = !isAimingMeteor;
        }
    }

    private void HandleMeteorAiming()
    {
        if (Input.GetMouseButtonDown(1))
        {
            isAimingMeteor = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (Input.mousePosition.y < 120f) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                if (currentMana >= CostMeteor)
                {
                    currentMana -= CostMeteor;
                    isAimingMeteor = false;
                    StartCoroutine(CastMeteorRoutine(hit.point));
                }
            }
        }
    }

    private IEnumerator CastMeteorRoutine(Vector3 impactPoint)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundManager.SoundType.MeteorCast, 0.7f);

        Vector3 spawnPos = impactPoint + new Vector3(0f, 25f, -6f);

        GameObject meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        meteor.name = "Meteor_Projectile";
        meteor.transform.position = spawnPos;
        meteor.transform.localScale = Vector3.one * 2.3f;

        Collider col = meteor.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = meteor.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(1f, 0.35f, 0f);
            rend.material = m;
        }

        float duration = 0.52f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (meteor != null)
            {
                meteor.transform.position = Vector3.Lerp(spawnPos, impactPoint, t * t);
            }
            yield return null;
        }

        if (meteor != null) Destroy(meteor);

        if (VisualEffectManager.Instance != null)
        {
            VisualEffectManager.Instance.PlayMeteorExplosion(impactPoint);
        }
        // メテオ着弾の強烈な画面振動とヒットストップ
        if (CameraController.Instance != null)
        {
            CameraController.Instance.Shake(0.4f, 0.65f); // 大きく揺らす
            CameraController.Instance.TriggerHitStop(0.08f); // 0.08秒の重厚な打撃硬直
        }

        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundManager.SoundType.MeteorExplosion, 0.9f);

        SpawnImpactShockwave(impactPoint);

        float blastRadius = 4.8f;
        float blastDamage = 140f;

        for (int i = 0; i < Unit.AllUnits.Count; i++)
        {
            Unit u = Unit.AllUnits[i];
            if (u == null || u.team != Unit.Team.Enemy || u.IsDead) continue;

            float dist = Vector3.Distance(new Vector3(impactPoint.x, 0f, impactPoint.z), new Vector3(u.transform.position.x, 0f, u.transform.position.z));
            if (dist <= blastRadius)
            {
                u.TakeDamage(blastDamage);
            }
        }
    }

    private void SpawnImpactShockwave(Vector3 center)
    {
        GameObject wave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wave.name = "Meteor_Shockwave";
        wave.transform.position = new Vector3(center.x, 0.05f, center.z);
        wave.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);

        Collider col = wave.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = wave.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            rend.material = m;
        }

        StartCoroutine(ShockwaveExpandRoutine(wave));
    }

    private IEnumerator ShockwaveExpandRoutine(GameObject wave)
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(1f, 0.02f, 1f);
        Vector3 maxScale = new Vector3(9.6f, 0.02f, 9.6f);

        while (elapsed < duration)
        {
            if (wave == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            wave.transform.localScale = Vector3.Lerp(startScale, maxScale, t);
            yield return null;
        }

        if (wave != null) Destroy(wave);
    }

    public void TrySpawnUnit(UnitType type, float cost)
    {
        if (CurrentGameState != GameState.Battle) return;
        if (Time.time - lastPlayerSpawnTime < SpawnCooldown) return;

        if (currentMana >= cost)
        {
            currentMana -= cost;
            lastPlayerSpawnTime = Time.time;

            if (SoundManager.Instance != null)
                SoundManager.Instance.Play(SoundManager.SoundType.Spawn, 0.6f);

            SpawnUnit(Unit.Team.Player, type, playerSpawnPoint.position);
        }
    }

    private void SpawnUnit(Unit.Team team, UnitType type, Vector3 basePos)
    {
        Vector3 spawnPos = basePos + new Vector3(Random.Range(-0.8f, 0.8f), 0f, 0f);

        GameObject unitObj = null;
        GameObject prefab = (team == Unit.Team.Player) ? playerUnitPrefab : enemyUnitPrefab;

        if (prefab != null)
        {
            unitObj = Instantiate(prefab, spawnPos, (team == Unit.Team.Player) ? Quaternion.identity : Quaternion.Euler(0, 180f, 0));
        }
        else
        {
            unitObj = CreateFallbackCapsule(team, spawnPos);
        }

        UnitModelBuilder.BuildUnitModel(unitObj, type, team);

        Unit unit = unitObj.GetComponent<Unit>();
        if (unit != null)
        {
            unit.SetTeam(team);

            float hpMul = (team == Unit.Team.Enemy && SelectedDifficulty == Difficulty.Hard) ? 1.35f : 1.0f;
            float atkMul = (team == Unit.Team.Enemy && SelectedDifficulty == Difficulty.Hard) ? 1.30f : 1.0f;

            switch (type)
            {
                case UnitType.Soldier:
                    unitObj.name = $"{team}_Soldier";
                    unitObj.transform.localScale = Vector3.one;
                    unit.InitStats(hp: 110f * hpMul, damage: 16f * atkMul, range: 2.0f, speed: 3.5f, interval: 1.15f);
                    break;

                case UnitType.Archer:
                    unitObj.name = $"{team}_Archer";
                    unitObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                    unit.InitStats(hp: 50f * hpMul, damage: 24f * atkMul, range: 7.0f, speed: 3.1f, interval: 1.4f);
                    break;

                case UnitType.Tank:
                    unitObj.name = $"{team}_Tank";
                    unitObj.transform.localScale = new Vector3(1.35f, 1.35f, 1.35f);
                    unit.InitStats(hp: 420f * hpMul, damage: 22f * atkMul, range: 2.2f, speed: 1.8f, interval: 1.7f);
                    break;

                case UnitType.Wyvern:
                    unitObj.name = $"{team}_Wyvern";
                    unitObj.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                    unit.InitStats(hp: 160f * hpMul, damage: 28f * atkMul, range: 5.0f, speed: 4.2f, interval: 1.6f, flying: true);
                    break;

                case UnitType.Priest:
                    unitObj.name = $"{team}_Priest";
                    unitObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                    unit.InitStats(hp: 70f * hpMul, damage: 0f, range: 4.5f, speed: 2.8f, interval: 2.0f, healer: true);
                    break;

                case UnitType.Boss:
                    unitObj.name = "Enemy_BOSS_TANK";
                    unitObj.transform.localScale = new Vector3(2.3f, 2.3f, 2.3f);
                    unit.InitStats(hp: 850f * hpMul, damage: 45f * atkMul, range: 2.7f, speed: 1.6f, interval: 1.9f, boss: true);
                    break;
            }
        }

        if (type != UnitType.Wyvern)
        {
            ApplyNavMeshWarp(unitObj, spawnPos);
        }
    }

    private GameObject CreateFallbackCapsule(Unit.Team team, Vector3 position)
    {
        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.transform.position = position;

        NavMeshAgent agent = capsule.AddComponent<NavMeshAgent>();
        agent.speed = 3.5f;
        agent.stoppingDistance = 1.5f;

        capsule.AddComponent<Unit>();
        return capsule;
    }

    private void ApplyNavMeshWarp(GameObject unitObj, Vector3 targetPos)
    {
        if (unitObj == null) return;
        NavMeshAgent agent = unitObj.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                agent.Warp(targetPos);
        }
    }

    private void SetupSpawnPoints()
    {
        // 1. タワーオブジェクトの検索と新台座(Z = ±20)への強制配置
        if (playerTower == null) playerTower = GameObject.Find("Tower_Player");
        if (enemyTower == null) enemyTower = GameObject.Find("Tower_Enemy");

        if (playerTower != null)
        {
            // 味方タワー本体を新台座(Z = -20)の真上に確実に配置
            playerTower.transform.position = new Vector3(0f, 0.4f, -20f);
        }

        if (enemyTower != null)
        {
            // 敵タワー本体を新台座(Z = +20)の真上に確実に配置
            enemyTower.transform.position = new Vector3(0f, 0.4f, 20f);
        }

        // 2. 出撃スポーン地点をタワーの前方（Z = ±17.2f）に強制配置
        if (playerSpawnPoint == null)
        {
            GameObject pObj = GameObject.Find("PlayerSpawnPoint");
            if (pObj == null) pObj = new GameObject("PlayerSpawnPoint");
            playerSpawnPoint = pObj.transform;
        }
        playerSpawnPoint.position = new Vector3(0f, 0.1f, -17.2f);

        if (enemySpawnPoint == null)
        {
            GameObject eObj = GameObject.Find("EnemySpawnPoint");
            if (eObj == null) eObj = new GameObject("EnemySpawnPoint");
            enemySpawnPoint = eObj.transform;
        }
        enemySpawnPoint.position = new Vector3(0f, 0.1f, 17.2f);
    }

    private void CheckGameOutcome()
    {
        bool isEnemyTowerDestroyed = (enemyTower == null || !enemyTower.activeInHierarchy);
        bool isPlayerTowerDestroyed = (playerTower == null || !playerTower.activeInHierarchy);

        if (isEnemyTowerDestroyed || isPlayerTowerDestroyed)
        {
            CurrentGameState = GameState.Result;
            isAimingMeteor = false;

            if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

            float diffBonus = (SelectedDifficulty == Difficulty.Hard) ? 1.5f : 1.0f;

            if (isEnemyTowerDestroyed)
            {
                gameResultText = "VICTORY";
                if (SoundManager.Instance != null) SoundManager.Instance.Play(SoundManager.SoundType.Victory, 0.8f);

                float towerHp = (playerTower != null && playerTower.GetComponent<Unit>() != null) ? playerTower.GetComponent<Unit>().CurrentHp : 0;
                int baseScore = 5000 + (enemyKillCount * 120) + (currentGold * 10) + Mathf.RoundToInt(towerHp * 3) - Mathf.RoundToInt(gameTime * 10);
                finalScore = Mathf.Max(0, Mathf.RoundToInt(baseScore * diffBonus));
            }
            else
            {
                gameResultText = "DEFEAT";
                if (SoundManager.Instance != null) SoundManager.Instance.Play(SoundManager.SoundType.Defeat, 0.8f);
                int baseScore = enemyKillCount * 80 + (currentGold * 5) + Mathf.RoundToInt(gameTime * 15);
                finalScore = Mathf.Max(0, Mathf.RoundToInt(baseScore * diffBonus));
            }
        }
    }

    private void RestartGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    public void TryUpgradeMana()
    {
        if (CurrentGameState != GameState.Battle) return;
        if (currentGold >= ManaUpgradeCost)
        {
            currentGold -= ManaUpgradeCost;
            manaUpgradeLevel++;
            currentManaRecoveryRate = baseManaRecoveryRate + (manaUpgradeLevel - 1) * 0.35f;
            if (SoundManager.Instance != null) SoundManager.Instance.Play(SoundManager.SoundType.Victory, 0.4f);
        }
    }

    public void TryUpgradeTower()
    {
        if (CurrentGameState != GameState.Battle) return;
        if (currentGold >= TowerUpgradeCost)
        {
            currentGold -= TowerUpgradeCost;
            towerUpgradeLevel++;
            if (playerTower != null)
            {
                Unit towerUnit = playerTower.GetComponent<Unit>();
                if (towerUnit != null)
                {
                    float newDmg = 22f + (towerUpgradeLevel - 1) * 9f;
                    float newInterval = Mathf.Max(0.65f, 1.4f - (towerUpgradeLevel - 1) * 0.18f);
                    towerUnit.UpgradeTowerDefense(newDmg, newInterval);
                }
            }
            if (SoundManager.Instance != null) SoundManager.Instance.Play(SoundManager.SoundType.TowerShoot, 0.7f);
        }
    }

    private bool IsRetryKeyPressed() => Input.GetKeyDown(KeyCode.R);

    #region GUI Display

    private Texture2D cardTexAvailable;
    private Texture2D cardTexDisabled;
    private Texture2D cardTexMeteor;
    private Texture2D manaBarBgTex;
    private Texture2D manaBarFillTex;
    private Texture2D overlayDarkTex;
    private Texture2D btnTex;
    private Texture2D btnHardTex;
    private Texture2D upgradeBtnTex;
    private Texture2D miniMapBgTex;
    private Texture2D miniMapLaneTex;
    private Texture2D dotPlayerTex;
    private Texture2D dotEnemyTex;
    private Texture2D dotBossTex;
    private Texture2D dotFlyingTex;
    private Texture2D camFrustumTex;
    private void InitTextures()
    {
        if (cardTexAvailable == null)
        {
            cardTexAvailable = MakeTex(2, 2, new Color(0.12f, 0.28f, 0.45f, 0.9f));
            cardTexDisabled = MakeTex(2, 2, new Color(0.18f, 0.18f, 0.22f, 0.7f));
            cardTexMeteor = MakeTex(2, 2, new Color(0.55f, 0.20f, 0.10f, 0.95f));
            manaBarBgTex = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            manaBarFillTex = MakeTex(2, 2, new Color(0.1f, 0.65f, 1.0f, 0.95f));
            overlayDarkTex = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.08f, 0.85f));
            btnTex = MakeTex(2, 2, new Color(0.15f, 0.45f, 0.85f, 0.95f));
            btnHardTex = MakeTex(2, 2, new Color(0.75f, 0.25f, 0.15f, 0.95f));
            upgradeBtnTex = MakeTex(2, 2, new Color(0.6f, 0.45f, 0.15f, 0.9f));

        // ミニマップ用テクスチャ
            miniMapBgTex = MakeTex(2, 2, new Color(0.12f, 0.15f, 0.12f, 0.85f));
            miniMapLaneTex = MakeTex(2, 2, new Color(0.35f, 0.32f, 0.28f, 0.9f));
            dotPlayerTex = MakeTex(2, 2, new Color(0.2f, 0.8f, 1.0f, 1.0f));
            dotEnemyTex = MakeTex(2, 2, new Color(1.0f, 0.25f, 0.25f, 1.0f));
            dotBossTex = MakeTex(2, 2, new Color(1.0f, 0.1f, 0.8f, 1.0f));
            dotFlyingTex = MakeTex(2, 2, new Color(1.0f, 0.9f, 0.2f, 1.0f));
            camFrustumTex = MakeTex(2, 2, new Color(1f, 1f, 1f, 0.2f));
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void OnGUI()
    {
        InitTextures();

        float sw = Screen.width;
        float sh = Screen.height;

        switch (CurrentGameState)
        {
            case GameState.Title:
                DrawTitleScreen(sw, sh);
                break;
            case GameState.Battle:
                DrawBattleScreen(sw, sh);
                break;
            case GameState.Result:
                DrawResultScreen(sw, sh);
                break;
        }
    }

    private void DrawTitleScreen(float sw, float sh)
    {
        // 1. タイトル背景画像の描画
        if (titleBgImage != null)
        {
            // 画面全体にアスペクト比を考慮してフィット（Fill）描画
            GUI.DrawTexture(new Rect(0, 0, sw, sh), titleBgImage, ScaleMode.ScaleAndCrop);

            // 文字がくっきり読めるように、上部とボタン周辺にダークオーバーレイを半透明で薄く重ねる
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), overlayDarkTex);
            GUI.color = Color.white;
        }
        else
        {
            // 画像がセットされていない場合のフォールバック背景
            GUI.DrawTexture(new Rect(0, 0, sw, sh), overlayDarkTex);
        }

        // 2. タイトルロゴ（背後にシャドウ効果を入れて視認性を向上）
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 52,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        // シャドウ
        titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(2, sh * 0.12f + 2, sw, 65f), "TOWER DEFENSE 3D", titleStyle);

        // メイン文字
        titleStyle.normal.textColor = new Color(0.35f, 0.85f, 1f);
        GUI.Label(new Rect(0, sh * 0.12f, sw, 65f), "TOWER DEFENSE 3D", titleStyle);

        // 3. 難易度選択ボタン
        float diffBtnW = 220f;
        float diffBtnH = 40f;
        Rect diffRect = new Rect((sw - diffBtnW) * 0.5f, sh * 0.23f, diffBtnW, diffBtnH);
        GUI.DrawTexture(diffRect, (SelectedDifficulty == Difficulty.Hard) ? btnHardTex : cardTexAvailable);

        GUIStyle emptyBtn = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        if (GUI.Button(diffRect, GUIContent.none, emptyBtn)) ToggleDifficulty();

        GUIStyle diffLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        diffLabel.normal.textColor = (SelectedDifficulty == Difficulty.Hard) ? Color.yellow : Color.white;
        GUI.Label(diffRect, (SelectedDifficulty == Difficulty.Hard) ? "DIFFICULTY : HARD [D]" : "DIFFICULTY : NORMAL [D]", diffLabel);

        // 4. 操作ガイド（黒帯プレートを敷いて読みやすく）
        float guideBoxW = 600f;
        float guideBoxH = 90f;
        Rect guideRect = new Rect((sw - guideBoxW) * 0.5f, sh * 0.32f, guideBoxW, guideBoxH);
        GUI.DrawTexture(guideRect, overlayDarkTex);

        GUIStyle guideStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
        guideStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        string guide = "【出撃】 [1] 兵士  [2] 弓兵  [3] 盾兵  [4] 飛竜  [5] 僧侶\n" +
                       "【必殺技】 [6] メテオ（画面クリック投下）\n" +
                       "【拠点強化】 [U] マナ回復UP  [I] タワー砲撃UP\n" +
                       "仲間たちを進軍させ、敵陣タワーを陥落させよ！";
        GUI.Label(guideRect, guide, guideStyle);

        // 5. BATTLE START ボタン
        float btnW = 280f;
        float btnH = 65f;
        Rect btnRect = new Rect((sw - btnW) * 0.5f, sh * 0.72f, btnW, btnH);

        GUI.DrawTexture(btnRect, btnTex);
        if (GUI.Button(btnRect, GUIContent.none, emptyBtn)) StartBattle();

        GUIStyle btnTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Label(btnRect, "BATTLE START", btnTextStyle);

        GUIStyle subTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        GUI.Label(new Rect(0, sh * 0.83f, sw, 30f), "Press [Enter] or [Space] to Start", subTextStyle);
    }

    private void DrawBattleScreen(float sw, float sh)
    {
        // 拠点HPバー（画面上部）
        float towerBarW = 200f;
        float towerBarH = 16f;

        Unit playerTowerUnit = (playerTower != null) ? playerTower.GetComponent<Unit>() : null;
        if (playerTowerUnit != null)
        {
            float pHp = playerTowerUnit.CurrentHp;
            float pMax = playerTowerUnit.MaxHp;
            float pRatio = Mathf.Clamp01(pHp / pMax);
            GUI.DrawTexture(new Rect(20f, 20f, towerBarW, towerBarH), manaBarBgTex);
            GUI.DrawTexture(new Rect(20f, 20f, towerBarW * pRatio, towerBarH), cardTexAvailable);

            GUIStyle pStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.cyan } };
            GUI.Label(new Rect(20f, 38f, towerBarW, 20f), $"味方拠点: {Mathf.CeilToInt(pHp)} / {pMax:F0}", pStyle);
        }

        Unit enemyTowerUnit = (enemyTower != null) ? enemyTower.GetComponent<Unit>() : null;
        if (enemyTowerUnit != null)
        {
            float eHp = enemyTowerUnit.CurrentHp;
            float eMax = enemyTowerUnit.MaxHp;
            float eRatio = Mathf.Clamp01(eHp / eMax);
            GUI.DrawTexture(new Rect(sw - towerBarW - 20f, 20f, towerBarW, towerBarH), manaBarBgTex);
            GUI.DrawTexture(new Rect(sw - towerBarW - 20f, 20f, towerBarW * eRatio, towerBarH), cardTexMeteor);

            GUIStyle eStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
            GUI.Label(new Rect(sw - towerBarW - 20f, 38f, towerBarW, 20f), $"敵拠点: {Mathf.CeilToInt(eHp)} / {eMax:F0}", eStyle);
        }

        // WAVE表示
        GUIStyle waveStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        waveStyle.normal.textColor = (SelectedDifficulty == Difficulty.Hard) ? new Color(1f, 0.5f, 0.3f) : Color.white;
        string modeTag = (SelectedDifficulty == Difficulty.Hard) ? "[HARD] " : "";
        string waveTitle = (currentWave == WaveState.Wave1) ? $"{modeTag}WAVE 1 / 3" :
                           (currentWave == WaveState.Wave2) ? $"{modeTag}WAVE 2 / 3" : $"{modeTag}FINAL WAVE (BOSS)";
        string nextWaveTimer = (currentWave == WaveState.Wave1) ? $"Next Wave: {Mathf.Max(0, 25f - gameTime):F0}s  |  Kills: {enemyKillCount}" :
                               (currentWave == WaveState.Wave2) ? $"Final Wave: {Mathf.Max(0, 55f - gameTime):F0}s  |  Kills: {enemyKillCount}" : $"SURVIVE & DEFEAT BOSS!  |  Kills: {enemyKillCount}";

        GUI.Label(new Rect(0, 15f, sw, 28f), waveTitle, waveStyle);
        GUIStyle subWaveStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperCenter, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
        GUI.Label(new Rect(0, 42f, sw, 20f), nextWaveTimer, subWaveStyle);
        // --- 倍速・ポーズボタン（画面右上） ---
        float speedBtnW = 60f;
        float speedBtnH = 26f;
        float topBtnY = 20f;
        Rect speedRect = new Rect(sw - 350f, topBtnY, speedBtnW, speedBtnH);
        GUI.DrawTexture(speedRect, (GameSpeedMultiplier == 2.0f) ? btnHardTex : cardTexAvailable);
        GUIStyle emptyBtn = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        if (GUI.Button(speedRect, GUIContent.none, emptyBtn)) ToggleSpeed();

        GUIStyle topBtnStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUI.Label(speedRect, (GameSpeedMultiplier == 2.0f) ? "2x >>" : "1x >", topBtnStyle);

        Rect pauseRect = new Rect(sw - 285f, topBtnY, speedBtnW, speedBtnH);
        GUI.DrawTexture(pauseRect, IsGamePaused ? btnHardTex : cardTexAvailable);
        if (GUI.Button(pauseRect, GUIContent.none, emptyBtn)) TogglePause();
        GUI.Label(pauseRect, IsGamePaused ? "PLAY" : "PAUSE", topBtnStyle);

        // --- ポーズ中のオーバーレイ表示 ---
        if (IsGamePaused)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), overlayDarkTex);
            GUI.color = Color.white;

            GUIStyle pauseTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(0, sh * 0.4f, sw, 50f), "PAUSED", pauseTextStyle);

            GUIStyle pauseSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, sh * 0.48f, sw, 30f), "Press [P] or Click PAUSE to Resume", pauseSubStyle);
        }
        DrawUpgradePanel(sw);

        if (waveBannerTimer > 0f)
        {
            GUIStyle bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = (currentWave == WaveState.FinalWave) ? new Color(1f, 0.2f, 0.2f) : new Color(1f, 0.9f, 0.3f);
            GUI.Label(new Rect(0, sh * 0.22f, sw, 60f), waveBannerText, bannerStyle);
        }

        // マナバー
        float barW = 540f;
        float barH = 14f;
        float barX = (sw - barW) * 0.5f;
        float barY = sh - 116f;

        GUI.DrawTexture(new Rect(barX, barY, barW, barH), manaBarBgTex);
        float manaRatio = Mathf.Clamp01(currentMana / maxMana);
        GUI.DrawTexture(new Rect(barX, barY, barW * manaRatio, barH), manaBarFillTex);

        GUIStyle manaStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUI.Label(new Rect(barX, barY - 20f, barW, 20f), $"MANA: {currentMana:F1} / {maxMana:F0} (+{currentManaRecoveryRate:F1}/s)", manaStyle);

        // 6枚の出撃カード（ソルジャー、アーチャー、タンク、ワイバーン、プリースト、メテオ）
        float cardW = 95f;
        float cardH = 78f;
        float spacing = 8f;
        float totalCardsW = (cardW * 6) + (spacing * 5);
        float startX = (sw - totalCardsW) * 0.5f;
        float cardY = sh - 94f;

        DrawUnitCard(new Rect(startX + (cardW + spacing) * 0, cardY, cardW, cardH), "[1] 兵士", CostSoldier, UnitType.Soldier);
        DrawUnitCard(new Rect(startX + (cardW + spacing) * 1, cardY, cardW, cardH), "[2] 弓兵", CostArcher, UnitType.Archer);
        DrawUnitCard(new Rect(startX + (cardW + spacing) * 2, cardY, cardW, cardH), "[3] 盾兵", CostTank, UnitType.Tank);
        DrawUnitCard(new Rect(startX + (cardW + spacing) * 3, cardY, cardW, cardH), "[4] 飛竜", CostWyvern, UnitType.Wyvern);
        DrawUnitCard(new Rect(startX + (cardW + spacing) * 4, cardY, cardW, cardH), "[5] 僧侶", CostPriest, UnitType.Priest);
        DrawMeteorCard(new Rect(startX + (cardW + spacing) * 5, cardY, cardW, cardH));
        // ミニマップ（レーダー）の描画
        DrawMiniMap(sw, sh);

        if (isAimingMeteor)
        {
            GUIStyle aimGuideStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.4f, 0.1f) } };
            GUI.Label(new Rect(0, sh * 0.35f, sw, 40f), "【メテオ照準中】 地面をクリックで投下 / 右クリックでキャンセル", aimGuideStyle);
        }
    }
    /// <summary>
    /// 画面左下にレーン全体のレーダー・ミニマップを描画
    /// </summary>
    private void DrawMiniMap(float sw, float sh)
    {
        float mapW = 90f;
        float mapH = 150f;
        float mapX = 18f;
        float mapY = sh - mapH - 18f;

        Rect mapRect = new Rect(mapX, mapY, mapW, mapH);
        GUI.DrawTexture(mapRect, miniMapBgTex);

        GUIStyle mapLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f) }
        };
        GUI.Label(new Rect(mapX, mapY - 14f, mapW, 16f), "RADAR", mapLabelStyle);

        float laneW = mapW * 0.32f;
        float laneX = mapX + (mapW - laneW) * 0.5f;
        GUI.DrawTexture(new Rect(laneX, mapY, laneW, mapH), miniMapLaneTex);

        // 拡張後のワールド座標（Z: -23f 〜 +23f, X: -10f 〜 +10f）
        float minZ = -23f;
        float maxZ = 23f;
        float minX = -10f;
        float maxX = 10f;

        // カメラ視界枠
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 cPos = cam.transform.position;
            float camNormX = Mathf.InverseLerp(minX, maxX, cPos.x);
            float camNormZ = Mathf.InverseLerp(minZ, maxZ, cPos.z + 8f);
            float camBoxW = mapW * 0.7f;
            float camBoxH = mapH * 0.28f;
            float camBoxX = mapX + camNormX * mapW - camBoxW * 0.5f;
            float camBoxY = mapY + (1f - camNormZ) * mapH - camBoxH * 0.5f;

            camBoxX = Mathf.Clamp(camBoxX, mapX, mapX + mapW - camBoxW);
            camBoxY = Mathf.Clamp(camBoxY, mapY, mapY + mapH - camBoxH);
            GUI.DrawTexture(new Rect(camBoxX, camBoxY, camBoxW, camBoxH), camFrustumTex);
        }

        // 両拠点タワー（Z = -20 と Z = +20）
        DrawMapIcon(mapX, mapY, mapW, mapH, minX, maxX, minZ, maxZ, 0f, -20f, 10f, dotPlayerTex);
        DrawMapIcon(mapX, mapY, mapW, mapH, minX, maxX, minZ, maxZ, 0f, 20f, 10f, dotEnemyTex);

        // 各ユニットのドット描画
        for (int i = 0; i < Unit.AllUnits.Count; i++)
        {
            Unit u = Unit.AllUnits[i];
            if (u == null || u.IsDead || u.isTower) continue;

            Vector3 uPos = u.transform.position;
            Texture2D dotTex = dotPlayerTex;
            float dotSize = 5f;

            if (u.team == Unit.Team.Enemy)
            {
                if (u.isBoss) { dotTex = dotBossTex; dotSize = 9f; }
                else if (u.isFlying) { dotTex = dotFlyingTex; dotSize = 6f; }
                else { dotTex = dotEnemyTex; }
            }
            else
            {
                if (u.isFlying) { dotTex = dotFlyingTex; dotSize = 6f; }
            }

            DrawMapIcon(mapX, mapY, mapW, mapH, minX, maxX, minZ, maxZ, uPos.x, uPos.z, dotSize, dotTex);
        }
    }

    private void DrawMapIcon(float mapX, float mapY, float mapW, float mapH, float minX, float maxX, float minZ, float maxZ, float worldX, float worldZ, float size, Texture2D tex)
    {
        float normX = Mathf.InverseLerp(minX, maxX, worldX);
        float normZ = Mathf.InverseLerp(minZ, maxZ, worldZ);

        // GUI座標系は上がY=0なので (1 - normZ)
        float px = mapX + normX * mapW - size * 0.5f;
        float py = mapY + (1f - normZ) * mapH - size * 0.5f;

        px = Mathf.Clamp(px, mapX, mapX + mapW - size);
        py = Mathf.Clamp(py, mapY, mapY + mapH - size);

        GUI.DrawTexture(new Rect(px, py, size, size), tex);
    }

    private void DrawUpgradePanel(float sw)
    {
        float panelW = 210f;
        float panelX = sw - panelW - 15f;
        float panelY = 65f;

        GUIStyle goldStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
        GUI.Label(new Rect(panelX, panelY, panelW, 24f), $"GOLD: {currentGold} G", goldStyle);

        float btnY1 = panelY + 30f;
        bool canAffordMana = currentGold >= ManaUpgradeCost;
        Rect manaBtnRect = new Rect(panelX, btnY1, panelW, 36f);

        GUI.DrawTexture(manaBtnRect, canAffordMana ? upgradeBtnTex : cardTexDisabled);

        GUIStyle emptyBtn = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        if (GUI.Button(manaBtnRect, GUIContent.none, emptyBtn)) TryUpgradeMana();

        GUIStyle upText = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        upText.normal.textColor = canAffordMana ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        GUI.Label(manaBtnRect, $"[U] マナ回復 Lv.{manaUpgradeLevel} ({ManaUpgradeCost}G)", upText);

        float btnY2 = btnY1 + 42f;
        bool canAffordTower = currentGold >= TowerUpgradeCost;
        Rect towerBtnRect = new Rect(panelX, btnY2, panelW, 36f);

        GUI.DrawTexture(towerBtnRect, canAffordTower ? upgradeBtnTex : cardTexDisabled);
        if (GUI.Button(towerBtnRect, GUIContent.none, emptyBtn)) TryUpgradeTower();
        upText.normal.textColor = canAffordTower ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        GUI.Label(towerBtnRect, $"[I] タワー砲撃 Lv.{towerUpgradeLevel} ({TowerUpgradeCost}G)", upText);
    }

    private void DrawResultScreen(float sw, float sh)
    {
        GUI.DrawTexture(new Rect(0, 0, sw, sh), overlayDarkTex);

        GUIStyle resultStyle = new GUIStyle(GUI.skin.label) { fontSize = 68, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        resultStyle.normal.textColor = (gameResultText == "VICTORY") ? Color.cyan : new Color(1f, 0.25f, 0.25f);
        GUI.Label(new Rect(0, sh * 0.15f, sw, 80f), gameResultText, resultStyle);

        GUIStyle statsStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        string modeLabel = (SelectedDifficulty == Difficulty.Hard) ? "[HARD x1.5]" : "[NORMAL]";
        string stats = $"SCORE : {finalScore:#,0} pt  {modeLabel}\n\n" +
                       $"撃破数 (Kills) : {enemyKillCount} 体  |  残存ゴールド : {currentGold} G\n" +
                       $"拠点強化 Lv : マナ Lv.{manaUpgradeLevel} / タワー Lv.{towerUpgradeLevel}\n" +
                       $"戦闘時間 : {gameTime:F1} 秒";
        GUI.Label(new Rect(0, sh * 0.35f, sw, 120f), stats, statsStyle);

        float btnW = 180f;
        float btnH = 50f;
        float btnY = sh * 0.68f;
        float gap = 30f;
        GUIStyle emptyBtn = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        GUIStyle btnLabel = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        Rect retryRect = new Rect(sw * 0.5f - btnW - gap * 0.5f, btnY, btnW, btnH);
        GUI.DrawTexture(retryRect, btnTex);
        if (GUI.Button(retryRect, GUIContent.none, emptyBtn)) RestartGame();
        GUI.Label(retryRect, "RETRY [R]", btnLabel);

        Rect titleRect = new Rect(sw * 0.5f + gap * 0.5f, btnY, btnW, btnH);
        GUI.DrawTexture(titleRect, cardTexAvailable);
        if (GUI.Button(titleRect, GUIContent.none, emptyBtn)) RestartGame();
        GUI.Label(titleRect, "TITLE", btnLabel);
    }

    private void DrawUnitCard(Rect rect, string nameLabel, float cost, UnitType type)
    {
        bool canAfford = currentMana >= cost;
        GUI.DrawTexture(rect, canAfford ? cardTexAvailable : cardTexDisabled);

        GUIStyle emptyBtnStyle = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        if (GUI.Button(rect, GUIContent.none, emptyBtnStyle))
        {
            if (canAfford)
            {
                isAimingMeteor = false;
                TrySpawnUnit(type, cost);
            }
        }

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        titleStyle.normal.textColor = canAfford ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 22f), nameLabel, titleStyle);

        GUIStyle costStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        costStyle.normal.textColor = canAfford ? new Color(1f, 0.85f, 0.2f) : new Color(0.5f, 0.5f, 0.5f);
        GUI.Label(new Rect(rect.x, rect.y + 34f, rect.width, 28f), $"Cost: {cost:F0}", costStyle);
    }

    private void DrawMeteorCard(Rect rect)
    {
        bool canAfford = currentMana >= CostMeteor;
        GUI.DrawTexture(rect, isAimingMeteor ? cardTexMeteor : (canAfford ? cardTexMeteor : cardTexDisabled));

        GUIStyle emptyBtnStyle = new GUIStyle(GUI.skin.button) { normal = { background = null }, hover = { background = null }, active = { background = null } };
        if (GUI.Button(rect, GUIContent.none, emptyBtnStyle)) ToggleMeteorMode();

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
        titleStyle.normal.textColor = (isAimingMeteor || canAfford) ? Color.yellow : new Color(0.6f, 0.6f, 0.6f);
        string labelText = isAimingMeteor ? "照準中..." : "[6] メテオ";
        GUI.Label(new Rect(rect.x, rect.y + 10f, rect.width, 22f), labelText, titleStyle);

        GUIStyle costStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        costStyle.normal.textColor = canAfford ? new Color(1f, 0.5f, 0.1f) : new Color(0.5f, 0.5f, 0.5f);
        GUI.Label(new Rect(rect.x, rect.y + 34f, rect.width, 28f), $"Cost: {CostMeteor:F0}", costStyle);
    }

    #endregion
    public void TogglePause()
    {
        IsGamePaused = !IsGamePaused;
        Time.timeScale = IsGamePaused ? 0f : GameSpeedMultiplier;
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundManager.SoundType.Spawn, 0.5f);
    }

    public void ToggleSpeed()
    {
        GameSpeedMultiplier = (GameSpeedMultiplier == 1.0f) ? 2.0f : 1.0f;
        if (!IsGamePaused)
        {
            Time.timeScale = GameSpeedMultiplier;
        }
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundManager.SoundType.Spawn, 0.5f);
    }
}