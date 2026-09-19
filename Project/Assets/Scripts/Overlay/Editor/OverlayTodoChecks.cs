#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace CrazyChat.Overlay
{
    public static class OverlayTodoChecks
    {
        [MenuItem("CrazyChat/Validate Todo Frequencies")]
        public static void Run()
        {
            var settings = new OverlayUserSettings();
            Assert.IsFalse(settings.HasTodos);
            settings.Todos.Add(new OverlayUserSettings.TodoItem { text = "  " });
            Assert.IsFalse(settings.HasTodos);
            settings.Todos[0].text = "喝水";
            Assert.IsTrue(settings.HasTodos);
            foreach (OverlayUserSettings.TodoFrequency frequency in Enum.GetValues(typeof(OverlayUserSettings.TodoFrequency)))
            {
                var task = new OverlayUserSettings.TodoItem { text = "test", frequency = frequency };
                var today = new DateTime(2026, 9, 17);
                task.ToggleCompletion(today);
                Assert.IsTrue(task.IsComplete(today));
                task.ToggleCompletion(today);
                Assert.IsFalse(task.IsComplete(today));
                var afterUndo = JsonUtility.FromJson<OverlayUserSettings.TodoItem>(JsonUtility.ToJson(task));
                Assert.IsFalse(afterUndo.IsComplete(today));
                task.ToggleCompletion(today);
                Assert.IsTrue(task.IsComplete(today));
            }
            var item = new OverlayUserSettings.TodoItem { text = "喝水", id = "test" };
            Assert.IsFalse(item.IsComplete(new DateTime(2026, 9, 17)));
            item.completedPeriod = "once";
            Assert.IsTrue(item.IsComplete(new DateTime(2030, 1, 1)));

            item.frequency = OverlayUserSettings.TodoFrequency.Daily;
            item.completedPeriod = "2026-09-17";
            Assert.IsTrue(item.IsComplete(new DateTime(2026, 9, 17, 23, 59, 59)));
            Assert.IsTrue(item.IsComplete(new DateTime(2026, 9, 18, 5, 59, 59)));
            Assert.IsFalse(item.IsComplete(new DateTime(2026, 9, 18, 6, 0, 0)));
            Assert.IsFalse(item.IsComplete(new DateTime(2026, 9, 16)));
            item.completedPeriod = "2028-02-29";
            Assert.IsTrue(item.IsComplete(new DateTime(2028, 2, 29, 6, 0, 0)));
            Assert.IsTrue(item.IsComplete(new DateTime(2028, 3, 1, 5, 59, 59)));
            Assert.IsFalse(item.IsComplete(new DateTime(2028, 3, 1, 6, 0, 0)));

            settings.EnsureCheckin();
            var checkin = settings.Checkin;
            Assert.AreEqual(OverlayUserSettings.DefaultCheckinText, checkin.DisplayText);
            var count = settings.Todos.Count;
            settings.EnsureCheckin();
            Assert.AreEqual(count, settings.Todos.Count);
            settings.DeleteTodo(checkin);
            Assert.IsNotNull(settings.Checkin);
            checkin.ToggleCompletion(new DateTime(2026, 9, 18, 6, 0, 0));
            checkin.ToggleCompletion(new DateTime(2026, 9, 18, 7, 0, 0));
            Assert.IsTrue(checkin.IsComplete(new DateTime(2026, 9, 19, 5, 59, 59)));
            Assert.IsFalse(checkin.IsComplete(new DateTime(2026, 9, 19, 6, 0, 0)));

            item.frequency = OverlayUserSettings.TodoFrequency.Weekly;
            item.completedPeriod = "2026-09-14";
            Assert.IsTrue(item.IsComplete(new DateTime(2026, 9, 14)));
            Assert.IsTrue(item.IsComplete(new DateTime(2026, 9, 20, 23, 59, 59)));
            Assert.IsFalse(item.IsComplete(new DateTime(2026, 9, 21)));
            item.completedPeriod = "2025-12-29";
            Assert.IsTrue(item.IsComplete(new DateTime(2026, 1, 4)));
            Assert.IsFalse(item.IsComplete(new DateTime(2026, 1, 5)));

            var restored = JsonUtility.FromJson<OverlayUserSettings.TodoItem>(JsonUtility.ToJson(item));
            Assert.AreEqual(item.text, restored.text);
            Assert.AreEqual(item.frequency, restored.frequency);
            Assert.IsTrue(restored.IsComplete(new DateTime(2026, 1, 4)));

            var prefab = Resources.Load<GameObject>("Prefab/UI/SettingsMenu");
            var page = prefab.transform.Find("SettingsPanel/Background/Pages/TodoPage");
            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Find("Add").GetComponent<UnityEngine.UI.Button>());
            var scroll = page.Find("List").GetComponent<UnityEngine.UI.ScrollRect>();
            Assert.IsTrue(scroll.vertical && !scroll.horizontal);
            Assert.IsNotNull(scroll.content);
            var row = page.Find("RowTemplate");
            Assert.IsFalse(row.gameObject.activeSelf);
            Assert.IsNotNull(row.GetComponentInChildren<UnityEngine.UI.InputField>(true));
            Assert.AreEqual(3, row.GetComponentInChildren<UnityEngine.UI.Dropdown>(true).options.Count);
            Assert.IsNotNull(row.Find("Delete").GetComponent<UnityEngine.UI.Button>());
            Debug.Log("[Overlay] Todo checks passed: empty/blank tasks, completion/undo/recompletion, once/daily/weekly boundaries, serialization and settings prefab.");
        }
    }
}
#endif
