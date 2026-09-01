using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrazyChat.Overlay
{
    public sealed class OverlayHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        Action _enter;
        Action _exit;

        public static void Bind(GameObject go, Action enter, Action exit)
        {
            if (go == null)
            {
                return;
            }

            var relay = go.GetComponent<OverlayHoverRelay>();
            if (relay == null)
            {
                relay = go.AddComponent<OverlayHoverRelay>();
            }

            relay._enter = enter;
            relay._exit = exit;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _enter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _exit?.Invoke();
        }
    }
}
