using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;       // 바라볼 대상(없으면 현재 위치를 기준)
    public Transform pivot;        // 회전 피벗(보통 자식 오브젝트)
    public Camera cam;             // 사용할 카메라(비우면 자동)

    [Header("Orbit")]
    public float orbitSpeed = 2160f;   // 좌클릭 회전 속도(3배 ↑)
    public bool invertX = false;
    public bool invertY = false;

    [Header("Zoom")]
    public float zoomSpeed = 10f;      // 휠 줌 속도
    public float minDistance = 1.0f;   // 피벗~카메라 최소 거리
    public float maxDistance = 30f;    // 최대 거리
    public float zoomDamping = 10f;    // 줌 부드럽게

    [Header("Pan (RMB drag)")]
    public float panSpeed = 21.0f;     // 우클릭 패닝 속도(3배 ↑)
    public float panDamping = 10f;

    [Header("Smoothing")]
    public float orbitDamping = 12f;   // 회전 보간

    [Header("Focus")]
    public KeyCode focusKey = KeyCode.F; // 대상 프레이밍

    // 내부 상태
    float yaw, pitch;               // 각도 제한 없음 (무제한 회전)
    float desiredDistance;
    Vector3 desiredPivotPos;

    void Reset()
    {
        cam = Camera.main;
        if (transform.childCount > 0 && transform.GetChild(0) != null)
            pivot = transform.GetChild(0);
    }

    void Start()
    {
        if (!cam) cam = Camera.main;
        if (!pivot)
        {
            // 없으면 자동 생성
            var p = new GameObject("Pivot").transform;
            p.SetParent(transform, false);
            pivot = p;
        }

        if (target)
        {
            // 타겟으로 피벗 이동
            desiredPivotPos = GetTargetCenter(target);
            pivot.position = desiredPivotPos;
        }
        else
        {
            desiredPivotPos = pivot.position;
        }

        // 초기 각도 계산
        Vector3 toCam = (cam.transform.position - pivot.position).normalized;
        yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(toCam.y) * Mathf.Rad2Deg;

        desiredDistance = Vector3.Distance(cam.transform.position, pivot.position);
    }

    void Update()
    {
        if (target && Input.GetKeyDown(focusKey))
            FrameTarget();

        HandleOrbit();
        HandleZoom();
        HandlePan();
    }

    void LateUpdate()
    {
        // 피벗 위치 보간
        pivot.position = Vector3.Lerp(
            pivot.position,
            desiredPivotPos,
            1f - Mathf.Exp(-panDamping * Time.deltaTime)
        );

        // 각도 제한 없음 → 그대로 사용
        Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);
        pivot.rotation = Quaternion.Slerp(
            pivot.rotation,
            desiredRot,
            1f - Mathf.Exp(-orbitDamping * Time.deltaTime)
        );

        // 거리 보간
        float currentDist = Vector3.Distance(cam.transform.position, pivot.position);
        float newDist = Mathf.Lerp(
            currentDist,
            desiredDistance,
            1f - Mathf.Exp(-zoomDamping * Time.deltaTime)
        );

        cam.transform.position = pivot.position - pivot.forward * newDist;
        cam.transform.LookAt(pivot.position);
    }

    void HandleOrbit()
    {
        // 좌클릭 드래그 시 오비트
        if (Input.GetMouseButton(0))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");

            float xSign = invertX ? -1f : 1f;
            float ySign = invertY ? -1f : 1f;

            yaw   += xSign * dx * orbitSpeed * Time.deltaTime;
            pitch -= ySign * dy * orbitSpeed * Time.deltaTime; // 제한 없음
        }

        // 터치(모바일) 대응: 한 손가락 드래그로 오비트
        if (Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                Vector2 d = t.deltaPosition;
                yaw   += d.x * 0.6f;   // (기존 0.2f → 3배 민감하게 하려면 0.6f)
                pitch -= d.y * 0.6f;   // 제한 없음
            }
        }
    }

    void HandleZoom()
    {
        // 마우스 휠
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            desiredDistance -= scroll * zoomSpeed;
            desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
        }

        // 터치 핀치
        if (Input.touchCount == 2)
        {
            var t0 = Input.GetTouch(0);
            var t1 = Input.GetTouch(1);
            var prevDist = (t0.position - t0.deltaPosition - (t1.position - t1.deltaPosition)).magnitude;
            var currDist = (t0.position - t1.position).magnitude;
            float diff = currDist - prevDist;
            desiredDistance -= diff * 0.03f; // (기존 0.01f → 3배 민감)
            desiredDistance = Mathf.Clamp(desiredDistance, minDistance, maxDistance);
        }
    }

    void HandlePan()
    {
        // 우클릭 드래그로 패닝(카메라 평면 기준)
        if (Input.GetMouseButton(1))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");

            // 거리 비례로 스케일(가까우면 세밀, 멀면 크게)
            float scale = desiredDistance * panSpeed * Time.deltaTime;

            // 카메라 기준 오른쪽/위 벡터
            Vector3 right = cam.transform.right;
            Vector3 up    = cam.transform.up;

            desiredPivotPos -= right * dx * scale;
            desiredPivotPos -= up    * dy * scale;
        }
    }

    void FrameTarget()
    {
        if (!target) return;

        // 타겟 바운딩 박스 구해서 화면에 꽉 차게
        Bounds b = GetTargetBounds(target);
        desiredPivotPos = b.center;

        float radius = b.extents.magnitude;
        radius = Mathf.Max(radius, 0.5f);

        // 시야각 기반으로 적절한 거리 계산(여유분 1.2배)
        float fov = cam.fieldOfView * Mathf.Deg2Rad;
        float dist = radius / Mathf.Sin(fov * 0.5f) * 1.2f;

        desiredDistance = Mathf.Clamp(dist, minDistance, maxDistance);
    }

    Vector3 GetTargetCenter(Transform t)
    {
        return GetTargetBounds(t).center;
    }

    Bounds GetTargetBounds(Transform t)
    {
        Renderer[] rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one);

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            b.Encapsulate(rs[i].bounds);
        return b;
    }
}
