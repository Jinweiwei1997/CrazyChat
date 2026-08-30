using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrazyChat.Overlay
{
    [Serializable]
    public sealed class OverlayChatMessage
    {
        public string from;
        public string text;
        public long time;
        public bool mine;
    }

    public sealed class OverlayChatStore
    {
        const string FileName = "overlay_chat.json";

        int _maxPerFriend = 200;

        readonly Dictionary<ulong, List<OverlayChatMessage>> _threads = new Dictionary<ulong, List<OverlayChatMessage>>();
        readonly Dictionary<ulong, int> _unread = new Dictionary<ulong, int>();

        public event Action Changed;

        public void SetMaxPerFriend(int max)
        {
            _maxPerFriend = Mathf.Max(1, max);
        }

        public void Load()
        {
            _threads.Clear();
            _unread.Clear();
            var json = ReadLocal();
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<ChatFile>(json);
                if (data?.threads == null)
                {
                    return;
                }

                for (var i = 0; i < data.threads.Count; i++)
                {
                    var thread = data.threads[i];
                    if (!ulong.TryParse(thread.id, out var id) || thread.messages == null)
                    {
                        continue;
                    }

                    _threads[id] = thread.messages;
                    _unread[id] = Mathf.Max(0, thread.unread);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 读取聊天记录失败: " + e.Message);
            }
        }

        public void Save()
        {
            var data = new ChatFile { threads = new List<ThreadFile>() };
            foreach (var pair in _threads)
            {
                data.threads.Add(new ThreadFile
                {
                    id = pair.Key.ToString(),
                    unread = GetUnread(pair.Key),
                    messages = pair.Value
                });
            }

            WriteLocal(JsonUtility.ToJson(data));
        }

        public OverlayChatMessage GetLatest(ulong friendId)
        {
            if (!_threads.TryGetValue(friendId, out var list) || list.Count == 0)
            {
                return null;
            }

            return list[list.Count - 1];
        }

        public IReadOnlyList<OverlayChatMessage> GetMessages(ulong friendId)
        {
            return _threads.TryGetValue(friendId, out var list) ? list : (IReadOnlyList<OverlayChatMessage>)Array.Empty<OverlayChatMessage>();
        }

        public int GetUnread(ulong friendId)
        {
            return _unread.TryGetValue(friendId, out var n) ? n : 0;
        }

        public void Add(ulong friendId, string text, bool mine, ulong fromId)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            var latest = GetLatest(friendId);
            if (latest != null && latest.mine == mine && latest.text == text &&
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() - latest.time < 2)
            {
                return;
            }

            if (!_threads.TryGetValue(friendId, out var list))
            {
                list = new List<OverlayChatMessage>();
                _threads[friendId] = list;
            }

            list.Add(new OverlayChatMessage
            {
                from = fromId.ToString(),
                text = text,
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                mine = mine
            });

            while (list.Count > _maxPerFriend)
            {
                list.RemoveAt(0);
            }

            if (!mine)
            {
                _unread[friendId] = GetUnread(friendId) + 1;
            }

            Save();
            Changed?.Invoke();
        }

        public void MarkRead(ulong friendId)
        {
            if (GetUnread(friendId) == 0)
            {
                return;
            }

            _unread[friendId] = 0;
            Save();
            Changed?.Invoke();
        }

        static string ReadLocal()
        {
            try
            {
                var path = Path.Combine(Application.persistentDataPath, FileName);
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        static void WriteLocal(string json)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(Path.Combine(Application.persistentDataPath, FileName), json, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Overlay] 写入聊天记录失败: " + e.Message);
            }
        }

        [Serializable]
        class ChatFile
        {
            public List<ThreadFile> threads = new List<ThreadFile>();
        }

        [Serializable]
        class ThreadFile
        {
            public string id;
            public int unread;
            public List<OverlayChatMessage> messages = new List<OverlayChatMessage>();
        }
    }
}
