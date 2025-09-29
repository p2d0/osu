﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Game.Online.API.Requests;
using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.API.Requests.Responses;
using System.Collections.Generic;
using osu.Game.Online.API;
using osu.Framework.Allocation;
using osu.Framework.Localisation;
using APIUser = osu.Game.Online.API.Requests.Responses.APIUser;
using osu.Game.Scoring;
using System.Threading.Tasks;
using osu.Framework.Logging;
using System.Linq;

namespace osu.Game.Overlays.Profile.Sections.Ranks
{
    public partial class PaginatedLocalScoreContainer : PaginatedLocalProfileSubsection<ScoreInfo>
    {
        private readonly ScoreType type;

        [Resolved]
        private ScoreManager ScoreManager { get; set; } = null!;

        public PaginatedLocalScoreContainer(ScoreType type, Bindable<UserProfileData?> user, LocalisableString headerText)
            : base(user, headerText)
        {
            this.type = type;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            ItemsContainer.Direction = FillDirection.Vertical;
        }

        protected override int GetCount(APIUser user)
        {
            switch (type)
            {
                case ScoreType.Best:
                    return user.ScoresBestCount;

                case ScoreType.Firsts:
                    return user.ScoresFirstCount;

                case ScoreType.Recent:
                    return user.ScoresRecentCount;

                case ScoreType.Pinned:
                    return user.ScoresPinnedCount;

                default:
                    return 0;
            }
        }

        protected override void OnItemsReceived(List<ScoreInfo> items)
        {
            if (CurrentPage == null || CurrentPage?.Offset == 0)
                drawableItemIndex = 0;

            base.OnItemsReceived(items);
        }

        protected override async Task<List<ScoreInfo>> CreateTask(UserProfileData user) {
            Logger.Log($"Loading local scores for {user.User.Username}, ruleset: {user.Ruleset.ShortName}", level: LogLevel.Debug);
            return await getScores(user).ConfigureAwait(false);
        }
        private Task<List<ScoreInfo>> getScores(UserProfileData user){
            return Task.Run(() => {
                switch (type) {
                    case ScoreType.Recent:
                        return user.User.Username == "Guest" ? ScoreManager.Recent(user.Ruleset) : ScoreManager.Recent(user.Ruleset,user.User.Username);
                    case ScoreType.Best:
                    default:
                        return user.User.Username == "Guest" ? ScoreManager.All(user.Ruleset) : ScoreManager.ByUsername(user.User.Username,user.Ruleset);

                }});
                }

        private int drawableItemIndex;

        protected override Drawable CreateDrawableItem(ScoreInfo model)
        {
            switch (type)
            {
                default:
                    return new DrawableProfileLocalScore(model);

                case ScoreType.Best:
                    return new DrawableProfileLocalWeightedScore(model, Math.Pow(0.95, drawableItemIndex++));
            }
        }
    }
}
