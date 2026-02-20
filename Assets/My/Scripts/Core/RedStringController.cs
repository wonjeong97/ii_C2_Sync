using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;

namespace My.Scripts.UI
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class RedStringController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private PlayerController player1;
        [SerializeField] private PlayerController player2;

        [Header("Visual Settings")]
        [SerializeField] private float thicknessNear = 25f; 
        [SerializeField] private float thicknessFar = 13f;
        [SerializeField] private float thicknessLerpSpeed = 10f;
        [SerializeField] private float nearYOffset = 30f;   
        
        [Header("Hit & Near Offsets")]
        [SerializeField] private float hitHeightNear = 44f;
        [SerializeField] private float hitHeightFar = 70f;
        [SerializeField] private float minWidthForIdx0 = 45f;

        [Header("Distance Sprites")]
        [SerializeField] private Sprite[] normalSprites; 
        [SerializeField] private Sprite hit1000;
        [SerializeField] private Sprite hit1400;

        private RectTransform _rectTransform;
        private Image _image;
        private Canvas _parentCanvas;
        private readonly float[] _thresholds = { 200f, 400f, 800f, 1000f, 1400f };
        private float _currentThickness;
        private bool _wasHit;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        private void Start()
        {
            _rectTransform.pivot = new Vector2(0f, 0.5f); 
            _currentThickness = thicknessFar;
        }

        private void LateUpdate()
        {
            if (!player1 || !player2 || !_parentCanvas) return;
            UpdateLine();
        }

        private void UpdateLine()
        {
            Vector2 p1Pos = player1.CharacterRect.anchoredPosition;
            Vector2 p2Pos = player2.CharacterRect.anchoredPosition;
            bool p1IsLeft = p1Pos.x <= p2Pos.x;

            float playerDist = Vector2.Distance(p1Pos, p2Pos);
            int idx = GetThresholdIndex(playerDist);

            Vector3 w1 = player1.GetHandWorldPosition(p1IsLeft); 
            Vector3 w2 = player2.GetHandWorldPosition(!p1IsLeft);

            Vector2 start = WorldToCanvas(w1);
            Vector2 end = WorldToCanvas(w2);

            bool isStunned = player1.IsStunned || player2.IsStunned;
            float targetHeight = (idx == 0) ? thicknessNear : thicknessFar;
            
            if (isStunned)
            {
                if (idx == 3) targetHeight = hitHeightNear;      
                else if (idx == 4) targetHeight = hitHeightFar; 
            }

            _currentThickness = (isStunned || _wasHit) 
                ? targetHeight 
                : Mathf.Lerp(_currentThickness, targetHeight, Time.deltaTime * thicknessLerpSpeed);
            _wasHit = isStunned;

            if (idx == 0)
            {
                start.y -= nearYOffset; 
            }

            Vector2 diff = end - start;
            float finalWidth = diff.magnitude;

            if (idx == 0)
            {
                finalWidth = Mathf.Max(finalWidth, minWidthForIdx0);
            }

            _rectTransform.anchoredPosition = start;
            _rectTransform.sizeDelta = new Vector2(finalWidth, _currentThickness);
            _rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
            _rectTransform.localScale = p1IsLeft ? Vector3.one : new Vector3(1, -1, 1);

            UpdateSprite(idx, isStunned);
        }

        private Vector2 WorldToCanvas(Vector3 worldPos)
        {
            Camera refCam = null;
            if (_parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || _parentCanvas.renderMode == RenderMode.WorldSpace)
            {
                refCam = _parentCanvas.worldCamera;
            }
            if (!refCam)
            {
                refCam = Camera.main ? Camera.main : Camera.current;
            }

            if (!refCam)
            {
                Debug.LogWarning("[RedStringController] WorldToCanvas: 기준 카메라를 찾을 수 없습니다.");
                return Vector2.zero;
            }

            Vector2 screenPos = refCam.WorldToScreenPoint(worldPos);
            Camera eventCam = (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : refCam;

            RectTransform parentRect = _rectTransform.parent as RectTransform;
            if (!parentRect)
            {
                Debug.LogWarning("[RedStringController] WorldToCanvas: _rectTransform.parent가 없거나 RectTransform이 아닙니다.");
                return Vector2.zero;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, eventCam, out Vector2 local);
    
            return local;
        }

        private void UpdateSprite(int idx, bool isHit)
        {
            if (normalSprites == null || normalSprites.Length == 0)
            {
                Debug.LogWarning("[RedStringController] normalSprites 배열이 비어있습니다.");
                _image.sprite = null;
                return;
            }

            int clampedIdx = Mathf.Clamp(idx, 0, normalSprites.Length - 1);
            if (idx >= normalSprites.Length)
            {
                Debug.LogWarning($"[RedStringController] idx({idx})가 normalSprites 길이를 초과하여 {clampedIdx}로 클램핑되었습니다.");
            }

            Sprite s = normalSprites[clampedIdx];
            
            if (isHit)
            {
                if (idx == 3 && hit1000) s = hit1000;
                else if (idx == 4 && hit1400) s = hit1400;
            }

            if (!s)
            {
                _image.sprite = null;
            }
            else if (_image.sprite != s)
            {
                _image.sprite = s;
            }
        }

        private int GetThresholdIndex(float d)
        {
            int best = 0; float min = float.MaxValue;
            for (int i = 0; i < _thresholds.Length; i++)
            {
                float diff = Mathf.Abs(d - _thresholds[i]);
                if (diff < min) { min = diff; best = i; }
            }
            return best;
        }
    }
}