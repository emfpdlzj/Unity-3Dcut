using UnityEngine;
using System.Linq;

[ExecuteAlways] // 에디터 모드에서도 동작 (씬에서 미리보기/조정 가능)
public class SectionPlaneMarker : MonoBehaviour
{
    // ── 좌표계 및 부모 설정 ─────────────────────────────
    public enum CoordSpace { World, Local }                  // 프리셋 좌표를 월드/로컬 기준으로 적용
    public enum ParentMode { ThisObject, WorldRoot, Custom } // 마커를 어디에 귀속할지

    // ── 프리셋 구조체: 절단면 위치/회전/스케일 정보 ─────────────────
    [System.Serializable]
    public struct CutPreset
    {
        public string  label;     // 인스펙터에서 구분용 이름
        public Vector3 position;  // 기준 좌표 (World/Local 또는 followTarget 로컬)
        public Vector3 rotation;  // Euler 각도
        public Vector3 scale;     // Quad 스케일(보이는 크기 또는 타깃 로컬 단위)
    }

    [Header("Material")]
    public Material redUnlitMaterial; // 절단면에 적용할 머티리얼 (Unlit 권장)

    [Header("Presets")]
    public CutPreset[] presets = new CutPreset[4]; // 최대 4개의 절단면 설정

    [Header("Placement / Space")]
    public CoordSpace coordSpace = CoordSpace.World;     // 좌표계 기준
    public ParentMode parentMode = ParentMode.WorldRoot; // 부모 모드 (월드 루트 권장)
    public Transform customParent;                       // ParentMode.Custom일 경우 부모 지정
    public bool startHidden = true;                      // 시작 시 절단면 숨김 여부
    public int[] startActiveIndices;                     // 시작 시 표시할 인덱스들

    [Header("Stability")]
    public bool lockEveryFrame = true;    // true → Preset 값으로 매 프레임 덮어씀 (좌표 고정)
                                          // false → 마커 직접 이동 시 그 값을 Preset에 기록
    public bool ignoreParentScale = true; // 부모 오브젝트 스케일 영향 배제 여부

    [SerializeField, HideInInspector] Transform[] markers; // 프리셋별 실제 Quad 참조

    // ── microElbow 추적 관련 ─────────────────────────────
    public enum RotationFollowMode
    {
        CopyTarget,        // 타깃 회전 그대로 복사
        TargetPlusPreset,  // 타깃 회전 * 프리셋 오프셋 (기본)
        AlignTargetAxis    // 타깃 로컬축을 절단면 법선으로 정렬
    }

    public enum TargetAxis { Forward, Up, Right }

    [Header("Follow Target (no parenting)")]
    public Transform followTarget;            // microElbow 등, 따라갈 대상
    public bool relativeToTarget = true;      // true면 프리셋 좌표를 followTarget 로컬 기준으로 해석
    public bool scaleWithTarget = true;       // true면 타깃 스케일 변화에 절단면 크기도 비례
    public RotationFollowMode rotationMode = RotationFollowMode.TargetPlusPreset;
    public TargetAxis alignAxis = TargetAxis.Forward; // AlignTargetAxis 모드에서 사용할 축

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        EnsureMarkersSized();       // 프리셋 개수와 markers 배열 동기화
        EnsureMarkersExist();       // Quad 오브젝트 생성
        ApplyAllPresetsToMarkers(); // Preset → Marker 적용
        if (startHidden) HideAll();
        else ShowOnly(startActiveIndices);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 인스펙터 값이 바뀔 때마다 즉시 반영 (에디터 모드)
        EnsureMarkersSized();
        EnsureMarkersExist();
        ApplyAllPresetsToMarkers();
        if (startHidden) HideAll();
        else ShowOnly(startActiveIndices);
    }
#endif

    void Update()
    {
        if (markers == null) return;

        // 프리셋 고정/기록 모드
        if (lockEveryFrame)
        {
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m != null && m.gameObject.activeSelf)
                    ApplyPresetToMarker(i);
            }
        }
        else
        {
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m != null && m.gameObject.activeSelf)
                    CaptureMarkerToPreset(i);
            }
        }

        // 숫자키 단축키
        if (Input.GetKeyDown(KeyCode.Alpha1)) Toggle(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) Toggle(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) Toggle(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) Toggle(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) Toggle(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) Toggle(5);
        if (Input.GetKeyDown(KeyCode.Alpha0)) HideAll(); // 0키: 전체 숨김
    }

    // ───────── 내부 유틸 ─────────

    // Preset 배열 크기와 markers 배열 크기를 맞춤
    void EnsureMarkersSized()
    {
        int n = Mathf.Max(0, presets?.Length ?? 0);
        if (markers == null || markers.Length != n) markers = new Transform[n];
    }

    // 부모 오브젝트 결정
    Transform GetTargetParent()
    {
        switch (parentMode)
        {
            case ParentMode.WorldRoot: return null; // 씬 루트에 둠
            case ParentMode.Custom:    return customParent ? customParent : null;
            default:                   return transform; // 현재 오브젝트에 귀속
        }
    }

    // Preset 개수만큼 Quad 생성 (CutPlane_1~N)
    void EnsureMarkersExist()
    {
        var parent = GetTargetParent();

        for (int i = 0; i < (markers?.Length ?? 0); i++)
        {
            if (markers[i] == null)
            {
                // 새 Quad 생성
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = $"CutPlane_{i + 1}";

                // 충돌체 제거
                var col = go.GetComponent<Collider>();
#if UNITY_EDITOR
                DestroyImmediate(col);
#else
                if (col) Destroy(col);
#endif
                // 머티리얼 적용
                var mr = go.GetComponent<MeshRenderer>();
                if (mr && redUnlitMaterial) mr.sharedMaterial = redUnlitMaterial;

                var t = go.transform;
                t.SetParent(parent, true);
                markers[i] = t;
                markers[i].gameObject.SetActive(false); // 기본은 숨김
            }
            else
            {
                // 부모 변경 시 다시 귀속
                var wantParent = parent;
                if (markers[i].parent != wantParent)
                    markers[i].SetParent(wantParent, true);
            }
        }
    }

    // 부모 스케일을 고려해 "보이는 월드 스케일"을 로컬스케일로 환산
    Vector3 WorldToLocalScale(Vector3 desiredWorldScale, Transform parent)
    {
        if (ignoreParentScale && parent != null)
        {
            Vector3 ps = parent.lossyScale;
            // 0 가드
            ps.x = ps.x == 0 ? 1 : ps.x;
            ps.y = ps.y == 0 ? 1 : ps.y;
            ps.z = ps.z == 0 ? 1 : ps.z;
            return new Vector3(
                desiredWorldScale.x / Mathf.Abs(ps.x),
                desiredWorldScale.y / Mathf.Abs(ps.y),
                desiredWorldScale.z / Mathf.Abs(ps.z)
            );
        }
        return desiredWorldScale; // 부모 스케일 영향 무시 안 하면 로컬=월드로 가정
    }

    // ── Preset → Marker (Push) ─────────────────────────
    public void ApplyAllPresetsToMarkers()
    {
        for (int i = 0; i < (markers?.Length ?? 0); i++)
            ApplyPresetToMarker(i);
    }

    public void ApplyPresetToMarker(int index)
    {
        if (!IsValid(index)) return;
        var m = markers[index];
        var p = presets[index];

        // ── ① followTarget 기준 상대 적용(우선 적용) ─────────────────
        if (relativeToTarget && followTarget != null)
        {
            // 위치: 타깃 로컬 → 월드
            Vector3 worldPos = followTarget.TransformPoint(p.position);

            // 회전: 모드별
            Quaternion worldRot;
            switch (rotationMode)
            {
                case RotationFollowMode.CopyTarget:
                    worldRot = followTarget.rotation;
                    break;

                case RotationFollowMode.AlignTargetAxis:
                    Vector3 normal;
                    switch (alignAxis)
                    {
                        case TargetAxis.Up:    normal = followTarget.up; break;
                        case TargetAxis.Right: normal = followTarget.right; break;
                        default:               normal = followTarget.forward; break;
                    }
                    // 법선을 타깃 축에 맞추고, 프리셋 회전을 추가 오프셋으로 적용
                    worldRot = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(p.rotation);
                    break;

                default: // TargetPlusPreset
                    worldRot = followTarget.rotation * Quaternion.Euler(p.rotation);
                    break;
            }

            // 스케일: 타깃 스케일에 비례(선택)
            Vector3 desiredWorldScale = p.scale;
            if (scaleWithTarget)
            {
                Vector3 ts = followTarget.lossyScale;
                desiredWorldScale = new Vector3(
                    p.scale.x * Mathf.Abs(ts.x),
                    p.scale.y * Mathf.Abs(ts.y),
                    p.scale.z * Mathf.Abs(ts.z)
                );
            }

            m.position   = worldPos;
            m.rotation   = worldRot;
            m.localScale = WorldToLocalScale(desiredWorldScale, m.parent);
            return;
        }

        // ── ② 기존 동작(월드/로컬) 유지 ───────────────────────────────
        if (coordSpace == CoordSpace.World)
        {
            m.position = p.position;
            m.rotation = Quaternion.Euler(p.rotation);
            m.localScale = WorldToLocalScale(p.scale, m.parent);
        }
        else // Local
        {
            m.localPosition = p.position;
            m.localRotation = Quaternion.Euler(p.rotation);
            m.localScale    = p.scale;
        }
    }

    // ── Marker → Preset (Pull) ─────────────────────────
    public void CaptureAllMarkersToPresets()
    {
        for (int i = 0; i < (markers?.Length ?? 0); i++)
            CaptureMarkerToPreset(i);
    }

    public void CaptureMarkerToPreset(int index)
    {
        if (!IsValid(index)) return;
        var m = markers[index];

        // ── ① followTarget 기준으로 프리셋에 저장(우선 적용) ──────────
        if (relativeToTarget && followTarget != null)
        {
            // 월드 → 타깃 로컬
            presets[index].position = followTarget.InverseTransformPoint(m.position);
            Quaternion localRot = Quaternion.Inverse(followTarget.rotation) * m.rotation;
            presets[index].rotation = localRot.eulerAngles;

            // 현재 보이는 크기(월드 스케일)
            Vector3 worldScale = m.lossyScale;

            // 프리셋에는 "타깃 로컬 단위"로 저장해야 나중에 타깃 스케일이 변해도 같은 비율 유지
            if (scaleWithTarget)
            {
                Vector3 ts = followTarget.lossyScale;
                ts.x = ts.x == 0 ? 1 : ts.x;
                ts.y = ts.y == 0 ? 1 : ts.y;
                ts.z = ts.z == 0 ? 1 : ts.z;

                presets[index].scale = new Vector3(
                    worldScale.x / Mathf.Abs(ts.x),
                    worldScale.y / Mathf.Abs(ts.y),
                    worldScale.z / Mathf.Abs(ts.z)
                );
            }
            else
            {
                // 타깃 스케일 비례 X → 그냥 보이는 월드 크기를 저장
                presets[index].scale = worldScale;
            }
            return;
        }

        // ── ② 기존 동작(월드/로컬) 유지 ───────────────────────────────
        if (coordSpace == CoordSpace.World)
        {
            presets[index].position = m.position;
            presets[index].rotation = m.rotation.eulerAngles;

            // 보이는 월드 크기로 저장 (부모 스케일 보정)
            if (ignoreParentScale && m.parent != null)
            {
                Vector3 ps = m.parent.lossyScale;
                ps.x = ps.x == 0 ? 1 : ps.x;
                ps.y = ps.y == 0 ? 1 : ps.y;
                ps.z = ps.z == 0 ? 1 : ps.z;
                presets[index].scale = new Vector3(
                    m.localScale.x * Mathf.Abs(ps.x),
                    m.localScale.y * Mathf.Abs(ps.y),
                    m.localScale.z * Mathf.Abs(ps.z)
                );
            }
            else
            {
                presets[index].scale = m.localScale;
            }
        }
        else // Local
        {
            presets[index].position = m.localPosition;
            presets[index].rotation = m.localRotation.eulerAngles;
            presets[index].scale    = m.localScale;
        }
    }

    // ── 표시 제어 함수들 ─────────────────────────────
    public void Show(int index)
    {
        if (!IsValid(index)) return;
        markers[index].gameObject.SetActive(true);
        ApplyPresetToMarker(index);
    }

    public void Hide(int index)
    {
        if (!IsValid(index)) return;
        markers[index].gameObject.SetActive(false);
    }

    public void Toggle(int index)
    {
        if (!IsValid(index)) return;
        var go = markers[index].gameObject;
        go.SetActive(!go.activeSelf);
        if (go.activeSelf) ApplyPresetToMarker(index);
    }

    public void ShowOnly(params int[] indices)
    {
        indices = indices ?? System.Array.Empty<int>();
        for (int i = 0; i < (markers?.Length ?? 0); i++)
        {
            bool on = indices.Contains(i);
            var m = markers[i];
            if (!m) continue;
            m.gameObject.SetActive(on);
            if (on) ApplyPresetToMarker(i);
        }
    }

    public void HideAll()
    {
        for (int i = 0; i < (markers?.Length ?? 0); i++)
            if (markers[i]) markers[i].gameObject.SetActive(false);
    }

    // 인덱스 범위 체크
    bool IsValid(int index) =>
        markers != null && presets != null &&
        index >= 0 && index < markers.Length && index < presets.Length &&
        markers[index] != null;

    // ── 디버그: 마커 축 기즈모 ─────────────────────────
    void OnDrawGizmosSelected()
    {
        if (markers == null) return;
        foreach (var m in markers)
        {
            if (!m) continue;
            Gizmos.color = Color.red;   Gizmos.DrawLine(m.position, m.position + m.right * 0.2f);
            Gizmos.color = Color.green; Gizmos.DrawLine(m.position, m.position + m.up * 0.2f);
            Gizmos.color = Color.blue;  Gizmos.DrawLine(m.position, m.position + m.forward * 0.2f);
        }
    }
}
