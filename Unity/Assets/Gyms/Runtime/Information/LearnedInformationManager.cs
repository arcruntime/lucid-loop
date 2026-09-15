using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LucidLoop.Gyms
{
    [Serializable]
    public sealed class LearnedInformationEntry
    {
        [SerializeField] string id, content, category;
        [SerializeField] long order;
        [SerializeField] bool unread = true, retained;
        public string Id => id;
        public string Content => content;
        public string Category => category;
        public long Order => order;
        public bool IsUnread => unread;
        public bool IsRetained => retained;
        internal LearnedInformationEntry(string id, string text, string category, long order, bool retained)
        { this.id=id;content=text;this.category=category;this.order=order;this.retained=retained; }
        internal void Publish() => retained=true;
        internal void Read() => unread=false;
    }

    [DisallowMultipleComponent]
    public sealed class LearnedInformationManager : MonoBehaviour
    {
        [SerializeField] List<LearnedInformationEntry> entries = new List<LearnedInformationEntry>();
        public IReadOnlyList<LearnedInformationEntry> Entries => entries.AsReadOnly();
        public event Action Changed;
        public bool HasUnread => entries.Any(e=>e.IsRetained && e.IsUnread);
        public LearnedInformationEntry MostRecentUnread => entries.Where(e=>e.IsRetained && e.IsUnread).OrderByDescending(e=>e.Order).FirstOrDefault();
        public bool HasRetained => entries.Any(e=>e.IsRetained);

        // For locally confirmed information, publish immediately and show the unread icon.
        public string AddLearnedInformation(string text)
        {
            string id=Guid.NewGuid().ToString("N");
            AddLearnedInformation(id,text);return id;
        }
        public bool AddLearnedInformation(string id, string text, string category = "", bool deferUntilLoopStart = false)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(text))return false;
            if(entries.Any(e=>e.Id==id))return false; // Revisions and reconnects never re-alert an existing ID.
            long order=entries.Count==0?1:entries.Max(e=>e.Order)+1;
            entries.Add(new LearnedInformationEntry(id,text,category,order,!deferUntilLoopStart));Changed?.Invoke();return true;
        }
        public void NotifyLoopStarted()
        {
            bool changed=false;
            foreach(var entry in entries)if(!entry.IsRetained){entry.Publish();changed=true;}
            if(changed)Changed?.Invoke();
        }
        public void MarkRead(IEnumerable<string> ids)
        {
            var selected=new HashSet<string>(ids);bool changed=false;
            foreach(var entry in entries)if(entry.IsRetained && entry.IsUnread && selected.Contains(entry.Id)){entry.Read();changed=true;}
            if(changed)Changed?.Invoke();
        }
        public void ClearForNewGame() { entries.Clear();Changed?.Invoke(); }
    }
}
