using Hypocycloid.Core;
using UnityEngine.Localization;

namespace Baseline
{
    public struct LocaleRequestedSignal : ISignal
    {
        public int Index;
    }

    public struct LocaleChangedSignal : ISignal
    {
        public Locale Locale;
    }

    public struct WindowModeRequestedSignal : ISignal
    {
        public WindowPresentationMode Mode;
    }
}
