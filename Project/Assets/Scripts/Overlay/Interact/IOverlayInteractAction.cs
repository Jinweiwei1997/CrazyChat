using UnityEngine;

namespace CrazyChat.Overlay.Interact
{
    public interface IOverlayInteractAction
    {
        string Id { get; }

        string Label { get; }

        void Play(OverlayInteractFx fx, Vector2 from, Vector2 to);
    }
}
