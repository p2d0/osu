// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Users;

namespace osu.Game.Overlays.Profile.Header.Components
{
    public partial class PPGraph : RankGraph
    {
        protected override float GetDataPointHeight(int rank) => MathF.Pow(rank, 4);
        private const int pp_days = 88;

        protected override UserGraphTooltipContent GetTooltipContent(int index, int rank)
        {
            int days = pp_days - index + 1;

            return new UserGraphTooltipContent
            {
                Name = @"PP",
                Count = rank.ToLocalisableString("#,##0"),
                Time = days == 0 ? "now" : $"{"day".ToQuantity(days)} ago",
            };
        }
    }
}
