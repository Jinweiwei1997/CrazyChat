using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    /// <summary>
    /// Square crop UI embedded in the settings Dynamic page: pan/zoom, confirm writes cropped PNG bytes.
    /// </summary>
    public sealed class OverlayAvatarCropUi : MonoBehaviour
    {
        const float MinZoom = 1f;
        const float MaxZoom = 4f;
        const float ZoomStep = 1.15f;
        const float HoldRepeatDelay = 0.28f;
        const float HoldRepeatRate = 6f;
        const float StageSize = 168f;

        RectTransform _stageRt;
        RectTransform _imageRt;
        RawImage _image;
        Texture2D _source;
        float _zoom = 1f;
        Vector2 _pan;
        Action<byte[]> _onConfirm;
        Action _onCancel;
        float _cropPx = StageSize;
        float _holdZoomSign;
        float _holdZoomStartedAt = -1f;

        public static OverlayAvatarCropUi CreateEmbedded(Transform host)
        {
            var go = new GameObject("AvatarCrop", typeof(RectTransform));
            go.transform.SetParent(host, false);
            Stretch((RectTransform)go.transform);
            var ui = go.AddComponent<OverlayAvatarCropUi>();
            ui.BuildEmbedded();
            go.SetActive(false);
            return ui;
        }

        public void Bind()
        {
            _stageRt = FindNode(transform, "Stage") as RectTransform;
            _imageRt = FindNode(transform, "Stage/Frame/Mask/Photo") as RectTransform;
            _image = _imageRt != null ? _imageRt.GetComponent<RawImage>() : null;

            var frame = FindNode(transform, "Stage/Frame");
            BindEvent(frame, EventTriggerType.Drag, data => OnDrag((PointerEventData)data));
            BindEvent(frame, EventTriggerType.Scroll, data => OnScroll((PointerEventData)data));
            BindHold(FindNode(transform, "Shrink"), -1f);
            BindHold(FindNode(transform, "Grow"), 1f);
            BindClick(FindNode(transform, "Cancel"), Cancel);
            BindClick(FindNode(transform, "Confirm"), Confirm);
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
                var cancel = _onCancel;
                Close();
                cancel?.Invoke();
                return;
            }

            if (_image == null || _imageRt == null || _stageRt == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 的动态页裁剪节点不完整。");
                var cancel = _onCancel;
                Close();
                cancel?.Invoke();
                return;
            }

            _source.wrapMode = TextureWrapMode.Clamp;
            _zoom = 1f;
            _pan = Vector2.zero;
            StopZoomHold();
            if (_image != null)
            {
                _image.texture = _source;
            }

            gameObject.SetActive(true);
            RefreshCropSize();
            ApplyLayout();
        }

        void Update()
        {
            if (_holdZoomSign == 0f || _source == null)
            {
                return;
            }

            if (Time.unscaledTime - _holdZoomStartedAt < HoldRepeatDelay)
            {
                return;
            }

            var steps = HoldRepeatRate * Time.unscaledDeltaTime;
            SetZoom(_zoom * Mathf.Pow(ZoomStep, _holdZoomSign * steps));
        }

        void BuildEmbedded()
        {
            var hint = PlaceLabel(transform, "拖动移动 · 滚轮缩放 · 按住缩小/放大", 12, OverlaySkin.TextMuted);
            hint.gameObject.name = "Hint";
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, -4f);
            hintRt.sizeDelta = new Vector2(0f, 20f);
            var stage = new GameObject("Stage", typeof(RectTransform));
            stage.transform.SetParent(transform, false);
            _stageRt = (RectTransform)stage.transform;
            _stageRt.anchorMin = _stageRt.anchorMax = new Vector2(0.5f, 0.5f);
            _stageRt.pivot = new Vector2(0.5f, 0.5f);
            _stageRt.anchoredPosition = new Vector2(0f, 16f);
            _stageRt.sizeDelta = new Vector2(StageSize, StageSize);

            var frame = CreateImage("Frame", _stageRt, new Color(0.1f, 0.1f, 0.12f, 1f), OverlaySprites.RoundedSquare);
            frame.raycastTarget = true;
            Stretch(frame.rectTransform);

            var maskGo = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(frame.rectTransform, false);
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

            frame.gameObject.AddComponent<EventTrigger>();

            AddHoldZoomBtn(transform, "Shrink", "缩小", new Vector2(-40f, 44f));
            AddHoldZoomBtn(transform, "Grow", "放大", new Vector2(40f, 44f));
            AddBtn(transform, "Cancel", "取消", new Vector2(-40f, 12f));
            AddBtn(transform, "Confirm", "确认", new Vector2(40f, 12f));
        }

        void OnDrag(PointerEventData eventData)
        {
            _pan += eventData.delta;
            ApplyLayout();
        }

        void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Approximately(eventData.scrollDelta.y, 0f))
            {
                return;
            }

            SetZoom(_zoom * (eventData.scrollDelta.y > 0f ? 1.1f : 1f / 1.1f));
        }

        void SetZoom(float z)
        {
            _zoom = Mathf.Clamp(z, MinZoom, MaxZoom);
            ApplyLayout();
        }

        void BeginZoomHold(float sign)
        {
            _holdZoomSign = sign;
            _holdZoomStartedAt = Time.unscaledTime;
            SetZoom(_zoom * (sign > 0f ? ZoomStep : 1f / ZoomStep));
        }

        void StopZoomHold()
        {
            _holdZoomSign = 0f;
            _holdZoomStartedAt = -1f;
        }

        void RefreshCropSize()
        {
            if (_stageRt == null)
            {
                _cropPx = StageSize;
                return;
            }

            var width = _stageRt.rect.width;
            _cropPx = Mathf.Max(64f, width > 1f ? width : _stageRt.sizeDelta.x);
        }

        void ApplyLayout()
        {
            if (_source == null || _imageRt == null)
            {
                return;
            }

            RefreshCropSize();
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
            var cancel = _onCancel;
            Close();
            if (png != null)
            {
                cb?.Invoke(png);
            }
            else
            {
                Debug.LogWarning("[Overlay] 截取失败。");
                cancel?.Invoke();
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

            StopZoomHold();
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

        void AddBtn(Transform parent, string id, string title, Vector2 pos)
        {
            var img = CreateImage(id, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(img);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(64f, 28f);
            FillLabel(rt, title, 13, OverlaySkin.Text);
            img.gameObject.AddComponent<Button>();
        }

        void AddHoldZoomBtn(Transform parent, string id, string title, Vector2 pos)
        {
            var img = CreateImage(id, parent, OverlaySprites.Button, OverlaySprites.RoundedRect);
            OverlaySkin.ApplyButton(img);
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(64f, 28f);
            FillLabel(rt, title, 13, OverlaySkin.Text);
            img.gameObject.AddComponent<EventTrigger>();
        }

        void BindHold(Transform node, float sign)
        {
            if (node == null)
            {
                return;
            }

            BindEvent(node, EventTriggerType.PointerDown, _ => BeginZoomHold(sign));
            BindEvent(node, EventTriggerType.PointerUp, _ => StopZoomHold());
            BindEvent(node, EventTriggerType.PointerExit, _ => StopZoomHold());
        }

        static void BindEvent(
            Transform node,
            EventTriggerType type,
            UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            if (node == null)
            {
                return;
            }

            var trigger = node.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 缺少事件节点: " + node.name);
                return;
            }

            if (trigger.triggers == null)
            {
                trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();
            }

            for (var i = trigger.triggers.Count - 1; i >= 0; i--)
            {
                if (trigger.triggers[i].eventID == type)
                {
                    trigger.triggers.RemoveAt(i);
                }
            }

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        void BindClick(Component graphic, UnityEngine.Events.UnityAction action)
        {
            if (graphic == null)
            {
                return;
            }

            var button = graphic.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[Overlay] 设置 Prefab 缺少按钮组件: " + graphic.name);
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            button.transition = Selectable.Transition.None;
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

        static Text PlaceLabel(Transform parent, string text, int size, Color color)
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

        static Transform FindNode(Transform root, string path)
        {
            return root != null ? root.Find(path) : null;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

    }
}
