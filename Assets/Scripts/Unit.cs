using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[RequireComponent(typeof(NavMeshAgent))]
public class Unit : MonoBehaviour
{
    public enum Team
    {
        Player,
        Enemy
    }

    [Header("陣営・ステータス")]
    [SerializeField] public Team team = Team.Player;
    [SerializeField] public bool isTower = false;
    [SerializeField] public bool isBoss = false;
    [SerializeField] public bool isFlying = false;
    [SerializeField] public bool isHealer = false;

    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float currentHp;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackRange = 2.0f;
    [SerializeField] private float attackInterval = 1.2f;

    [Header("タワー防衛射撃設定")]
    [SerializeField] private float towerAttackRange = 8.5f;
    [SerializeField] private float towerAttackDamage = 22f;
    [SerializeField] private float towerAttackInterval = 1.4f;

    private float bossSkillTimer = 0f;
    private const float BossSkillInterval = 6.0f;

    private NavMeshAgent agent;
    private float lastAttackTime;
    private Unit currentTarget;

    private Renderer[] allRenderers;
    private Color[] originalColors;
    private Coroutine flashCoroutine;

    private GameObject hpBarObj;
    private RectTransform hpBarTransform;
    private RectTransform hpFillRect;
    private float barHeight = 2.2f;

    private const float FlyingAltitude = 3.2f;

    public static List<Unit> AllUnits = new List<Unit>();

    public bool IsDead => currentHp <= 0f;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;

    public void SetTeam(Team newTeam)
    {
        team = newTeam;
    }

    public void InitStats(float hp, float damage, float range, float speed, float interval, bool boss = false, bool flying = false, bool healer = false)
    {
        maxHp = hp;
        currentHp = hp;
        attackDamage = damage;
        attackRange = range;
        attackInterval = interval;
        isBoss = boss;
        isFlying = flying;
        isHealer = healer;

        barHeight = isBoss ? 4.2f : (isTower ? 4.5f : (isFlying ? 1.4f : 2.3f));

        if (agent != null)
        {
            if (isFlying)
            {
                agent.enabled = false;
                transform.position = new Vector3(transform.position.x, FlyingAltitude, transform.position.z);
            }
            else
            {
                agent.speed = speed;
                agent.stoppingDistance = range * 0.8f;
            }
        }

        CacheRenderers();
        UpdateHealthBar();
    }

    public void UpgradeTowerDefense(float newDamage, float newInterval)
    {
        towerAttackDamage = newDamage;
        towerAttackInterval = newInterval;
    }

    private void Awake()
    {
        if (name.Contains("Enemy")) team = Team.Enemy;
        if (name.Contains("Player")) team = Team.Player;
        if (name.Contains("Tower")) isTower = true;
        if (name.Contains("BOSS")) isBoss = true;

        if (isTower)
        {
            maxHp = 600f;
            barHeight = 4.5f;
        }
        else if (isBoss)
        {
            maxHp = 850f;
            barHeight = 4.2f;
        }
        else
        {
            barHeight = 2.3f;
        }

        currentHp = maxHp;
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        if (!AllUnits.Contains(this))
        {
            AllUnits.Add(this);
        }
    }

    private void OnDisable()
    {
        AllUnits.Remove(this);
    }

    private void Start()
    {
        if (isTower)
        {
            if (transform.Find("CastleTowerModel") == null)
            {
                UnitModelBuilder.BuildCastleTower(gameObject, team);
            }
            if (agent != null) agent.enabled = false;
        }
        else
        {
            if (!isFlying && agent != null)
            {
                agent.enabled = true;
                agent.speed = 3.5f;
                agent.stoppingDistance = attackRange * 0.8f;

                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }
        }

        CacheRenderers();
        CreateHeadHpBar();
    }

    private void CacheRenderers()
    {
        allRenderers = GetComponentsInChildren<Renderer>();
        if (allRenderers != null && allRenderers.Length > 0)
        {
            originalColors = new Color[allRenderers.Length];
            for (int i = 0; i < allRenderers.Length; i++)
            {
                if (allRenderers[i].material != null)
                    originalColors[i] = allRenderers[i].material.color;
            }
        }
    }

    private void Update()
    {
        if (IsDead) return;
        if (BattleManager.CurrentGameState != BattleManager.GameState.Battle) return;

        if (isTower)
        {
            UpdateTowerDefense();
            return;
        }

        if (isBoss)
        {
            bossSkillTimer += Time.deltaTime;
            if (bossSkillTimer >= BossSkillInterval)
            {
                bossSkillTimer = 0f;
                ExecuteBossRoarSkill();
            }
        }

        if (isHealer)
        {
            UpdateHealerBehavior();
            return;
        }

        if (isFlying)
        {
            UpdateFlyingBehavior();
            return;
        }

        FindTarget();

        if (currentTarget != null && !currentTarget.IsDead)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (dist <= attackRange)
            {
                if (agent.enabled) agent.isStopped = true;
                if (Time.time - lastAttackTime >= attackInterval)
                {
                    Attack(currentTarget);
                    lastAttackTime = Time.time;
                }
            }
            else
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(currentTarget.transform.position);
                }
            }
        }
        else
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                float targetZ = (team == Team.Player) ? 20f : -20f;
                Vector3 marchPos = new Vector3(transform.position.x, transform.position.y, targetZ);
                agent.SetDestination(marchPos);
            }
        }
    }

    private void UpdateHealerBehavior()
    {
        Unit injuredAlly = null;
        float lowestHpRatio = 0.95f;

        for (int i = 0; i < AllUnits.Count; i++)
        {
            Unit u = AllUnits[i];
            if (u == null || u == this || u.team != this.team || u.IsDead) continue;

            float ratio = u.CurrentHp / u.MaxHp;
            if (ratio < lowestHpRatio)
            {
                lowestHpRatio = ratio;
                injuredAlly = u;
            }
        }

        if (injuredAlly != null)
        {
            float dist = Vector3.Distance(transform.position, injuredAlly.transform.position);
            if (dist <= attackRange)
            {
                if (agent.enabled) agent.isStopped = true;
                if (Time.time - lastAttackTime >= attackInterval)
                {
                    lastAttackTime = Time.time;
                    HealTarget(injuredAlly);
                }
            }
            else
            {
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(injuredAlly.transform.position);
                }
            }
        }
        else
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                float targetZ = (team == Team.Player) ? 20f : -20f;
                agent.SetDestination(new Vector3(transform.position.x, transform.position.y, targetZ));
            }
        }
    }

    private void HealTarget(Unit target)
    {
        float healAmount = 35f;
        target.ReceiveHeal(healAmount);

        if (VisualEffectManager.Instance != null)
        {
            VisualEffectManager.Instance.PlayHealEffect(target.transform.position);
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.Victory, 0.35f);
        }
    }

    public void ReceiveHeal(float amount)
    {
        if (IsDead) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        UpdateHealthBar();
        SpawnHealPopup(amount);
    }

    private void SpawnHealPopup(float amount)
    {
        GameObject popupObj = new GameObject("HealPopup");
        popupObj.transform.position = transform.position + Vector3.up * (barHeight + 0.3f);
        Destroy(popupObj, 0.65f);

        TextMesh tm = popupObj.AddComponent<TextMesh>();
        tm.text = $"+{Mathf.RoundToInt(amount)}";
        tm.fontSize = 28;
        tm.characterSize = 0.08f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(0.2f, 1f, 0.4f);
        tm.fontStyle = FontStyle.Bold;

        StartCoroutine(AnimateDamagePopup(popupObj, tm, tm.color));
    }

    private void UpdateFlyingBehavior()
    {
        Vector3 curPos = transform.position;
        curPos.y = Mathf.Lerp(curPos.y, FlyingAltitude, Time.deltaTime * 3f);
        transform.position = curPos;

        FindTarget();

        if (currentTarget != null && !currentTarget.IsDead)
        {
            Vector3 targetXZ = new Vector3(currentTarget.transform.position.x, FlyingAltitude, currentTarget.transform.position.z);
            float dist = Vector3.Distance(transform.position, targetXZ);

            if (dist <= attackRange)
            {
                if (Time.time - lastAttackTime >= attackInterval)
                {
                    lastAttackTime = Time.time;
                    ShootWyvernFireball(currentTarget);
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, targetXZ, 4.2f * Time.deltaTime);
                transform.forward = (targetXZ - transform.position).normalized;
            }
        }
        else
        {
            float targetZ = (team == Team.Player) ? 20f : -20f;
            Vector3 targetPos = new Vector3(0f, FlyingAltitude, targetZ);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, 4.2f * Time.deltaTime);
            transform.forward = (targetPos - transform.position).normalized;
        }
    }

    private void ShootWyvernFireball(Unit target)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.MeteorCast, 0.45f);
        }

        Vector3 firePos = transform.position + Vector3.down * 0.3f;
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "WyvernFireball";
        bullet.transform.position = firePos;
        bullet.transform.localScale = Vector3.one * 0.4f;

        Collider col = bullet.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = bullet.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(1f, 0.4f, 0.05f);
            rend.material = m;
        }

        ProjectileBehavior proj = bullet.AddComponent<ProjectileBehavior>();
        proj.Initialize(target, attackDamage, 13f, new Color(1f, 0.45f, 0.1f));
    }

    private void ExecuteBossRoarSkill()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.MeteorExplosion, 0.7f);
        }

        GameObject shock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shock.name = "Boss_RoarWave";
        shock.transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
        shock.transform.localScale = new Vector3(1f, 0.02f, 1f);

        Collider c = shock.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Renderer rend = shock.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = new Color(0.9f, 0.1f, 0.1f, 0.8f);
            rend.material = m;
        }

        StartCoroutine(ExpandRoarWave(shock));

        float radius = 5.0f;
        for (int i = 0; i < AllUnits.Count; i++)
        {
            Unit u = AllUnits[i];
            if (u == null || u.team != Team.Player || u.isTower || u.IsDead) continue;

            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= radius)
            {
                u.TakeDamage(25f);
            }
        }
    }

    private IEnumerator ExpandRoarWave(GameObject shock)
    {
        float dur = 0.35f;
        float el = 0f;
        Vector3 maxScale = new Vector3(10f, 0.02f, 10f);

        while (el < dur)
        {
            if (shock == null) yield break;
            el += Time.deltaTime;
            shock.transform.localScale = Vector3.Lerp(Vector3.one, maxScale, el / dur);
            yield return null;
        }
        if (shock != null) Destroy(shock);
    }

    private void Attack(Unit target)
    {
        if (target == null) return;

        if (attackRange > 3.0f)
        {
            ShootArcherProjectile(target);
        }
        else
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.Play(SoundManager.SoundType.MeleeHit, 0.4f);
            }
            target.TakeDamage(attackDamage);
        }
    }

    private void ShootArcherProjectile(Unit target)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.ShootArrow, 0.5f);
        }

        Vector3 firePos = transform.position + Vector3.up * 0.9f;
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = $"{team}_Arrow";
        bullet.transform.position = firePos;
        bullet.transform.localScale = Vector3.one * 0.25f;

        Collider col = bullet.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = bullet.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = (team == Team.Player) ? new Color(0.2f, 0.9f, 1f) : new Color(1f, 0.6f, 0.1f);
            rend.material = m;
        }

        ProjectileBehavior proj = bullet.AddComponent<ProjectileBehavior>();
        proj.Initialize(target, attackDamage, 15f, (team == Team.Player) ? Color.cyan : Color.red);
    }

    private void UpdateTowerDefense()
    {
        Unit closestEnemy = null;
        float minDistance = towerAttackRange;

        for (int i = 0; i < AllUnits.Count; i++)
        {
            Unit u = AllUnits[i];
            if (u == null || u.team == this.team || u.isTower || u.IsDead) continue;

            float d = Vector3.Distance(transform.position, u.transform.position);
            if (d <= minDistance)
            {
                minDistance = d;
                closestEnemy = u;
            }
        }

        if (closestEnemy != null)
        {
            if (Time.time - lastAttackTime >= towerAttackInterval)
            {
                lastAttackTime = Time.time;
                ShootTowerProjectile(closestEnemy);
            }
        }
    }

    private void ShootTowerProjectile(Unit target)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.TowerShoot, 0.6f);
        }

        Vector3 firePos = transform.position + Vector3.up * 3.4f;
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "TowerBullet";
        bullet.transform.position = firePos;
        bullet.transform.localScale = Vector3.one * 0.45f;

        Collider col = bullet.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = bullet.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(s);
            m.color = (team == Team.Player) ? Color.cyan : new Color(1f, 0.4f, 0f);
            rend.material = m;
        }

        ProjectileBehavior proj = bullet.AddComponent<ProjectileBehavior>();
        proj.Initialize(target, towerAttackDamage, 14f, (team == Team.Player) ? Color.cyan : new Color(1f, 0.5f, 0f));
    }

    private void LateUpdate()
    {
        if (hpBarTransform == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        if (isTower)
        {
            float zOffset = (team == Team.Player) ? 2.5f : -2.5f;
            Vector3 towerFrontPos = new Vector3(transform.position.x, 3.8f, transform.position.z + zOffset);
            hpBarTransform.position = towerFrontPos;
        }
        else
        {
            hpBarTransform.position = transform.position + Vector3.up * barHeight;
        }

        hpBarTransform.forward = cam.transform.forward;
    }

    private void FindTarget()
    {
        Unit bestUnit = null;
        float minUnitDist = float.MaxValue;
        Unit bestTower = null;
        float minTowerDist = float.MaxValue;

        for (int i = 0; i < AllUnits.Count; i++)
        {
            Unit other = AllUnits[i];
            if (other == null || other == this || other.team == this.team || other.IsDead)
                continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);

            if (!other.isTower)
            {
                if (dist < minUnitDist)
                {
                    minUnitDist = dist;
                    bestUnit = other;
                }
            }
            else
            {
                if (dist < minTowerDist)
                {
                    minTowerDist = dist;
                    bestTower = other;
                }
            }
        }

        currentTarget = (bestUnit != null) ? bestUnit : bestTower;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead) return;

        currentHp = Mathf.Max(0f, currentHp - damage);
        UpdateHealthBar();

        PlayHitFlash();
        SpawnDamagePopup(damage);

        if (currentHp <= 0f)
        {
            Die();
        }
    }

    private void PlayHitFlash()
    {
        if (allRenderers == null || allRenderers.Length == 0) return;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null && allRenderers[i].material != null)
                allRenderers[i].material.color = Color.white;
        }

        yield return new WaitForSeconds(0.08f);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null && allRenderers[i].material != null && i < originalColors.Length)
                allRenderers[i].material.color = originalColors[i];
        }
    }

    private void SpawnDamagePopup(float damage)
    {
        GameObject popupObj = new GameObject("DamagePopup");
        popupObj.transform.position = transform.position + Vector3.up * (barHeight + 0.3f) + new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f);

        Destroy(popupObj, 0.65f);

        TextMesh textMesh = popupObj.AddComponent<TextMesh>();
        textMesh.text = $"-{Mathf.RoundToInt(damage)}";
        textMesh.fontSize = isBoss ? 34 : 28;
        textMesh.characterSize = isBoss ? 0.1f : 0.08f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontStyle = FontStyle.Bold;

        Color textColor = (team == Team.Player) ? new Color(1f, 0.3f, 0.3f, 1f) : new Color(1f, 0.9f, 0.2f, 1f);
        textMesh.color = textColor;

        StartCoroutine(AnimateDamagePopup(popupObj, textMesh, textColor));
    }

    private IEnumerator AnimateDamagePopup(GameObject popupObj, TextMesh textMesh, Color baseColor)
    {
        float duration = 0.65f;
        float elapsed = 0f;
        Vector3 startPos = popupObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1.0f;

        while (elapsed < duration)
        {
            if (popupObj == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            popupObj.transform.position = Vector3.Lerp(startPos, endPos, Mathf.Sin(t * Mathf.PI * 0.5f));

            if (Camera.main != null)
            {
                popupObj.transform.forward = Camera.main.transform.forward;
            }

            Color c = baseColor;
            c.a = 1f - (t * t);
            if (textMesh != null) textMesh.color = c;

            yield return null;
        }

        if (popupObj != null) Destroy(popupObj);
    }

    private void CreateHeadHpBar()
    {
        hpBarObj = new GameObject($"{gameObject.name}_HpBar");
        hpBarTransform = hpBarObj.AddComponent<RectTransform>();

        hpBarObj.transform.SetParent(null);
        hpBarObj.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = hpBarObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;

        float width = isTower ? 180f : (isBoss ? 150f : 100f);
        float height = isTower ? 22f : (isBoss ? 20f : 15f);

        GameObject bgObj = new GameObject("Bg");
        bgObj.transform.SetParent(hpBarObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.8f);
        bgImg.raycastTarget = false;
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(width, height);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(hpBarObj.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = (team == Team.Player) ? new Color(0.2f, 0.75f, 1f) : new Color(1f, 0.2f, 0.2f);
        fillImg.raycastTarget = false;

        hpFillRect = fillObj.GetComponent<RectTransform>();
        hpFillRect.pivot = new Vector2(0f, 0.5f);
        hpFillRect.anchoredPosition = new Vector2(-width / 2f, 0f);
        hpFillRect.sizeDelta = new Vector2(width, height);
    }

    private void UpdateHealthBar()
    {
        if (hpFillRect != null)
        {
            float width = isTower ? 180f : (isBoss ? 150f : 100f);
            float height = isTower ? 22f : (isBoss ? 20f : 15f);
            float ratio = Mathf.Clamp01(currentHp / maxHp);
            hpFillRect.sizeDelta = new Vector2(width * ratio, height);
        }
    }

    private void Die()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.Play(SoundManager.SoundType.UnitDeath, (isTower || isBoss) ? 0.9f : 0.5f);
        }

        if (team == Team.Enemy && !isTower)
        {
            BattleManager.RegisterEnemyKill(isBoss);
        }

        SpawnDeathExplosion();
        Destroy(gameObject);
    }

    private void SpawnDeathExplosion()
    {
        int pieceCount = isTower ? 28 : (isBoss ? 22 : 10);
        float pieceScale = isTower ? 0.55f : (isBoss ? 0.45f : 0.28f);
        float explosionForce = isTower ? 9.0f : (isBoss ? 8.0f : 6.0f);
        Vector3 originPos = transform.position + Vector3.up * 0.8f;

        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Color baseCol = (allRenderers != null && allRenderers.Length > 0 && allRenderers[0] != null) ? allRenderers[0].material.color : Color.gray;

        for (int i = 0; i < pieceCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = "Debris";
            piece.transform.position = originPos + Random.insideUnitSphere * 0.4f;
            piece.transform.localScale = Vector3.one * Random.Range(pieceScale * 0.7f, pieceScale * 1.3f);

            Renderer rend = piece.GetComponent<Renderer>();
            if (rend != null)
            {
                Material m = new Material(s);
                m.color = baseCol;
                rend.material = m;
            }

            Rigidbody rb = piece.AddComponent<Rigidbody>();
            rb.mass = 0.2f;

            Vector3 randomDir = (Random.insideUnitSphere + Vector3.up * 1.5f).normalized;
            rb.AddForce(randomDir * Random.Range(explosionForce * 0.6f, explosionForce * 1.3f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);

            BoxCollider col = piece.GetComponent<BoxCollider>();
            if (col != null) col.size = Vector3.one * 0.9f;

            DebrisBehavior debrisComp = piece.AddComponent<DebrisBehavior>();
            debrisComp.Initialize(Random.Range(0.8f, 1.1f));
        }
    }

    private void OnDestroy()
    {
        if (hpBarObj != null)
        {
            Destroy(hpBarObj);
        }
    }
}

// ==========================================
// 以下の弾丸クラスと破片クラスが抜けていたため復元
// ==========================================

public class ProjectileBehavior : MonoBehaviour
{
    private Unit targetUnit;
    private float damage;
    private float speed;
    private Color sparkColor;

    public void Initialize(Unit target, float dmg, float spd, Color sparkCol)
    {
        targetUnit = target;
        damage = dmg;
        speed = spd;
        sparkColor = sparkCol;

        Destroy(gameObject, 2.5f);
    }

    private void Update()
    {
        if (targetUnit != null && !targetUnit.IsDead)
        {
            Vector3 targetPos = targetUnit.transform.position + Vector3.up * 0.8f;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos) < 0.4f)
            {
                targetUnit.TakeDamage(damage);
                if (VisualEffectManager.Instance != null)
                {
                    VisualEffectManager.Instance.PlayHitSpark(transform.position, sparkColor);
                }
                Destroy(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
}

public class DebrisBehavior : MonoBehaviour
{
    private float lifeTime;
    private float elapsed;
    private Vector3 initialScale;

    public void Initialize(float duration)
    {
        lifeTime = duration;
        elapsed = 0f;
        initialScale = transform.localScale;

        Destroy(gameObject, 1.5f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / lifeTime;

        if (t > 0.5f)
        {
            float shrinkT = (t - 0.5f) / 0.5f;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, shrinkT * shrinkT);
        }

        if (elapsed >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}