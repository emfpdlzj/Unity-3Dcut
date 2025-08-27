using UnityEngine;
using System.Linq;

[ExecuteAlways] // 에디터 모드에서도 동작 (씬에서 미리보기/조정 가능)
public class SectionPlaneMarker : MonoBehaviour
{
    // ── 좌표계 및 부모 설정 ─────────────────────────────
    public enum CoordSpace { World, Local }          // 프리셋 좌표를 월드/로컬 기준으로 적용
    public enum ParentMode { ThisObject, WorldRoot, Custom } // 마커를 어디에 귀속할지

    // ── 프리셋 구조체: 절단면 위치/회전/스케일 정보 ─────────────────
    [System.Serializable]
    public struct CutPreset
    {
        public string  label;     // 인스펙터에서 구분용 이름
        public Vector3 position;  // 기준 좌표 (World/Local)
        public Vector3 rotation;  // Euler 각도
        public Vector3 scale;     // Quad 스케일
    }

    [Header("Material")]
    public Material redUnlitMaterial; // 절단면에 적용할 머티리얼 (빨간색 추천)

    [Header("Presets")]
    public CutPreset[] presets = new CutPreset[4]; // 최대 4개의 절단면 설정

    [Header("Placement / Space")]
    public CoordSpace coordSpace = CoordSpace.World;    // 좌표계 기준
    public ParentMode parentMode = ParentMode.WorldRoot; // 부모 모드 (월드 루트 권장)
    public Transform customParent;                      // ParentMode.Custom일 경우 지정
    public bool startHidden = true;                     // 시작 시 절단면 숨김 여부
    public int[] startActiveIndices;                    // 시작 시 표시할 인덱스들

    [Header("Stability")]
    public bool lockEveryFrame = true;  // true → Preset 값으로 매 프레임 덮어씀 (좌표 고정)
                                        // false → 마커 직접 이동 가능, 값은 Preset에 기록됨
    public bool ignoreParentScale = true; // 부모 오브젝트 스케일 영향 배제 여부

    [SerializeField, HideInInspector] Transform[] markers; // 프리셋별 실제 Quad 참조

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        EnsureMarkersSized();      // 프리셋 개수와 markers 배열 동기화
        EnsureMarkersExist();      // Quad 오브젝트 생성
        ApplyAllPresetsToMarkers();// Preset → Marker 적용
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
        // ── Preset 고정 모드 ─────────────────────────────
        if (lockEveryFrame)
        {
            // Preset 값으로 마커 위치/회전/스케일을 매 프레임 덮어씀
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m != null && m.gameObject.activeSelf)
                    ApplyPresetToMarker(i);
            }
        }
        else
        {
            // 마커를 직접 조정할 경우, 그 값을 Preset에 저장
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (m != null && m.gameObject.activeSelf)
                    CaptureMarkerToPreset(i);
            }
        }

        // ── 숫자키 단축키로 토글 ─────────────────────────
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
                t.SetParent(GetTargetParent(), true);
                markers[i] = t;
                markers[i].gameObject.SetActive(false); // 기본은 숨김
            }
            else
            {
                // 부모 변경 시 다시 귀속
                var wantParent = GetTargetParent();
                if (markers[i].parent != wantParent)
                    markers[i].SetParent(wantParent, true);
            }
        }
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

        if (coordSpace == CoordSpace.World)
        {
            // 월드 좌표 기준으로 적용
            m.position = p.position;
            m.rotation = Quaternion.Euler(p.rotation);

            if (ignoreParentScale && m.parent != null)
            {
                // 부모 스케일 무시 → 월드 스케일 유지
                Vector3 parentLossy = m.parent.lossyScale;
                Vector3 safe = new Vector3(
                    parentLossy.x == 0 ? 1 : parentLossy.x,
                    parentLossy.y == 0 ? 1 : parentLossy.y,
                    parentLossy.z == 0 ? 1 : parentLossy.z
                );
                m.localScale = new Vector3(p.scale.x / safe.x, p.scale.y / safe.y, p.scale.z / safe.z);
            }
            else
            {
                m.localScale = p.scale;
            }
        }
        else // 로컬 좌표 기준
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

        if (coordSpace == CoordSpace.World)
        {
            presets[index].position = m.position;
            presets[index].rotation = m.rotation.eulerAngles;

            if (ignoreParentScale && m.parent != null)
            {
                // 부모 스케일이 있으면 월드 스케일로 변환
                Vector3 parentLossy = m.parent.lossyScale;
                Vector3 safe = new Vector3(
                    parentLossy.x == 0 ? 1 : parentLossy.x,
                    parentLossy.y == 0 ? 1 : parentLossy.y,
                    parentLossy.z == 0 ? 1 : parentLossy.z
                );
                Vector3 worldScale = new Vector3(
                    m.localScale.x * safe.x,
                    m.localScale.y * safe.y,
                    m.localScale.z * safe.z
                );
                presets[index].scale = worldScale;
            }
            else
            {
                presets[index].scale = m.localScale;
            }
        }
        else // 로컬 기준
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
}
