// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Screens.Select.Leaderboards;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.IO.Network;
using osu.Game.Extensions;
using System.Text;

namespace osu.Game.Online.API.Requests
{
    public class GetScoresRequest : APIRequest<APIScoresCollection>, IEquatable<GetScoresRequest>
    {
        public const int DEFAULT_SCORES_PER_REQUEST = 50;
        public const int MAX_SCORES_PER_REQUEST = 100;

        private readonly IBeatmapInfo beatmapInfo;
        private readonly BeatmapLeaderboardScope scope;
        private readonly IRulesetInfo ruleset;
        private readonly IEnumerable<IMod> mods;
        private readonly int Id;

        public GetScoresRequest(IBeatmapInfo beatmapInfo, IRulesetInfo ruleset, BeatmapLeaderboardScope scope = BeatmapLeaderboardScope.Global, IEnumerable<IMod>? mods = null)
        {
            Logger.Log($"Creating GetScoresRequest for beatmap {beatmapInfo.OnlineID} with ruleset {ruleset.ShortName} and scope {scope}");
            if (beatmapInfo.OnlineID <= 0)
                this.Id = 0;
            else
                this.Id = beatmapInfo.OnlineID;

            if (scope == BeatmapLeaderboardScope.Local)
                throw new InvalidOperationException("Should not attempt to request online scores for a local scoped leaderboard");

            this.beatmapInfo = beatmapInfo;
            this.scope = scope;
            this.ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
            this.mods = mods ?? Array.Empty<IMod>();
        }

        protected override string Target => $@"beatmaps/{Id}/solo-scores{createQueryParameters()}";

        private string createQueryParameters()
        {
            StringBuilder query = new StringBuilder(@"?");

            query.Append($@"type={scope.ToString().ToLowerInvariant()}");
            query.Append($@"&mode={ruleset.ShortName}");
            // query.Append($@"&difficulty={beatmapInfo.DifficultyName}");
            // query.Append($@"&star_rating={beatmapInfo.StarRating}");
            // query.Append($@"&beatmapset_id={beatmapInfo.BeatmapSet.OnlineID}");
            // query.Append($@"&beatmapset_title={beatmapInfo.Metadata.Title}");
            // query.Append($@"&beatmapset_artist={beatmapInfo.Metadata.Artist}");
            // query.Append($@"&beatmapset_creator={beatmapInfo.Metadata.Author}");
            // query.Append($@"&beatmapset_source={beatmapInfo.Metadata.Source}");
            // query.Append($@"&beatmapset_status={beatmapInfo.BeatmapSet.Status.ToString().ToLowerInvariant()}");
            // query.Append($@"&beatmapset_bpm={beatmapInfo.BeatmapSet.MaxBPM}");
            // query.Append($@"&beatmapset_star_difficulty={beatmapInfo.BeatmapSet.MaxStarDifficulty}");
            // query.Append($@"&beatmapset_length={beatmapInfo.BeatmapSet.MaxLength}");

            query.Append($@"&difficulty={beatmapInfo.DifficultyName}");
            query.Append($@"&star_rating={beatmapInfo.StarRating}");
            query.Append($@"&beatmapset_id={beatmapInfo.BeatmapSet?.OnlineID ?? 0}");
            query.Append($@"&beatmapset_title={beatmapInfo.Metadata.Title}");
            query.Append($@"&beatmapset_artist={beatmapInfo.Metadata.Artist }");
            query.Append($@"&beatmapset_creator={beatmapInfo.Metadata.Author?.Username }");
            query.Append($@"&beatmapset_source={beatmapInfo.Metadata.Source }");
            // query.Append($@"&beatmapset_status={beatmapInfo.BeatmapSet?.Status.ToString().ToLowerInvariant() }");
            query.Append($@"&beatmapset_bpm={beatmapInfo.BeatmapSet?.MaxBPM ?? 0}");
            query.Append($@"&beatmapset_star_difficulty={beatmapInfo.BeatmapSet?.MaxStarDifficulty ?? 0}");
            query.Append($@"&beatmapset_length={beatmapInfo.BeatmapSet?.MaxLength ?? 0}");
            query.Append($@"&checksum={beatmapInfo.MD5Hash }");
            query.Append($@"&total_length={beatmapInfo.Length}");
            query.Append($@"&hit_length={beatmapInfo.Length}"); // Assuming hit_length is same as Length
            query.Append($@"&bpm={beatmapInfo.BPM}");
            query.Append($@"&max_combo={beatmapInfo.TotalObjectCount}"); // Proxy for max_combo
            query.Append($@"&count_normal={Math.Round((double)(beatmapInfo.TotalObjectCount - (0.2 * beatmapInfo.TotalObjectCount)))}");
            query.Append($@"&count_slider={Math.Round((double)(beatmapInfo.TotalObjectCount - (0.8 * beatmapInfo.TotalObjectCount)))}");
            query.Append($@"&count_spinner=1");
            query.Append($@"&diff_drain={beatmapInfo.Difficulty.DrainRate}");
            query.Append($@"&diff_size={beatmapInfo.Difficulty.CircleSize}");
            query.Append($@"&diff_overall={beatmapInfo.Difficulty.OverallDifficulty}");
            query.Append($@"&diff_approach={beatmapInfo.Difficulty.ApproachRate}");
            query.Append($@"&playcount=0"); // Not in IBeatmapInfo
            query.Append($@"&passcount=0"); // Not in IBeatmapInfo
            query.Append($@"&user_id={beatmapInfo.Metadata.Author.OnlineID}");

            foreach (var mod in mods)
                query.Append($@"&mods[]={mod.Acronym}");

            return query.ToString();
        }

        public bool Equals(GetScoresRequest? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return beatmapInfo.Equals(other.beatmapInfo)
                   && scope == other.scope
                   && ruleset.Equals(other.ruleset)
                   && mods.SequenceEqual(other.mods);
        }
    }
}
