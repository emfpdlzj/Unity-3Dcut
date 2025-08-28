using UnityEngine;

public class SimpleMouseCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;   // microElbow 루트 등
    public Transform pivot;    // 회전/줌 기준(없으면 자동 생성)
    public Camera cam;         // 비우면 MainCamera

    [Header("Rotate (RMB)")]
    public float rotateDegPerPixel = 0.20f; // 우클릭 드래그 회전 감도(°/px)
    public bool invertX = false;
    public bool invertY = false;
    public Vector2 pitchClamp = new Vector2(-89f, 89f);

    [Header("Pan (LMB)")]
    public float panSpeedScale = 1.0f;      // 좌클릭 드래그 이동 감도(픽셀→월드 변환에 곱)

    [Header("Zoom (Wheel)")]
    public float minDistance = 0.3f;
    public float maxDistance = 100f;
    public float wheelZoomStrength = 0.12f; // 휠 1틱당 지수 비율

    [Header("Start")]
    public bool frameTargetOnStart = true;  // 시작 시 타겟 중앙으로 프레이밍

    [Header("Stabilization (camera only)")]
    public float camSmoothTime = 0.08f;     // 카메라 위치 스무스 시간(초)
    public float camRotDamping = 18f;       // 카메라 회전 감쇠(지수)

    // ── 내부 상태 ─────────────────────────────────────────────
    float yaw, pitch;
    float desiredDistance;
    Vector3 desiredPivotPos;
    Vector3 lastMousePos;
    Vector3 _camVel; // SmoothDamp 내부 속도

    void Reset()
    {
        cam = Camera.main;
        if (transform.childCount > 0) pivot = transform.GetChild(0);
    }

    void Start()
    {
        if (!cam) cam = Camera.main;
        if (!pivot)
        {
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
        }

        if (target)
        {
            desiredPivotPos = GetTargetBounds(target).center;
            pivot.position = desiredPivotPos;
        }
        else
        {
            desiredPivotPos = pivot.position;
        }

        // 초기 각도/거리
        Vector3 toCam = (cam.transform.position - pivot.position).normalized;
        yaw   = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(toCam.y) * Mathf.Rad2Deg;
        desiredDistance = Mathf.Clamp(Vector3.Distance(cam.transform.position, pivot.position), minDistance, maxDistance);

        if (frameTargetOnStart && target) FrameTarget(true);

        lastMousePos = Input.mousePosition;
    }

    void Update()
    {
        Vector2 pixelDelta = (Vector2)(Input.mousePosition - lastMousePos);

        HandlePan(pixelDelta);     // LMB = 이동
        HandleRotate(pixelDelta);  // RMB = 회전
        HandleWheelZoom();         // Wheel = 줌

        lastMousePos = Input.mousePosition;
    }

    void LateUpdate()
    {
        // 튐 제거: pivot/거리 = 즉시 적용, "카메라만" 부드럽게 보간
        // 1) 목표 포즈 계산
        pitch = Mathf.Clamp(pitch, pitchClamp.x, pitchClamp.y);
        Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);

        pivot.position = desiredPivotPos;   // 보간하지 않음
        pivot.rotation = desiredRot;        // 보간하지 않음

        Vector3 desiredCamPos = pivot.position - (desiredRot * Vector3.forward) * desiredDistance;
        Quaternion desiredCamRot = Quaternion.LookRotation(pivot.position - desiredCamPos, Vector3.up);

        // 2) 카메라만 스무스
        cam.transform.position = Vector3.SmoothDamp(
            cam.transform.position,
            desiredCamPos,
            ref _camVel,
            camSmoothTime
        );

        float rotT = 1f - Mathf.Exp(-camRotDamping * Time.deltaTime);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, desiredCamRot, rotT);
    }

    // ── 입력 처리 ─────────────────────────────────────────────

    void HandlePan(Vector2 pixelDelta)
    {
        if (!Input.GetMouseButton(0)) return; // 좌클릭 드래그
        float worldPerPixel = PanPixelToWorld(desiredDistance);
        Vector3 right = cam.transform.right;
        Vector3 up    = cam.transform.up;

        // 에디터 감각: 마우스를 오른쪽/위로 → 장면이 반대로 이동(피벗은 반대로)
        desiredPivotPos -= right * pixelDelta.x * worldPerPixel * panSpeedScale;
        desiredPivotPos -= up    * pixelDelta.y * worldPerPixel * panSpeedScale;
    }

    void HandleRotate(Vector2 pixelDelta)
    {
        if (!Input.GetMouseButton(1)) return; // 우클릭 드래그
        float sx = invertX ? -1f : 1f;
        float sy = invertY ? -1f : 1f;
        yaw   += sx * pixelDelta.x * rotateDegPerPixel;
        pitch -= sy * pixelDelta.y * rotateDegPerPixel;
    }

    void HandleWheelZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.0001f) return;

        // 지수 스케일 줌: 가까울 때 미세, 멀 때 강하게
        float k = scroll * wheelZoomStrength;
        desiredDistance = Mathf.Clamp(desiredDistance * Mathf.Exp(-k), minDistance, maxDistance);
    }

    float PanPixelToWorld(float distance)
    {
        // 화면 1픽셀 → 월드 이동량 (FOV/거리 기반)
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        return 2f * Mathf.Tan(fovRad * 0.5f) * distance / Mathf.Max(Screen.height, 1);
    }

    // ── 타깃 프레이밍 ─────────────────────────────────────────

    public void FrameTarget(bool instant = false)
    {
        if (!target) return;

        Bounds b = GetTargetBounds(target);
        desiredPivotPos = b.center;

        // 바운딩 구면 기준 거리(여유 1.2배) — tan이 정확
        float radius = Mathf.Max(b.extents.magnitude, 0.001f);
        float fov = cam.fieldOfView * Mathf.Deg2Rad;
        float dist = radius / Mathf.Tan(fov * 0.5f) * 1.2f;
        desiredDistance = Mathf.Clamp(dist, minDistance, maxDistance);

        if (instant)
        {
            pivot.position = desiredPivotPos;
            Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 camPos = pivot.position - (desiredRot * Vector3.forward) * desiredDistance;
            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.LookRotation(pivot.position - camPos, Vector3.up);
            _camVel = Vector3.zero;
        }
    }

    Bounds GetTargetBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 0.1f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
