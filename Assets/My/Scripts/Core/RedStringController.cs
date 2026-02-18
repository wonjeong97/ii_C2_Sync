using UnityEngine;
using UnityEngine.UI;
using My.Scripts.Core;

namespace My.Scripts.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class RedStringController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private PlayerController player1;
        [SerializeField] private PlayerController player2;

        [Header("Hand X Offsets")]
        [SerializeField] private float leftHandOffsetX = -80f;
        [SerializeField] private float rightHandOffsetX = 80f;

        [Header("Hand Y Settings (Dynamic)")]
        [SerializeField] private float handOffsetY_Near = -60f;
        [SerializeField] private float handOffsetY_Far = -40f;

        [Header("Distance Sprites (Normal)")]
        [SerializeField] private Sprite spriteDist200;   // Index 0
        [SerializeField] private Sprite spriteDist400;   // Index 1
        [SerializeField] private Sprite spriteDist800;   // Index 2
        [SerializeField] private Sprite spriteDist1000;  // Index 3
        [SerializeField] private Sprite spriteDist1400;  // Index 4

        [Header("Hit Sprites (Stunned)")]
        [Tooltip("1000m 구간(Index 3)에서 스턴 시 보여줄 스프라이트")]
        [SerializeField] private Sprite hitSpriteDist1000; 
        [Tooltip("1400m 구간(Index 4)에서 스턴 시 보여줄 스프라이트")]
        [SerializeField] private Sprite hitSpriteDist1400;

        [Header("Thickness Settings")]
        [Tooltip("가장 가까운 거리(200)일 때의 선 두께")]
        [SerializeField] private float thicknessNear = 25f; 
        [Tooltip("나머지 거리(400~)일 때의 선 두께")]
        [SerializeField] private float thicknessFar = 13f;

        // ★ [추가] Hit 상태일 때의 두께 설정
        [Tooltip("Hit4 (1000m) 상태일 때의 선 두께 (즉시 변경)")]
        [SerializeField] private float thicknessHit1000 = 44f;
        [Tooltip("Hit5 (1400m) 상태일 때의 선 두께 (즉시 변경)")]
        [SerializeField] private float thicknessHit1400 = 70f;
        
        [Tooltip("두께가 변하는 속도 (일반 상태일 때만 적용)")]
        [SerializeField] private float thicknessLerpSpeed = 10f;

        private RectTransform _rectTransform;
        private Image _image;

        private readonly float[] _thresholds = { 200f, 400f, 800f, 1000f, 1400f };
        private Sprite[] _sprites;
        
        private float _currentThickness;
        private bool _wasHitState = false; // 이전 프레임에 Hit 상태였는지 체크

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
        }

        private void Start()
        {
            _sprites = new Sprite[] { 
                spriteDist200, 
                spriteDist400, 
                spriteDist800, 
                spriteDist1000, 
                spriteDist1400 
            };
            
            _rectTransform.pivot = new Vector2(0f, 0.5f);
            _currentThickness = thicknessFar;
        }

        private void LateUpdate()
        {
            if (!player1 || !player2) return;
            if (!player1.CharacterRect || !player2.CharacterRect) return;

            UpdateLineTransform();
        }

        private void UpdateLineTransform()
        {
            // 1. 캐릭터 중심 좌표 및 거리 계산
            Vector2 p1Center = player1.CharacterRect.anchoredPosition;
            Vector2 p2Center = player2.CharacterRect.anchoredPosition;

            float centerDistX = Mathf.Abs(p2Center.x - p1Center.x);
            float centerDistY = Mathf.Abs(p2Center.y - p1Center.y);
            float centerDistance = Mathf.Sqrt(centerDistX * centerDistX + centerDistY * centerDistY);

            // 2. 거리 단계(Index) 확인
            int closestIndex = GetClosestDistanceIndex(centerDistance);

            // 3. 기본 타겟 설정 (Normal 상태)
            float targetY = (closestIndex == 0) ? handOffsetY_Near : handOffsetY_Far;
            float targetThickness = (closestIndex == 0) ? thicknessNear : thicknessFar;
            
            Sprite targetSprite = null;
            if (_sprites != null && closestIndex < _sprites.Length)
            {
                targetSprite = _sprites[closestIndex];
            }

            // 4. Hit(Stun) 상태 체크 및 오버라이드
            bool isStunned = player1.IsStunned || player2.IsStunned;
            bool isHitState = false; // 현재 프레임이 Hit 상태인지

            if (isStunned)
            {
                // Hit4 (1000m)
                if (closestIndex == 3 && hitSpriteDist1000 != null)
                {
                    targetSprite = hitSpriteDist1000;
                    targetThickness = thicknessHit1000; // 44
                    isHitState = true;
                }
                // Hit5 (1400m)
                else if (closestIndex == 4 && hitSpriteDist1400 != null)
                {
                    targetSprite = hitSpriteDist1400;
                    targetThickness = thicknessHit1400; // 70
                    isHitState = true;
                }
            }

            // 5. ★ [수정] 두께 적용 로직 (즉시 vs Lerp)
            // 현재 Hit 상태이거나, 방금 Hit 상태에서 돌아온 경우 -> 즉시 변경
            if (isHitState || _wasHitState)
            {
                _currentThickness = targetThickness;
            }
            else
            {
                // 일반적인 거리 변화 -> Lerp
                _currentThickness = Mathf.Lerp(_currentThickness, targetThickness, Time.deltaTime * thicknessLerpSpeed);
            }

            // 상태 저장
            _wasHitState = isHitState;


            // --- 드로잉 처리 ---

            // 6. 손 위치 결정 및 Flip
            float p1HandX, p2HandX;
            if (p1Center.x < p2Center.x)
            {
                // Player 1이 왼쪽에 있을 때
                p1HandX = rightHandOffsetX;
                p2HandX = leftHandOffsetX;
                _rectTransform.localScale = Vector3.one; 
            }
            else if (p1Center.x > p2Center.x)
            {
                // Player 1이 오른쪽에 있을 때
                p1HandX = leftHandOffsetX;
                p2HandX = rightHandOffsetX;
                _rectTransform.localScale = new Vector3(1f, -1f, 1f); 
            }
            else
            {
                // 두 플레이어의 X가 겹치는 경우
                // 실제 게임에서 가능성은 낮기만 예외 처리 함
                p1HandX = rightHandOffsetX;
                p2HandX = leftHandOffsetX;
                _rectTransform.localScale = Vector3.one; 
            }

            // 7. 최종 좌표 및 길이 계산
            Vector2 startPos = new Vector2(p1Center.x + p1HandX, p1Center.y + targetY);
            Vector2 endPos = new Vector2(p2Center.x + p2HandX, p2Center.y + targetY);
            Vector2 diff = endPos - startPos;
            float visualDistance = diff.magnitude;

            // 8. 트랜스폼 적용
            _rectTransform.anchoredPosition = startPos;
            _rectTransform.sizeDelta = new Vector2(visualDistance, _currentThickness);
            
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // 9. 스프라이트 교체
            if (targetSprite != null && _image.sprite != targetSprite)
            {
                _image.sprite = targetSprite;
            }
        }

        private int GetClosestDistanceIndex(float currentDistance)
        {
            int closestIndex = 0;
            float minDiff = float.MaxValue;

            for (int i = 0; i < _thresholds.Length; i++)
            {
                float diff = Mathf.Abs(currentDistance - _thresholds[i]);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestIndex = i;
                }
            }
            return closestIndex;
        }
    }
}