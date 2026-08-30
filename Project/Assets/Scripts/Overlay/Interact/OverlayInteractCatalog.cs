using System.Collections.Generic;

namespace CrazyChat.Overlay.Interact
{
    /// <summary>
    /// 互动动作注册表。以后加玩法：实现 IOverlayInteractAction，并在此登记一行。
    /// </summary>
    public static class OverlayInteractCatalog
    {
        static readonly IOverlayInteractAction[] Actions =
        {
            new TomatoInteractAction()
        };

        public static IReadOnlyList<IOverlayInteractAction> All => Actions;

        public static bool TryGet(string id, out IOverlayInteractAction action)
        {
            for (var i = 0; i < Actions.Length; i++)
            {
                if (Actions[i].Id == id)
                {
                    action = Actions[i];
                    return true;
                }
            }

            action = null;
            return false;
        }
    }
}
