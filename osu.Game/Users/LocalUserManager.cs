
using System;
using System.Collections.Generic;
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
            return await GetLocalUserWithStatisticsUncached(ruleset).ConfigureAwait(false);
        }

        public static (float totalPp, float totalAccuracy) CalculateUserTotalPerformanceAggregates(IEnumerable<ScoreInfo> scores)
        {
            ScoreInfo[] groupedScores = scores.ToArray();

            // Build the diminishing sum
            double factor = 1;
            double totalPp = 0;
            double totalAccuracy = 0;

            foreach (var score in groupedScores)
            {
                totalPp += score.PP!.Value * factor;
                totalAccuracy += score.Accuracy * factor;
                factor *= 0.95;
            }

            // This weird factor is to keep legacy compatibility with the diminishing bonus of 0.25 by 0.9994 each score.
            // Of note, this is using de-duped scores which may be below 1,000 depending on how the user plays.
            totalPp += (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, groupedScores.Length));

            // We want our accuracy to be normalized.
            if (groupedScores.Length > 0)
            {
                // We want the percentage, not a factor in [0, 1], hence we divide 20 by 100.
                totalAccuracy *= 100.0 / (20 * (1 - Math.Pow(0.95, groupedScores.Length)));
            }

            if (double.IsNegative(totalPp) || double.IsNaN(totalPp) || double.IsInfinity(totalPp))
                throw new InvalidOperationException($"Calculating total PP resulted in invalid value ({totalPp})");

            if (double.IsNaN(totalAccuracy) || double.IsInfinity(totalAccuracy))
                throw new InvalidOperationException($"Calculating total accuracy resulted in invalid value ({totalAccuracy})");

            // handle floating point precision edge cases.
            totalAccuracy = Math.Clamp(totalAccuracy, 0, 100);

            return ((float)totalPp, (float)totalAccuracy);
        }

        public async Task<APIUser> GetLocalUserWithStatisticsUncached(RulesetInfo ruleset)
        {
            return await Task.Run(() =>
            {
                // 1. Get all scores for the user in the specified ruleset.
                var allScores = api.LocalUser.Value.Username == "Guest"
                    ? scoreManager.All(ruleset)
                    : scoreManager.ByUsername(api.LocalUser.Value.Username, ruleset);

                // Filter for scores that have a PP value, as these are the only ones that affect rank.
                var scoresWithPP = allScores.Where(s => s.PP.HasValue).ToList();

                // 2. Create a reusable helper function to calculate weighted total PP.
                // This takes a list of scores and calculates the weighted PP based on the top 100 plays.
                // double calculateTotalPP(IEnumerable<ScoreInfo> scoresToCalculate)
                // {
                //     double totalPP = 0;
                //     double weight = 1;

                //     foreach (var score in scoresToCalculate.OrderByDescending(s => s.PP.Value).Take(100))
                //     {
                //         totalPP += score.PP.Value * weight;
                //         weight *= 0.95;
                //     }

                //     return totalPP;
                // }

                // --- Rank History Calculation (Last 90 Days) ---
                var rankHistoryData = new int[90];
                var today = DateTimeOffset.UtcNow.Date;

                // Efficiently iterate through the last 90 days to build the history.
                for (int i = 0; i < 90; i++)
                {
                    // Determine the date for the current point in history (from 89 days ago to today).
                    var historyDate = today.AddDays(-89 + i);

                    // For each day, filter ALL scores to include only those set on or before that historical date.
                    // This ensures the calculation is cumulative and includes plays from "before the period".
                    Logger.Log($"Calculating rank for {historyDate:yyyy-MM-dd}, total scores: {allScores.Count}, scores with PP: {scoresWithPP.Count}");
                    var scoresUpToHistoryDate = scoresWithPP.Where(s => s.Date.Date <= historyDate);

                    // Calculate the user's total PP as it would have been on that day.
                    (var pp, var acc) = CalculateUserTotalPerformanceAggregates(scoresUpToHistoryDate);
                    rankHistoryData[i] = (int)pp;
                }

                // --- Final User Object Construction ---
                // Calculate current total PP and accuracy using all available scores.
                (float currentTotalPP, float accuracy) = CalculateUserTotalPerformanceAggregates(allScores);

                return new APIUser
                {
                    Id = api.LocalUser.Value.Id,
                    Username = api.LocalUser.Value.Username,
                    CountryCode = api.LocalUser.Value.CountryCode,
                    CoverUrl = api.LocalUser.Value.CoverUrl,
                    Statistics = new UserStatistics
                    {
                        IsRanked = true,
                        PP = (decimal)currentTotalPP,
                        Accuracy = accuracy,
                        GlobalRank = (int)currentTotalPP, // Placeholder for rank
                        RankHistory = new APIRankHistory
                        {
                            Data = rankHistoryData,
                            Mode = ruleset.ShortName
                        },
                        PlayCount = allScores.Count,
                        TotalScore = allScores.Sum(s => s.TotalScore),
                        PlayTime = (int)allScores.Sum(s => s.BeatmapInfo.Length) / 3600,
                        MaxCombo = allScores.Any() ? allScores.Max(s => s.MaxCombo) : 0,
                        GradesCount = new UserStatistics.Grades
                        {
                            SSPlus = allScores.Count(s => s.Rank == ScoreRank.XH),
                            SS = allScores.Count(s => s.Rank == ScoreRank.X),
                            SPlus = allScores.Count(s => s.Rank == ScoreRank.SH),
                            S = allScores.Count(s => s.Rank == ScoreRank.S),
                            A = allScores.Count(s => s.Rank == ScoreRank.A)
                        },
                    }
                };
            }).ConfigureAwait(false);
        }
    }
}
