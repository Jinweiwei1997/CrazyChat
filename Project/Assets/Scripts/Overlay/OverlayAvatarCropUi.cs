using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Square crop UI: pan/zoom source image, confirm writes cropped PNG bytes.
    /// </summary>
    public sealed class OverlayAvatarCropUi : MonoBehaviour, IDragHandler, IScrollHandler
    {
        const float MinZoom = 1f;
        const float MaxZoom = 4f;

        RectTransform _imageRt;
        RawImage _image;
        Text _hint;
        Texture2D _source;
        float _zoom = 1f;
        float _uiScale = 1f;
        Vector2 _pan;
        Action<byte[]> _onConfirm;
        Action _onCancel;
        float _cropPx = 240f;

        public static OverlayAvatarCropUi Create(Transform modal)
        {
            var go = new GameObject("AvatarCropUi", typeof(RectTransform));
            go.transform.SetParent(modal, false);
            Stretch((RectTransform)go.transform);
            var ui = go.AddComponent<OverlayAvatarCropUi>();
            ui.Build();
            go.SetActive(false);
            return ui;
        }

        public void Open(byte[] imageBytes, Action<byte[]> onConfirm, Action onCancel = null)
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            if (_source != null)
            {
                Destroy(_source);
                _source = null;
            }

            _source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!_source.LoadImage(imageBytes))
            {
                Destroy(_source);
                _source = null;
                Debug.LogWarning("[Overlay] 无法读取选中的图片。");
                _onCancel?.Invoke();
                return;
            }

            _source.wrapMode = TextureWrapMode.Clamp;
            _zoom = 1f;
            _pan = Vector2.zero;
            _uiScale = 1f;
            _image.texture = _source;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ApplyLayout();
        }

        void Build()
        {
            var dim = CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.55f), OverlaySprites.RoundedRect);
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);

            var card = CreateImage("Card", transform, OverlaySprites.Panel, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyPanel(card);
            card.raycastTarget = true;
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(360f, 420f);

            PlaceLabel(cardRt, "截取方形头像", 16, OverlaySkin.Text, new Vector2(0f, 180f), new Vector2(280f, 24f));
            _hint = PlaceLabel(cardRt, "拖动移动 · 滚轮缩放画面 · +/- 放大窗口", 12, OverlaySkin.TextMuted,
                new Vector2(0f, 152f), new Vector2(320f, 20f));

            var stage = new GameObject("Stage", typeof(RectTransform));
            stage.transform.SetParent(cardRt, false);
            var stageRt = (RectTransform)stage.transform;
            stageRt.anchorMin = stageRt.anchorMax = new Vector2(0.5f, 0.5f);
            stageRt.anchoredPosition = new Vector2(0f, 10f);
            stageRt.sizeDelta = new Vector2(280f, 280f);

            var frame = CreateImage("Frame", stageRt, new Color(0.1f, 0.1f, 0.12f, 1f), OverlaySprites.RoundedSquare);
            frame.raycastTarget = true;
            var frameRt = frame.rectTransform;
            Stretch(frameRt);

            var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(frameRt, false);
            Stretch((RectTransform)maskGo.transform);
            var maskImg = maskGo.GetComponent<Image>();
            maskImg.sprite = OverlaySprites.RoundedSquare;
            maskImg.raycastTarget = true;
            maskGo.GetComponent<Mask>().showMaskGraphic = false;

            var imgGo = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imgGo.transform.SetParent(maskGo.transform, false);
            _imageRt = (RectTransform)imgGo.transform;
            _imageRt.anchorMin = _imageRt.anchorMax = new Vector2(0.5f, 0.5f);
            _imageRt.pivot = new Vector2(0.5f, 0.5f);
            _image = imgGo.GetComponent<RawImage>();
            _image.raycastTarget = true;

            var drag = frame.gameObject.AddComponent<CropDragRelay>();
            drag.Owner = this;

            AddBtn(cardRt, "－", new Vector2(-110f, -150f), () => SetUiScale(_uiScale / 1.15f));
            AddBtn(cardRt, "＋", new Vector2(-50f, -150f), () => SetUiScale(_uiScale * 1.15f));
            AddBtn(cardRt, "缩小", new Vector2(30f, -150f), () => SetZoom(_zoom / 1.15f));
            AddBtn(cardRt, "放大", new Vector2(100f, -150f), () => SetZoom(_zoom * 1.15f));
            AddBtn(cardRt, "取消", new Vector2(-60f, -190f), Cancel);
            AddBtn(cardRt, "确认", new Vector2(60f, -190f), Confirm, accent: true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _pan += eventData.delta;
            ApplyLayout();
        }

        public void OnScroll(PointerEventData eventData)
        {
            SetZoom(_zoom * (eventData.scrollDelta.y > 0f ? 1.1f : 1f / 1.1f));
        }

        void SetZoom(float z)
        {
            _zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
            ApplyLayout();
        }

        void SetUiScale(float s)
        {
            _uiScale = Mathf.Clamp(s, 0.75f, 1.6f);
            _cropPx = 240f * _uiScale;
            var card = transform.Find("Card") as RectTransform;
            if (card != null)
            {
                card.sizeDelta = new Vector2(360f * _uiScale, 420f * _uiScale);
            }

            var stage = transform.Find("Card/Stage") as RectTransform;
            if (stage != null)
            {
                stage.sizeDelta = new Vector2(280f * _uiScale, 280f * _uiScale);
            }

            ApplyLayout();
        }

        void ApplyLayout()
        {
            if (_source == null || _imageRt == null)
            {
                return;
            }

            var crop = _cropPx;
            var minEdge = Mathf.Min(_source.width, _source.height);
            var fit = crop / minEdge;
            var disp = fit * _zoom;
            _imageRt.sizeDelta = new Vector2(_source.width * disp, _source.height * disp);
            var maxPanX = Mathf.Max(0f, (_imageRt.sizeDelta.x - crop) * 0.5f);
            var maxPanY = Mathf.Max(0f, (_imageRt.sizeDelta.y - crop) * 0.5f);
            _pan.x = Mathf.Clamp(_pan.x, -maxPanX, maxPanX);
            _pan.y = Mathf.Clamp(_pan.y, -maxPanY, maxPanY);
            _imageRt.anchoredPosition = _pan;
        }

        void Confirm()
        {
            if (_source == null)
            {
                Cancel();
                return;
            }

            var png = BakeCrop();
            var cb = _onConfirm;
            Close();
            if (png != null)
            {
                cb?.Invoke(png);
            }
            else
            {
                Debug.LogWarning("[Overlay] 截取失败。");
                _onCancel?.Invoke();
            }
        }

        byte[] BakeCrop()
        {
            var crop = Mathf.RoundToInt(_cropPx);
            var minEdge = Mathf.Min(_source.width, _source.height);
            var fit = _cropPx / minEdge;
            var disp = fit * _zoom;
            var half = crop * 0.5f;
            var texCenterX = _source.width * 0.5f - _pan.x / disp;
            var texCenterY = _source.height * 0.5f - _pan.y / disp;
            var halfTex = half / disp;
            var x0 = Mathf.FloorToInt(texCenterX - halfTex);
            var y0 = Mathf.FloorToInt(texCenterY - halfTex);
            var size = Mathf.Max(1, Mathf.RoundToInt(halfTex * 2f));
            x0 = Mathf.Clamp(x0, 0, Mathf.Max(0, _source.width - size));
            y0 = Mathf.Clamp(y0, 0, Mathf.Max(0, _source.height - size));
            if (x0 + size > _source.width)
            {
                size = _source.width - x0;
            }

            if (y0 + size > _source.height)
            {
                size = _source.height - y0;
            }

            size = Mathf.Max(1, size);
            var pixels = _source.GetPixels(x0, y0, size, size);
            var square = new Texture2D(size, size, TextureFormat.RGBA32, false);
            square.SetPixels(pixels);
            square.Apply(false, false);
            var outSize = Mathf.Min(OverlayAvatarCodec.MaxEdge, size);
            Texture2D finalTex = square;
            if (outSize != size)
            {
                finalTex = Resize(square, outSize, outSize);
                Destroy(square);
            }

            var png = OverlayAvatarCodec.ProcessToPng(finalTex.EncodeToPNG());
            Destroy(finalTex);
            return png;
        }

        static Texture2D Resize(Texture2D source, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        void Cancel()
        {
            var cb = _onCancel;
            Close();
            cb?.Invoke();
        }

        public void ForceClose()
        {
            Close();
        }

        void Close()
        {
            if (_source != null)
            {
                Destroy(_source);
                _source = null;
            }

            if (_image != null)
            {
                _image.texture = null;
            }

            gameObject.SetActive(false);
            _onConfirm = null;
            _onCancel = null;
        }

        void OnDestroy()
        {
            if (_source != null)
            {
                Destroy(_source);
            }
        }

        void AddBtn(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction click,
            bool accent = false)
        {
            var img = CreateImage(title, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(img, accent: accent);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(56f, 28f);
            FillLabel(rt, title, 13, OverlaySkin.Text);
            img.gameObject.AddComponent<Button>().onClick.AddListener(click);
        }

        static Image CreateImage(string name, Transform parent, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            return image;
        }

        static Text PlaceLabel(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = OverlaySprites.UiFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            return label;
        }

        static void FillLabel(Transform parent, string text, int size, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = OverlaySprites.UiFont;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            Stretch((RectTransform)go.transform);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        sealed class CropDragRelay : MonoBehaviour, IDragHandler, IScrollHandler
        {
            public OverlayAvatarCropUi Owner;

            public void OnDrag(PointerEventData eventData)
            {
                Owner?.OnDrag(eventData);
            }

            public void OnScroll(PointerEventData eventData)
            {
                Owner?.OnScroll(eventData);
            }
        }
    }
}
