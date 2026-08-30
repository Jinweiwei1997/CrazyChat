using UnityEngine;

namespace CrazyChat.Overlay.Interact
{
    public sealed class TomatoInteractAction : IOverlayInteractAction
    {
        public string Id => "tomato";

        public string Label => "扔番茄";

        public void Play(OverlayInteractFx fx, Vector2 from, Vector2 to)
        {
            if (fx != null)
            {
                fx.PlayTomato(from, to);
            }
        }
    }
}
