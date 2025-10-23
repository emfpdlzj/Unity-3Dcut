# Unity MicroElbow Cut-Plane Demo

microElbow(타깃) 주변에서 **절단면(CutPlane)** 을 프리셋으로 관리하고, **카메라 조작(좌클릭 이동 / 우클릭 회전 / 휠 줌)** 을 깔끔하게 제공하는 예제 프로젝트.

---

## 주요 기능

- **SectionPlaneMarker**
  - microElbow(타깃) 기준으로 **위치/회전/크기**가 상대적으로 따라옴
  - 타깃 스케일 변경 시 절단면 크기도 **비례(scaleWithTarget)**  
  - 회전 모드:
    - `CopyTarget` — 타깃과 **완전히 동일한 회전**
    - `TargetPlusPreset` — 타깃 회전에 **프리셋 오프셋**을 더함
    - `AlignTargetAxis` — 타깃의 특정 축(Forward/Up/Right)을 **절단면 법선**(+Z)에 정렬
  - 프리셋 다중 관리(표시/숨김 토글)
  - 부모 스케일 무시 옵션(`ignoreParentScale`)로 **보이는 크기** 유지

- **SimpleMouseCamera**
  - **LMB = Pan**, **RMB = Rotate**, **Wheel = Zoom**
  - 시작 시 타깃 바운딩에 맞춰 **자동 프레이밍**(옵션)
  - **카메라만 스무스 보간** → 통통 튀는 느낌 제거

---

## 요구 사항
- **URP 기준 설명**(Unlit Transparent로 반투명 처리).  
  HDRP/빌트인 파이프라인에서도 동등 셰이더로 대체 가능.

[작동사진](./image/image01.jpeg)

[작동사진](./image/image02.jpeg)

---

## 빠른 시작 (Quick Start)

1. **머티리얼 생성**  
   - `Assets/Materials/RedCut` (권장)  
   - Shader: **Universal Render Pipeline/Unlit**  
   - Surface Type: **Transparent**  
   - **Alpha Clipping 해제**  
   - `Base Map` 색상 **알파(A)** 로 투명도 조절(예: RGBA(1,0,0,0.5))

2. **절단면 매니저 배치**  
   - 빈 오브젝트에 `SectionPlaneMarker` 추가  
   - `redUnlitMaterial` ← 위 `RedCut` 지정  
   - `followTarget` ← **microElbow(루트 Transform)** 드래그  
   - 체크: `relativeToTarget = ON`, `scaleWithTarget = ON`, `lockEveryFrame = ON`,  
     `ignoreParentScale = ON`  
   - 회전 모드 필요에 따라 선택:
     - **같은 회전** → `CopyTarget`
     - **오프셋 유지** → `TargetPlusPreset`
     - **특정 축 정렬** → `AlignTargetAxis` (+ 축 선택)
   - `startActiveIndices`에 처음 켤 프리셋 인덱스(e.g. `0`) 입력

3. **카메라 컨트롤러 세팅**  
   - **Main Camera** 에 `SimpleMouseCamera` 추가  
   - `target` ← **microElbow(루트 Transform)**  
   - `frameTargetOnStart = ON` (권장)

4. **실행 / 조작**
   - **좌클릭 드래그**: 이동(Pan)  
   - **우클릭 드래그**: 회전(Orbit)  
   - **마우스 휠**: 줌(지수 스케일)  
   - **절단면 토글**: 숫자 **1~6** (켜기/끄기), **0** (전부 숨김)

---

