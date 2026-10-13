using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("移動速度設定")]
    [SerializeField] private float moveSpeed = 18f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private float dragSpeed = 0.8f;

    [Header("移動・ズーム範囲制限")]
    [SerializeField] private Vector2 xLimit = new Vector2(-12f, 12f);
    [SerializeField] private Vector2 zLimit = new Vector2(-32f, 24f);
    [SerializeField] private Vector2 yZoomLimit = new Vector2(10f, 38f);

    private Vector3 defaultPosition = new Vector3(0f, 27f, -22f);
    private Quaternion defaultRotation = Quaternion.Euler(60f, 0f, 0f);

    private Vector3 currentBasePosition;
    private Vector3 lastMousePos;

    // スクリーンシェイク用変数
    private float shakeTimer = 0f;
    private float shakeMagnitude = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        currentBasePosition = defaultPosition;
        transform.position = defaultPosition;
        transform.rotation = defaultRotation;
    }

    private void Update()
    {
        // ポーズ中でもカメラ移動ができるように unscaledDeltaTime を使用
        float dt = Time.unscaledDeltaTime;

        // 1. WASD / 矢印キー移動
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            Vector3 move = (right * h + forward * v).normalized * moveSpeed * dt;
            currentBasePosition += move;
        }

        // 2. マウスホイールズーム
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newY = Mathf.Clamp(currentBasePosition.y - scroll * zoomSpeed * 10f, yZoomLimit.x, yZoomLimit.y);
            float deltaY = newY - currentBasePosition.y;
            currentBasePosition += new Vector3(0f, deltaY, deltaY * 0.4f);
        }

        // 3. マウスドラッグパン
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            Vector3 pan = new Vector3(-delta.x * dragSpeed * dt * 0.8f, 0f, -delta.y * dragSpeed * dt * 0.8f);
            currentBasePosition += pan;
            lastMousePos = Input.mousePosition;
        }

        // 4. [F] でリセット
        if (Input.GetKeyDown(KeyCode.F))
        {
            currentBasePosition = defaultPosition;
            transform.rotation = defaultRotation;
        }

        // 移動制限クランプ
        currentBasePosition.x = Mathf.Clamp(currentBasePosition.x, xLimit.x, xLimit.y);
        currentBasePosition.y = Mathf.Clamp(currentBasePosition.y, yZoomLimit.x, yZoomLimit.y);
        currentBasePosition.z = Mathf.Clamp(currentBasePosition.z, zLimit.x, zLimit.y);

        // 5. 画面揺れ（Screen Shake）の加算
        Vector3 shakeOffset = Vector3.zero;
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            shakeOffset = Random.insideUnitSphere * shakeMagnitude;
        }

        transform.position = currentBasePosition + shakeOffset;
    }

    /// <summary>
    /// 画面揺れを発動（duration: 秒数, magnitude: 揺れの強さ）
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        shakeTimer = duration;
        shakeMagnitude = magnitude;
    }

    /// <summary>
    /// ヒットストップ（一瞬の時間の極小化で打撃感を付与）
    /// </summary>
    public void TriggerHitStop(float duration = 0.05f)
    {
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        float originalTimeScale = Time.timeScale;
        Time.timeScale = 0.05f; // ほぼ停止状態
        yield return new WaitForSecondsRealtime(duration);
        if (BattleManager.CurrentGameState == BattleManager.GameState.Battle && !BattleManager.IsGamePaused)
        {
            Time.timeScale = BattleManager.GameSpeedMultiplier;
        }
        else if (BattleManager.IsGamePaused)
        {
            Time.timeScale = 0f;
        }
    }
}