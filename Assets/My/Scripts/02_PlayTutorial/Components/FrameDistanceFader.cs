using UnityEngine;
using UnityEngine.UI; // ★ Text 컴포넌트 제어를 위해 추가

namespace My.Scripts._02_PlayTutorial.Components
{
    /// <summary>
    /// 대상(카메라)과의 거리에 따라 스프라이트/메쉬 및 자식 텍스트의 투명도를 조절하는 클래스
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

        // ★ 텍스트 페이딩을 위한 변수 추가
        private Text[] _childTexts; 
        private Color[] _originTextColors;

        private void Awake()
        {
            // 1. 렌더러 컴포넌트 찾기
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
                    _originColor = _meshRenderer.material.color;
                }
            }

            // ★ 2. 자식 오브젝트에 있는 모든 Text 컴포넌트를 찾아서 색상 저장
            _childTexts = GetComponentsInChildren<Text>();
            if (_childTexts != null && _childTexts.Length > 0)
            {
                _originTextColors = new Color[_childTexts.Length];
                for (int i = 0; i < _childTexts.Length; i++)
                {
                    // 각 텍스트의 원래 색상(RGB)을 기억해둠
                    _originTextColors[i] = _childTexts[i].color;
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

            // 2. 거리 비율 계산 (가까움 -> 1, 멂 -> 0)
            float alpha = Mathf.InverseLerp(invisibleDist, fullyVisibleDist, distance);

            // 3. 알파값 일괄 적용
            SetAlpha(alpha);
        }

        private void SetAlpha(float alpha)
        {
            // (1) 프레임 본체(Sprite/Mesh) 투명도 조절
            if (_spriteRenderer != null)
            {
                Color c = _originColor;
                c.a = alpha;
                _spriteRenderer.color = c;
            }
            else if (_meshRenderer != null)
            {
                Color c = _originColor;
                c.a = alpha;
                if (_meshRenderer.material.HasProperty("_Color"))
                    _meshRenderer.material.color = c;
                else if (_meshRenderer.material.HasProperty("_BaseColor"))
                    _meshRenderer.material.SetColor("_BaseColor", c);
            }

            // ★ (2) 자식 텍스트들의 투명도 조절
            if (_childTexts != null)
            {
                for (int i = 0; i < _childTexts.Length; i++)
                {
                    if (_childTexts[i] != null)
                    {
                        Color c = _originTextColors[i]; // 원래 색상 가져오기
                        c.a = alpha;                    // 투명도만 덮어쓰기
                        _childTexts[i].color = c;       // 적용
                    }
                }
            }
        }
    }
}