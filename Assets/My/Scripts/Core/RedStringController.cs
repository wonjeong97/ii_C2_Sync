using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;

namespace My.Scripts.UI
{
    /// <summary>
    /// 캐릭터의 애니메이션 움직임에 맞춰 마주보는 손을 추적하고 붉은 실 UI를 그리는 컨트롤러.
    /// </summary>
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
        [Tooltip("가장 가까운 거리(Index 0)일 때 적용할 Y축 내림 수치")]
        [SerializeField] private float nearYOffset = 30f;

        [Header("Distance Sprites")]
        [SerializeField] private Sprite[] normalSprites; // 200, 400, 800, 1000, 1400
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
            // 1. 플레이어 위치 관계 확인
            Vector2 p1Pos = player1.CharacterRect.anchoredPosition;
            Vector2 p2Pos = player2.CharacterRect.anchoredPosition;
            bool p1IsLeft = p1Pos.x <= p2Pos.x;

            // 2. 캐릭터 거리 계산 및 단계(Index) 판정
            float playerDist = Vector2.Distance(p1Pos, p2Pos);
            int idx = GetThresholdIndex(playerDist);

            // 3. 연결할 손의 월드 좌표 획득
            Vector3 w1 = player1.GetHandWorldPosition(p1IsLeft); 
            Vector3 w2 = player2.GetHandWorldPosition(!p1IsLeft);

            // 4. 월드 좌표를 캔버스 로컬 좌표로 변환
            Vector2 start = WorldToCanvas(w1);
            Vector2 end = WorldToCanvas(w2);

            // 5. 상태에 따른 두께(Height) 및 너비(Width) 보정 로직
            bool isStunned = player1.IsStunned || player2.IsStunned;
            float targetHeight = (idx == 0) ? thicknessNear : thicknessFar;
            
            // 피격 상태(Stun)일 때 거리 단계별 고정 높이 적용
            if (isStunned)
            {
                if (idx == 3) targetHeight = 44f;      // hit1000 대응 Height
                else if (idx == 4) targetHeight = 70f; // hit1400 대응 Height
            }

            _currentThickness = (isStunned || _wasHit) 
                ? targetHeight 
                : Mathf.Lerp(_currentThickness, targetHeight, Time.deltaTime * thicknessLerpSpeed);
            _wasHit = isStunned;

            // 6. 트랜스폼 적용 (위치, 크기, 회전)
            Vector2 diff = end - start;
            float finalWidth = diff.magnitude;

            // ★ [수정] 가장 가까운 거리(Index 0)일 때 최소 너비 45 보장 및 Y 위치 하향 조정
            if (idx == 0)
            {
                finalWidth = Mathf.Max(finalWidth, 45f);
                start.y -= nearYOffset; // 실의 시작점 높이를 조금 내림
            }

            _rectTransform.anchoredPosition = start;
            _rectTransform.sizeDelta = new Vector2(finalWidth, _currentThickness);
            _rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
            _rectTransform.localScale = p1IsLeft ? Vector3.one : new Vector3(1, -1, 1);

            // 7. 스프라이트 업데이트
            UpdateSprite(idx, isStunned);
        }

        private Vector2 WorldToCanvas(Vector3 worldPos)
        {
            Camera cam = (_parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : Camera.main;
            Vector2 screenPos = (cam == null) ? (Vector2)worldPos : cam.WorldToScreenPoint(worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform.parent as RectTransform, screenPos, cam, out Vector2 local);
            return local;
        }

        private void UpdateSprite(int idx, bool isHit)
        {
            Sprite s = (normalSprites != null && idx < normalSprites.Length) ? normalSprites[idx] : null;
            
            if (isHit)
            {
                if (idx == 3 && hit1000) s = hit1000;
                else if (idx == 4 && hit1400) s = hit1400;
            }

            if (s && _image.sprite != s) _image.sprite = s;
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