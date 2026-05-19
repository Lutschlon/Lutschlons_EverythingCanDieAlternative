using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace EverythingCanDieAlternative.UI
{
    public class EnemyHealthBarUI : MonoBehaviour
    {
        public EnemyAI Enemy;

        private Canvas _canvas;
        private GameObject _barRoot;
        private Image _bgImage;
        private Image _fillImage;
        private RectTransform _fillRT;
        private TextMeshProUGUI _text;
        private float _offsetY = 2.5f;
        private UIConfiguration.HealthBarDisplayMode _appliedMode = (UIConfiguration.HealthBarDisplayMode)(-1);

        private const float BAR_WIDTH = 180f;
        private const float BAR_HEIGHT = 22f;
        private const float BAR_PADDING = 2f;
        private const float FILL_MAX_WIDTH = BAR_WIDTH - BAR_PADDING * 2f;
        private const float CANVAS_SCALE = 0.0035f;
        // Visible-range squared distances per HealthBarVisibilityDistance setting.
        private const float RANGE_CLOSE_SQR = 7f * 7f;
        private const float RANGE_MEDIUM_SQR = 13f * 13f;
        private const float RANGE_FAR_SQR = 20f * 20f;
        // World-space distance the bar is nudged toward the camera (XZ only) so it sits
        // closer to the player rather than dead-centered on top of the enemy.
        private const float TOWARD_PLAYER_OFFSET = 0.8f;

        private static TMP_FontAsset _cachedFont;

        private static float GetMaxVisibleDistanceSqr()
        {
            var range = UIConfiguration.HealthBarRange != null
                ? UIConfiguration.HealthBarRange.Value
                : UIConfiguration.HealthBarVisibilityDistance.Close;
            switch (range)
            {
                case UIConfiguration.HealthBarVisibilityDistance.Medium: return RANGE_MEDIUM_SQR;
                case UIConfiguration.HealthBarVisibilityDistance.Far: return RANGE_FAR_SQR;
                default: return RANGE_CLOSE_SQR;
            }
        }

        private static float GetSizeMultiplier()
        {
            var size = UIConfiguration.HealthBarSizeOption != null
                ? UIConfiguration.HealthBarSizeOption.Value
                : UIConfiguration.HealthBarSize.Medium;
            switch (size)
            {
                // Bumped one step up: previous Small/Medium/Large were 1.0/1.5/2.0.
                case UIConfiguration.HealthBarSize.Small: return 1.5f;
                case UIConfiguration.HealthBarSize.Large: return 2.5f;
                default: return 2f;
            }
        }

        public static void Attach(EnemyAI enemy)
        {
            if (enemy == null) return;
            if (enemy.GetComponentInChildren<EnemyHealthBarUI>(true) != null) return;

            var go = new GameObject("ECDA_HealthBar", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(enemy.transform, false);
            var ui = go.AddComponent<EnemyHealthBarUI>();
            ui.Enemy = enemy;
            ui.Build();
        }

        private void Build()
        {
            var nma = Enemy.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>();
            if (nma != null) _offsetY = nma.height + 0.5f;

            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(200f, 60f);
            rt.localScale = Vector3.one * CANVAS_SCALE;
            rt.localPosition = new Vector3(0f, _offsetY, 0f);

            _barRoot = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var barRT = (RectTransform)_barRoot.transform;
            barRT.SetParent(rt, false);
            barRT.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
            barRT.anchoredPosition = new Vector2(0f, -12f);
            _bgImage = _barRoot.GetComponent<Image>();
            _bgImage.color = new Color(0f, 0f, 0f, 0.75f);

            // Fill: anchored to the left edge of the bar, width is set per-frame from HP percentage.
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            _fillRT = (RectTransform)fillGO.transform;
            _fillRT.SetParent(barRT, false);
            _fillRT.anchorMin = new Vector2(0f, 0.5f);
            _fillRT.anchorMax = new Vector2(0f, 0.5f);
            _fillRT.pivot = new Vector2(0f, 0.5f);
            _fillRT.anchoredPosition = new Vector2(BAR_PADDING, 0f);
            _fillRT.sizeDelta = new Vector2(FILL_MAX_WIDTH, BAR_HEIGHT - BAR_PADDING * 2f);
            _fillImage = fillGO.GetComponent<Image>();
            _fillImage.color = new Color(0.2f, 1f, 0.2f, 0.95f);

            var textGO = new GameObject("Text", typeof(RectTransform));
            var textRT = (RectTransform)textGO.transform;
            textRT.SetParent(rt, false);
            textRT.sizeDelta = new Vector2(220f, 28f);
            textRT.anchoredPosition = new Vector2(0f, 18f);
            _text = textGO.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = 18f;
            _text.fontStyle = FontStyles.Bold;
            _text.color = Color.white;
            _text.text = string.Empty;
            _text.enableWordWrapping = false;
            _text.outlineWidth = 0.2f;
            _text.outlineColor = Color.black;

            var font = ResolveFont();
            if (font != null) _text.font = font;
        }

        private static TMP_FontAsset ResolveFont()
        {
            if (_cachedFont != null) return _cachedFont;

            if (TMP_Settings.defaultFontAsset != null)
            {
                _cachedFont = TMP_Settings.defaultFontAsset;
                return _cachedFont;
            }

            var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (all != null && all.Length > 0)
            {
                _cachedFont = all[0];
                return _cachedFont;
            }

            return null;
        }

        private void LateUpdate()
        {
            if (Enemy == null)
            {
                if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
                return;
            }

            var mode = UIConfiguration.HealthBarMode != null
                ? UIConfiguration.HealthBarMode.Value
                : UIConfiguration.HealthBarDisplayMode.Off;

            if (mode == UIConfiguration.HealthBarDisplayMode.Off || Enemy.isEnemyDead)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            if (!HealthManager.IsEnemyTracked(Enemy))
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            float maxHP = HealthManager.GetEnemyMaxHealth(Enemy);
            float curHP = HealthManager.GetEnemyHealth(Enemy);
            if (maxHP <= 0f)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            // Optionally hide for undamaged enemies so they don't reveal themselves while hiding.
            bool hideAtFull = UIConfiguration.HideHealthBarForFullHpEnemies != null
                ? UIConfiguration.HideHealthBarForFullHpEnemies.Value
                : true;
            if (hideAtFull && curHP >= maxHP)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            Camera cam = null;
            var sor = StartOfRound.Instance;
            if (sor != null && sor.localPlayerController != null)
            {
                cam = sor.localPlayerController.isPlayerDead ? sor.spectateCamera : sor.localPlayerController.gameplayCamera;
            }
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            float dSqr = (cam.transform.position - transform.position).sqrMagnitude;
            if (dSqr > GetMaxVisibleDistanceSqr())
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            if (!_canvas.enabled) _canvas.enabled = true;

            if (mode != _appliedMode)
            {
                _appliedMode = mode;
                _barRoot.SetActive(mode == UIConfiguration.HealthBarDisplayMode.BarOnly || mode == UIConfiguration.HealthBarDisplayMode.Both);
                _text.gameObject.SetActive(mode == UIConfiguration.HealthBarDisplayMode.NumberOnly || mode == UIConfiguration.HealthBarDisplayMode.Both);
            }

            // Anchor above the enemy and nudge horizontally toward the camera so the bar
            // floats between the enemy and the player rather than directly over its head.
            Vector3 anchor = Enemy.transform.position + Vector3.up * _offsetY;
            Vector3 toCam = cam.transform.position - Enemy.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                anchor += toCam.normalized * TOWARD_PLAYER_OFFSET;
            }
            transform.position = anchor;
            transform.localScale = Vector3.one * (CANVAS_SCALE * GetSizeMultiplier());

            Vector3 lookDir = transform.position - cam.transform.position;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDir, cam.transform.up);
            }

            float pct = Mathf.Clamp01(curHP / maxHP);
            if (_fillRT != null)
            {
                var sd = _fillRT.sizeDelta;
                sd.x = FILL_MAX_WIDTH * pct;
                _fillRT.sizeDelta = sd;
            }
            if (_fillImage != null)
            {
                _fillImage.color = Color.Lerp(new Color(1f, 0.15f, 0.15f, 0.95f), new Color(0.2f, 1f, 0.2f, 0.95f), pct);
            }
            if (_text != null && _text.gameObject.activeSelf)
            {
                _text.text = $"{Mathf.CeilToInt(curHP)} / {Mathf.CeilToInt(maxHP)}";
            }
        }
    }
}
