
using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Scoring;

namespace osu.Game.Users
{
    public class LocalUserManager
    {
        private readonly ScoreManager scoreManager;
        private readonly IAPIProvider api;
        private readonly LocalUserStatisticsProvider statisticsProvider;

        public LocalUserManager(ScoreManager scoreManager, IAPIProvider api, LocalUserStatisticsProvider statisticsProvider)
        {
            this.scoreManager = scoreManager;
            this.api = api;
            this.statisticsProvider = statisticsProvider;
        }

        public async Task<APIUser> GetLocalUserWithStatisticsAsync (RulesetInfo ruleset)
        {
            if(statisticsProvider.GetStatisticsFor(ruleset) is UserStatistics stats)
                return new APIUser
                {
                    Id = api.LocalUser.Value.Id,
                    Username = api.LocalUser.Value.Username,
                    CountryCode = api.LocalUser.Value.CountryCode,
                    CoverUrl = api.LocalUser.Value.CoverUrl,
                    Statistics = stats
                };
            return await GetLocalUserWithStatisticsUncached(ruleset);
        }

        private async Task<APIUser> GetLocalUserWithStatisticsUncached(RulesetInfo ruleset)
        {
            return await Task.Run(() =>
            {
                var scores = api.LocalUser.Value.Username == "Guest"
                    ? scoreManager.All(ruleset)
                    : scoreManager.ByUsername(api.LocalUser.Value.Username, ruleset);

                double totalPP = 0;
                double weight = 1;

                // Using LINQ can simplify this calculation
                foreach (var score in scores.Where(s => s.PP.HasValue).OrderByDescending(s => s.PP.Value).Take(100))
                {
                    totalPP += score.PP.Value * weight;
                    weight *= 0.95;
                }

                // 3. RETURN the user object at the end of the lambda.
                //    This changes the task from Task to Task<APIUser>.
                return new APIUser
                {
                    Id = api.LocalUser.Value.Id,
                    Username = api.LocalUser.Value.Username,
                    CountryCode = api.LocalUser.Value.CountryCode,
                    CoverUrl = api.LocalUser.Value.CoverUrl,
                    Statistics = new UserStatistics
                    {
                        IsRanked = true,
                        PP = (decimal)totalPP,
                        // Ensure scores list is not empty before calling Average to prevent exceptions
                        Accuracy = scores.Any() ? scores.Take(100).Average(s => s.Accuracy) * 100 : 1 * 100,
                        GlobalRank = (int)totalPP,
                        RankHistory = new APIRankHistory
                        {
                            Data = Enumerable.Range(2345, 45).Concat(Enumerable.Range(2109, 40)).ToArray(),
                            Mode = ruleset.ShortName
                        },
                        PlayCount = scores.Count,
                        TotalScore = scores.Sum(s => s.TotalScore),
                        MaxCombo = scores.Any() ? scores.Max(s => s.MaxCombo) : 0,
                        // TotalHits = scores.Sum(s => s.Count300 + s.Count100 + s.Count50 + s.CountMiss),
                        GradesCount = new UserStatistics.Grades
                        {
                            SSPlus = scores.Count(s => s.Rank == ScoreRank.XH),
                            SS = scores.Count(s => s.Rank == ScoreRank.X),
                            SPlus = scores.Count(s => s.Rank == ScoreRank.SH),
                            S = scores.Count(s => s.Rank == ScoreRank.S),
                            A = scores.Count(s => s.Rank == ScoreRank.A)
                        },

                    }
                };
            });
        }
    }
}
