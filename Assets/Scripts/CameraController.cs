using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("移動速度設定")]
    [SerializeField] private float moveSpeed = 18f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private float dragSpeed = 0.8f;

    [Header("移動・ズーム範囲制限（拡張フィールド対応）")]
    [SerializeField] private Vector2 xLimit = new Vector2(-12f, 12f);
    [SerializeField] private Vector2 zLimit = new Vector2(-32f, 24f); // Z軸を広範囲に拡張
    [SerializeField] private Vector2 yZoomLimit = new Vector2(10f, 38f);

    private Vector3 defaultPosition = new Vector3(0f, 27f, -22f);
    private Quaternion defaultRotation = Quaternion.Euler(60f, 0f, 0f);

    private Vector3 lastMousePos;

    private void Start()
    {
        transform.position = defaultPosition;
        transform.rotation = defaultRotation;
    }

    private void Update()
    {
        // 1. WASD / 矢印キー移動
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
        {
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            Vector3 move = (right * h + forward * v).normalized * moveSpeed * Time.deltaTime;
            transform.position += move;
        }

        // 2. マウスホイールズーム
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newY = Mathf.Clamp(transform.position.y - scroll * zoomSpeed * 10f, yZoomLimit.x, yZoomLimit.y);
            float deltaY = newY - transform.position.y;
            transform.position += new Vector3(0f, deltaY, deltaY * 0.4f);
        }

        // 3. マウス右ドラッグ / 中クリックパン
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            Vector3 pan = new Vector3(-delta.x * dragSpeed * Time.deltaTime * 0.8f, 0f, -delta.y * dragSpeed * Time.deltaTime * 0.8f);
            transform.position += pan;
            lastMousePos = Input.mousePosition;
        }

        // 4. [F] キーでリセット
        if (Input.GetKeyDown(KeyCode.F))
        {
            transform.position = defaultPosition;
            transform.rotation = defaultRotation;
        }

        // 移動制限クランプ
        Vector3 clampedPos = transform.position;
        clampedPos.x = Mathf.Clamp(clampedPos.x, xLimit.x, xLimit.y);
        clampedPos.y = Mathf.Clamp(clampedPos.y, yZoomLimit.x, yZoomLimit.y);
        clampedPos.z = Mathf.Clamp(clampedPos.z, zLimit.x, zLimit.y);
        transform.position = clampedPos;
    }
}