// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModSquare : ModSquare, IApplicableToBeatmap
    {
        public override LocalisableString Description => "Flip objects on the chosen axes.";
        public override Type[] IncompatibleMods => new[] { typeof(ModHardRock) };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap)
                return;
            osuBeatmap = (OsuBeatmap)beatmap;

            var firstHitObject = beatmap.HitObjects.OfType<OsuHitObject>().FirstOrDefault();
            if (firstHitObject == null)
                return;
            OsuHitObject lastHitObject = beatmap.HitObjects.OfType<OsuHitObject>().LastOrDefault();
            if (lastHitObject == null)
                return;
            var firstTime = firstHitObject.StartTime;
            var lastTime = lastHitObject.StartTime;

            var hitObjects = new List<OsuHitObject>();

            var beatLength = osuBeatmap.ControlPointInfo.TimingPointAt(firstTime).BeatLength / 2;

            var objectsPerSpacing = 8;
            // var interval = 300;
            var spacing = 100;
            do
            {
                var circle = new HitCircle
                {
                    StartTime = beatLength * hitObjects.Count,
                    Position = hitObjects.Count % 2 == 0 ? Vector2.Zero : new Vector2(spacing, 0),
                    NewCombo = true
                };
                circle.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);
                hitObjects.Add(circle);
            } while (hitObjects.Last().StartTime < lastTime);

            osuBeatmap.HitObjects = hitObjects;


        }

        // public void ApplyToHitObject(HitObject hitObject)
        // {
        //     var osuObject = (OsuHitObject)hitObject;
        //     osuObject.MaximumJudgementOffset

        // }

    }
}
