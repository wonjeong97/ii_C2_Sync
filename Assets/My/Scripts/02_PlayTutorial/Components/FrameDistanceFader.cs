using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace My.Scripts._02_PlayTutorial.Components
{
    /// <summary>
    /// 대상(카메라)과의 거리에 따라 스프라이트/메쉬 및 자식 텍스트의 투명도를 조절하는 클래스
    /// </summary>
    public class FrameDistanceFader : MonoBehaviour
    {
        private readonly static int ColorPropertyId = Shader.PropertyToID("_Color");
        private readonly static int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

        [Header("Target Settings")]
        public Transform targetTransform;

        [Header("Fading Settings")]
        public float fullyVisibleDist = 20f; 
        public float invisibleDist = 30f; 

        private SpriteRenderer _spriteRenderer;
        private MeshRenderer _meshRenderer;
        private Color _originColor;

        private readonly List<Text> _childTexts = new List<Text>(); 
        private readonly List<Color> _originTextColors = new List<Color>();

        private ILogger<FrameDistanceFader> _logger;

        [Inject]
        public void Construct(ILogger<FrameDistanceFader> logger)
        {
            _logger = logger;
        }

        private void Awake()
        {
            InitializeRenderers();
            InitializeChildTexts();
        }

        private void InitializeRenderers()
        {
            if (TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                _spriteRenderer = spriteRenderer;
                _originColor = _spriteRenderer.color;
            }
            else if (TryGetComponent(out MeshRenderer meshRenderer))
            {
                _meshRenderer = meshRenderer;
                // Material 인스턴스 생성을 막기 위해 sharedMaterial 사용 권장 (프로젝트 환경에 따라 조정)
                _originColor = _meshRenderer.sharedMaterial.color;
            }
        }

        private void InitializeChildTexts()
        {
            GetComponentsInChildren(true, _childTexts);
            foreach (Text txt in _childTexts)
            {
                _originTextColors.Add(txt.color);
            }
        }

        private void Start()
        {
            if (!targetTransform && Camera.main)
            {
                targetTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (!targetTransform) return;

            float distance = Mathf.Abs(transform.position.z - targetTransform.position.z);
            float alpha = Mathf.InverseLerp(invisibleDist, fullyVisibleDist, distance);

            SetAlpha(alpha);
        }

        private void SetAlpha(float alpha)
        {
            if (_spriteRenderer)
            {
                Color c = _originColor;
                c.a = alpha;
                _spriteRenderer.color = c;
            }
            else if (_meshRenderer)
            {
                ApplyMeshMaterialAlpha(_meshRenderer, alpha);
            }

            for (int i = 0; i < _childTexts.Count; i++)
            {
                if (_childTexts[i])
                {
                    Color c = _originTextColors[i];
                    c.a = alpha;
                    _childTexts[i].color = c;
                }
            }
        }

        private void ApplyMeshMaterialAlpha(MeshRenderer meshRenderer, float alpha)
        {
            // 공유 머티리얼이 아닌 인스턴스 제어가 필요할 경우 material 사용
            Material mat = meshRenderer.material; 
            Color c = _originColor;
            c.a = alpha;

            if (mat.HasProperty(ColorPropertyId))
            {
                mat.color = c;
            }
            else if (mat.HasProperty(BaseColorPropertyId))
            {
                mat.SetColor(BaseColorPropertyId, c);
            }
        }
        
        public void ForceUpdateAlpha()
        {
            if (!targetTransform)
            {
                if (Camera.main) targetTransform = Camera.main.transform;
                else return;
            }
            
            float distance = Mathf.Abs(transform.position.z - targetTransform.position.z);
            float alpha = Mathf.InverseLerp(invisibleDist, fullyVisibleDist, distance);
            SetAlpha(alpha);
        }
    }
}