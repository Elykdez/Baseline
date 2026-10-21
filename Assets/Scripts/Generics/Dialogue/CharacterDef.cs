using System;
using System.Collections.Generic;
using Baseline.Defs;
using Baseline.Presentation;
using UnityEngine;

namespace Baseline.Dialogue
{
    public enum PortraitFacing { Original = 0, Mirrored = 1 }
    public enum PortraitFacingOverride { CharacterDefault = 0, Original = 1, Mirrored = 2 }

    [Serializable]
    public sealed class CharacterExpression
    {
        public string id;
        public Sprite illustration;
        public Sprite portrait;
        public SoundCueDef voice;
    }

    [CreateAssetMenu(fileName = "Character", menuName = "Baseline/Defs/Character")]
    public sealed class CharacterDef : Def
    {
        public string displayName;
        public string role;
        public string nameKey;
        public string roleKey;
        public Sprite illustration;
        public Sprite portrait;
        public SoundCueDef defaultVoice;
        [Tooltip("Default orientation relative to the artwork. Dialogue lines may override it.")]
        public PortraitFacing facing;
        public Color accent = Color.white;
        public CharacterExpression[] expressions = Array.Empty<CharacterExpression>();

        public Sprite ResolvePortrait(string expression, bool compact)
        {
            if (!string.IsNullOrEmpty(expression) && expressions != null)
                foreach (CharacterExpression entry in expressions)
                    if (entry != null && entry.id == expression)
                    {
                        Sprite resolved = compact ? entry.portrait : entry.illustration;
                        if (resolved != null) return resolved;
                    }
            return compact ? portrait : illustration;
        }

        public PortraitFacing ResolveFacing(PortraitFacingOverride lineFacing = PortraitFacingOverride.CharacterDefault)
            => lineFacing switch
            {
                PortraitFacingOverride.Original => PortraitFacing.Original,
                PortraitFacingOverride.Mirrored => PortraitFacing.Mirrored,
                _ => facing,
            };

        public SoundCueDef ResolveVoice(string expression)
        {
            if (!string.IsNullOrEmpty(expression) && expressions != null)
                foreach (CharacterExpression entry in expressions)
                    if (entry != null && entry.id == expression && entry.voice != null) return entry.voice;
            return defaultVoice;
        }

        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(role))
                throw new InvalidOperationException($"Character '{DefName}' needs a name and role.");
            if (illustration == null || portrait == null)
                throw new InvalidOperationException($"Character '{DefName}' needs its default illustration and portrait.");
            if (expressions == null)
                throw new InvalidOperationException($"Character '{DefName}' has no expression collection.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterExpression expression in expressions)
                if (expression == null || string.IsNullOrWhiteSpace(expression.id) || !ids.Add(expression.id)
                    || expression.illustration == null || expression.portrait == null)
                    throw new InvalidOperationException($"Character '{DefName}' has an invalid or duplicate expression.");
        }
    }
}
