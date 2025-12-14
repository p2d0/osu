// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.MOsu.Edit.Blueprints.HitCircles;
using osu.Game.Rulesets.MOsu.Objects;

namespace osu.Game.Rulesets.MOsu.Edit
{
    public class HitCircleCompositionTool : CompositionTool
    {
        public HitCircleCompositionTool()
            : base(nameof(HitCircle))
        {
        }

        public override Drawable CreateIcon() => new BeatmapStatisticIcon(BeatmapStatisticsIconType.Circles);

        public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new HitCirclePlacementBlueprint();
    }
}
