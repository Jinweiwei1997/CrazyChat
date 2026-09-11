using UnityEngine;

namespace CrazyChat.Overlay.Interact
{
    public sealed class TestMessageInteractAction : IOverlayInteractAction
    {
        public string Id => "test_message";

        public string Label => "测试消息";

        public void Play(OverlayInteractFx fx, Vector2 from, Vector2 to)
        {
        }
    }
}
