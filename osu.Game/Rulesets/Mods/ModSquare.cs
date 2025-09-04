// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModSquare : Mod
    {
        public override string Name => "Square";
        public override string Acronym => "SQ";
        public override ModType Type => ModType.Conversion;
        public override double ScoreMultiplier => 1;
    }
}
