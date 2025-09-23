// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Overlays.Profile.Sections.Ranks;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osuTK;
using System.Linq;
using System;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.Profile.Sections
{
    public partial class LocalRanksSection : ProfileSection
    {
        public override string Identifier => "local_ranks";
        public override LocalisableString Title => UsersStrings.ShowExtraTopRanksTitle;

        public LocalRanksSection(List<ScoreInfo> scores)
        {
            // RelativeSizeAxes = Axes.X;
            // AutoSizeAxes = Axes.Y;
            // Direction = FillDirection.Vertical;
            // Spacing = new Vector2(0, 2);

            AddInternal(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    // new ProfileSectionHeader(UsersStrings.ShowExtraTopRanksTitle),
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        ChildrenEnumerable = scores.Take(100).Select((s, i) => new DrawableProfileLocalWeightedScore(s, Math.Pow(0.95, i)))
                    },
                }
            });
        }
    }
}
