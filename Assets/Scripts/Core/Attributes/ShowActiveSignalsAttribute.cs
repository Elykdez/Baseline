using System;
using UnityEngine;

namespace Hypocycloid.Core
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ShowActiveSignalsAttribute : PropertyAttribute
    {
        public ShowActiveSignalsAttribute() { }
    }
}
