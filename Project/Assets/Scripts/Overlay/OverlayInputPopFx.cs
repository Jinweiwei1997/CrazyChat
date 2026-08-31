using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrazyChat.Overlay
{
    public sealed class OverlayInputPopFx : MonoBehaviour
    {
        const float Size = 32f;
        const float Rise = 42f;
        const float Duration = 0.55f;
        const int PoolSize = 12;

        RectTransform _root;
        readonly List<Pop> _pops = new List<Pop>(PoolSize);

        public static OverlayInputPopFx Create(Transform parent)
        {
            var go = new GameObject("InputPopFx", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<OverlayInputPopFx>();
            fx._root = (RectTransform)go.transform;
            Stretch(fx._root);
            return fx;
        }

        public void Play(Vector2 head, Sprite icon)
        {
            if (icon == null)
            {
                return;
            }

            var pop = Rent();
            pop.Image.sprite = icon;
            pop.Image.color = Color.white;
            pop.Group.alpha = 1f;
            pop.From = head + new Vector2(Random.Range(-8f, 8f), 4f);
            pop.Until = Time.unscaledTime + Duration;
            pop.Rt.sizeDelta = new Vector2(Size, Size);
            pop.Rt.anchoredPosition = pop.From;
            pop.Rt.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            var now = Time.unscaledTime;
            for (var i = 0; i < _pops.Count; i++)
            {
                var pop = _pops[i];
                if (!pop.Rt.gameObject.activeSelf)
                {
                    continue;
                }

                var left = pop.Until - now;
                if (left <= 0f)
                {
                    pop.Rt.gameObject.SetActive(false);
                    continue;
                }

                var t = 1f - left / Duration;
                pop.Rt.anchoredPosition = pop.From + new Vector2(0f, Rise * t);
                pop.Group.alpha = t < 0.25f ? 1f : 1f - (t - 0.25f) / 0.75f;
            }
        }

        Pop Rent()
        {
            for (var i = 0; i < _pops.Count; i++)
            {
                if (!_pops[i].Rt.gameObject.activeSelf)
                {
                    return _pops[i];
                }
            }

            if (_pops.Count >= PoolSize)
            {
                var oldest = _pops[0];
                for (var i = 1; i < _pops.Count; i++)
                {
                    if (_pops[i].Until < oldest.Until)
                    {
                        oldest = _pops[i];
                    }
                }

                return oldest;
            }

            var image = CreateImage("Pop", _root);
            var pop = new Pop
            {
                Rt = image.rectTransform,
                Image = image,
                Group = image.gameObject.AddComponent<CanvasGroup>()
            };
            pop.Group.blocksRaycasts = false;
            pop.Rt.gameObject.SetActive(false);
            _pops.Add(pop);
            return pop;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        sealed class Pop
        {
            public RectTransform Rt;
            public Image Image;
            public CanvasGroup Group;
            public Vector2 From;
            public float Until;
        }
    }
}
