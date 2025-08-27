using UnityEngine;

public class Controller : MonoBehaviour
{
    [SerializeField] private Transform cube; 
    [SerializeField] private Transform cylinder1;
    [SerializeField] private Transform cylinder2;

    // 코드 내에서 직접 조절할 변수
    [Header("Shape Parameters")]
    public double cubeWidth = 1.0;
    public double cubeHeight = 1.0;
    public double cylinderRadius = 0.5;
    public double cylinderHeight = 2.0;

    private void Start()
    {
        // 시작 시 현재 오브젝트 스케일을 변수로 초기화
        cubeWidth = cube.localScale.x;
        cubeHeight = cube.localScale.y;
        cylinderRadius = cylinder1.localScale.x / 2f;
        cylinderHeight = cylinder1.localScale.y * 2f;
    }

    private void Update()
    {
        // Cube 적용
        Vector3 cubeScale = cube.localScale;
        cubeScale.x = (float)cubeWidth;
        cubeScale.y = (float)cubeHeight;
        cube.localScale = cubeScale;

        // Cylinder 두 개 동시에 적용
        Vector3 s1 = cylinder1.localScale;
        Vector3 s2 = cylinder2.localScale;

        s1.x = (float)(cylinderRadius * 2f);
        s1.z = (float)(cylinderRadius * 2f);
        s1.y = (float)(cylinderHeight / 2f); // 유니티 Cylinder는 y=반높이

        s2.x = (float)(cylinderRadius * 2f);
        s2.z = (float)(cylinderRadius * 2f);
        s2.y = (float)(cylinderHeight / 2f);

        cylinder1.localScale = s1;
        cylinder2.localScale = s2;
    }
}
