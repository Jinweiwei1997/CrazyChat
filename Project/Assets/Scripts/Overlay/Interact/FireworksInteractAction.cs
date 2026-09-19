using UnityEngine;

namespace CrazyChat.Overlay.Interact
{
    public sealed class FireworksInteractAction : IOverlayInteractAction
    {
        public string Id => "fireworks";

        public string Label => "放烟花";

        public void Play(OverlayInteractFx fx, Vector2 from, Vector2 to)
        {
            if (fx != null)
            {
                fx.PlayFireworks(to);
            }
        }
    }
}
