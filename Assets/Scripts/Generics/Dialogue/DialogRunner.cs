using System;

namespace Baseline.Dialogue
{
    /// <summary>Linear playback. Commands carry the displayed revision so stale callbacks cannot skip a line.</summary>
    public sealed class DialogRunner
    {
        public DialogDef Definition { get; private set; }
        public int LineIndex { get; private set; } = -1;
        public long Revision { get; private set; }
        public DialogLine Current => IsOpen ? Definition.lines[LineIndex] : null;
        public bool IsOpen => Definition != null;
        public bool OffersSkip => IsOpen && offerSkip && LineIndex == 0;
        public bool CanClose => IsOpen && (allowClose || OffersSkip);
        private bool allowClose, offerSkip;

        public event Action Changed;
        public event Action<bool> Closed;

        public void Play(DialogDef dialog, bool allowClose = true, bool offerSkip = false)
        {
            if (dialog == null) throw new ArgumentNullException(nameof(dialog));
            dialog.Validate();
            if (IsOpen) Finish(false);
            this.allowClose = allowClose;
            this.offerSkip = offerSkip;
            Definition = dialog;
            LineIndex = 0;
            Revision++;
            Changed?.Invoke();
        }

        public bool Advance(long expectedRevision)
        {
            if (!IsOpen || expectedRevision != Revision) return false;
            if (LineIndex + 1 == Definition.lines.Length)
                Finish(true);
            else
            {
                LineIndex++;
                Revision++;
                Changed?.Invoke();
            }
            return true;
        }

        public void Close()
        {
            if (CanClose) Finish(false);
        }

        /// <summary>The owning scene can always end playback, including an unskippable conversation.</summary>
        public void Cancel()
        {
            if (IsOpen) Finish(false);
        }

        private void Finish(bool completed)
        {
            Definition = null;
            LineIndex = -1;
            Revision++;
            Changed?.Invoke();
            Closed?.Invoke(completed);
        }
    }
}
