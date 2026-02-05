using UnityEngine;

namespace My.Scripts._02_PlayTutorial.Components
{
    /// <summary>
    /// 대상(카메라)과의 거리에 따라 스프라이트/메쉬의 투명도를 조절하는 클래스
    /// </summary>
    public class FrameDistanceFader : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("거리를 측정할 대상 (비워두면 메인 카메라 사용)")]
        public Transform targetTransform;

        [Header("Fading Settings")]
        [Tooltip("이 거리보다 가까우면 완전히 선명하게 보임 (Alpha 1)")]
        public float fullyVisibleDist = 10f; 

        [Tooltip("이 거리보다 멀어지면 완전히 투명해짐 (Alpha 0)")]
        public float invisibleDist = 30f; 

        private SpriteRenderer _spriteRenderer;
        private MeshRenderer _meshRenderer;
        private Color _originColor;

        private void Awake()
        {
            // 렌더러 컴포넌트 찾기 (Sprite 또는 Mesh 둘 다 대응)
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer)
            {
                _originColor = _spriteRenderer.color;
            }
            else
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer)
                {
                    // Material 인스턴스 생성을 막기 위해 sharedMaterial 사용 고려 가능하나,
                    // 색상을 개별적으로 바꾸려면 material에 접근해야 함.
                    _originColor = _meshRenderer.material.color;
                }
            }
        }

        private void Start()
        {
            // 타겟이 설정되지 않았다면 메인 카메라를 자동으로 찾음
            if (targetTransform == null && Camera.main != null)
            {
                targetTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (targetTransform == null) return;

            // 1. Z축 거리 계산 (절댓값)
            float distance = Mathf.Abs(transform.position.z - targetTransform.position.z);

            // 2. 거리 비율 계산 (InverseLerp: 범위 내 위치를 0~1로 반환)
            // 가까움(visibleDist) -> 1, 멂(invisibleDist) -> 0
            float alpha = Mathf.InverseLerp(invisibleDist, fullyVisibleDist, distance);

            // 3. 알파값 적용
            SetAlpha(alpha);
        }

        private void SetAlpha(float alpha)
        {
            // 스프라이트 렌더러인 경우
            if (_spriteRenderer != null)
            {
                Color c = _originColor;
                c.a = alpha;
                _spriteRenderer.color = c;
            }
            // 3D 메쉬 렌더러인 경우 (Quad 등)
            else if (_meshRenderer != null)
            {
                Color c = _originColor;
                c.a = alpha;
                // Standard Shader 등을 사용할 때 투명도 적용
                if (_meshRenderer.material.HasProperty("_Color"))
                    _meshRenderer.material.color = c;
                else if (_meshRenderer.material.HasProperty("_BaseColor"))
                    _meshRenderer.material.SetColor("_BaseColor", c);
            }
        }
    }
}