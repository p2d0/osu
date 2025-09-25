// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Osu.Utils;
using osu.Game.Rulesets.UI;
using osu.Framework.Graphics;
using osuTK;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Input.StateChanges;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Mod that randomises the positions of the <see cref="HitObject"/>s
    /// </summary>
    public class OsuModRandomV2 : ModRandomV2, IApplicableToBeatmap, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override LocalisableString Description => "It never gets boring!";

        public override Type[] IncompatibleMods => base.IncompatibleMods.Append(typeof(OsuModTargetPractice)).ToArray();

        [SettingSource("Angle sharpness", "How sharp angles should be", IsHidden = nameof(CustomAngle))]
        public BindableFloat AngleSharpness { get; } = new BindableFloat(7)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 0.1f
        };

        [SettingSource("Stream Angle sharpness", "How sharp angles should be", IsHidden = nameof(CustomAngle))]
        public BindableFloat StreamAngleSharpness { get; } = new BindableFloat(7)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 0.1f
        };

        [SettingSource("Custom angle", "Custom angle")]
        public Bindable<bool> CustomAngle { get; } = new BindableBool(false);

        public enum AngleEnum {
            Square,
            Star,
            FourtyFive,
        }

        public float GetAngleValue()
        {
            return AngleValues[Angle.Value];
        }

        private static readonly Dictionary<AngleEnum, float> AngleValues = new Dictionary<AngleEnum, float>
        {
            { AngleEnum.Square, 1.57079f }, // 90 degrees in radians
            { AngleEnum.Star, 2.51327f },   // ~36 degrees in radians
            { AngleEnum.FourtyFive, 0.785398f } // 45 degrees in radians
        };

        [SettingSource("Angle", "Angle selector", IsVisible = nameof(CustomAngle))]
        public Bindable<AngleEnum> Angle { get; } = new Bindable<AngleEnum>
        {
            Default = AngleEnum.Square,
        };


        [SettingSource("Aim Distance Multiplier", "How much bigger the distance")]
        public BindableFloat AimDistanceMultiplier { get; } = new BindableFloat(1)
        {
            MinValue = 0.5f,
            MaxValue = 10,
            Precision = 0.01f
        };

        [SettingSource("Power jumps", "Longer jumps get a smaller increase in distance")]
        public BindableBool PowerJumps { get; } = new BindableBool(false);

        [SettingSource("Stream Distance Multiplier", "How much bigger the distance")]
        public BindableFloat StreamDistanceMultiplier { get; } = new BindableFloat(1)
        {
            MinValue = 0.1f,
            MaxValue = 50,
            Precision = 0.1f
        };

        [SettingSource("Exponential streams", "Larger stream spacing receives diminishing distance increases")]
        public BindableBool PowerStreams { get; } = new BindableBool(false);

        [SettingSource("Divide by divisor", "Use the beat divisor to distinguish streams/jumps")]
        public Bindable<bool> DivideByDivisor { get; } = new BindableBool(false);

        [SettingSource("Divisor", "Divisor selector", IsVisible = nameof(DivideByDivisor))]
        public BindableInt Divisor { get; } = new BindableInt(2)
            {
                MinValue = 1,
                MaxValue = 16,
                Default = 2,
            };

        [SettingSource("Stream Distance", "How much bigger the distance", IsHidden = nameof(DivideByDivisor))]
        public BindableInt StreamDistance { get; } = new BindableInt(100)
        {
            MinValue = 25,
            MaxValue = 500,
        };

        // [SettingSource("Extend playarea", "Extend playarea")]
        public Bindable<bool> ExtendPlayArea { get; } = new BindableBool(false);

        // [SettingSource("Infinite playarea", "Infinite playarea")]
        public Bindable<bool> InfinitePlayArea { get; } = new BindableBool(false);

        // [SettingSource("SquareMod", "SquareMod")]
        public Bindable<bool> SquareMod { get; } = new BindableBool(false);

        [SettingSource("Hard random", "Remove circle padding and unnecessary shifting")]
        public Bindable<bool> Hardcore { get; } = new BindableBool(true);


        // [SettingSource("Square Distance", "Square distance")]
        // public BindableInt SquareDistance { get; } = new BindableInt(200)
        // {
        //     MinValue = 100,
        //     MaxValue = 1000,
        // };

        private static readonly float playfield_diagonal = OsuPlayfield.BASE_SIZE.LengthFast;

        private Random random = null!;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (beatmap is not OsuBeatmap osuBeatmap)
                return;

            if(SquareMod.Value)
                makeMapSquare(beatmap);

            Seed.Value ??= RNG.Next();

            random = new Random((int)Seed.Value);

            var positionInfos = OsuHitObjectGenerationUtils.GeneratePositionInfos(osuBeatmap.HitObjects);

            // Offsets the angles of all hit objects in a "section" by the same amount.
            float sectionOffset = 0;

            // Whether the angles are positive or negative (clockwise or counter-clockwise flow).
            bool flowDirection = false;
            var originalDistance = 0f;
            for (int i = 0; i < positionInfos.Count; i++)
            {
                originalDistance = positionInfos[i].DistanceFromPrevious;
                if (isStream(osuBeatmap, positionInfos,i, originalDistance))
                {
                    if(PowerStreams.Value){
                        float distance = positionInfos[i].DistanceFromPrevious;
                        float M = StreamDistanceMultiplier.Value;

                        // --- TUNABLE PARAMETERS ---

                        // 1. How much to amplify the base multiplier at zero distance.
                        // A value of 2.5 means the initial bonus is 2.5 times the base bonus.
                        // Based on your example, 2.65 is a great starting point.
                        float bonusAmplifier = 2.65f;

                        // 2. How quickly the bonus effect decays with distance.
                        // A larger value means a faster drop-off.
                        // Based on your example, 0.023 is a great starting point.
                        float decayRate = 0.023f;

                        // --------------------------


                        // Step 1: Calculate the maximum possible bonus (at distance = 0)
                        // We use (M - 1) because a multiplier of M=10 is a "bonus" of 9.
                        float maxBonus = (M - 1f) * bonusAmplifier;

                        // Step 2: Calculate the decay factor based on distance
                        // MathF.Exp() is the e^x function. The negative sign makes it decay.
                        float decayFactor = MathF.Exp(-decayRate * distance);

                        // Step 3: Calculate the actual bonus for this distance
                        float currentBonus = maxBonus * decayFactor;

                        // Step 4: Apply the final multiplier
                        // The final multiplier is 1.0 (no change) plus the current bonus.
                        positionInfos[i].DistanceFromPrevious *= (1f + currentBonus);
                    }
                    else
                        positionInfos[i].DistanceFromPrevious *= StreamDistanceMultiplier.Value;
                }
                else
                {
                    // if(CustomAngle.Value)
                    //     positionInfos[i].DistanceFromPrevious = SquareDistance.Value;
                    // else
                    if(PowerJumps.Value)
                        positionInfos[i].DistanceFromPrevious *= MathF.Pow(AimDistanceMultiplier.Value, 1f - positionInfos[i].DistanceFromPrevious / 640f);
                    else
                        positionInfos[i].DistanceFromPrevious *= AimDistanceMultiplier.Value;

                    // if(AimDistanceMultiplier.Value >= 1)
                    //     positionInfos[i].DistanceFromPrevious *= MathF.Pow(AimDistanceMultiplier.Value, 1f - positionInfos[i].DistanceFromPrevious / 640f);
                    // else
                    //     positionInfos[i].DistanceFromPrevious *= MathF.Pow(AimDistanceMultiplier.Value, positionInfos[i].DistanceFromPrevious / 640f);

                }
                if (shouldStartNewSection(osuBeatmap, positionInfos, i))
                {
                    sectionOffset = isStream(osuBeatmap, positionInfos,i, originalDistance) ? getRandomOffsetStream(0.008f) : getRandomOffset(0.0008f);
                    flowDirection = !flowDirection;
                }

                if (positionInfos[i].HitObject is Slider slider && random.NextDouble() < 0.5)
                {
                    OsuHitObjectGenerationUtils.FlipSliderInPlaceHorizontally(slider);
                }

                if (i == 0)
                {
                    positionInfos[i].DistanceFromPrevious = (float)(random.NextDouble() * OsuPlayfield.BASE_SIZE.Y / 2);
                    // Logger.Log($"DistanceFromPrevious i=0 {positionInfos[i].DistanceFromPrevious}");
                    positionInfos[i].RelativeAngle = (float)(random.NextDouble() * 2 * Math.PI - Math.PI);
                }
                else
                {
                    // Offsets only the angle of the current hit object if a flow change occurs.
                    float flowChangeOffset = 0;

                    // Offsets only the angle of the current hit object.
                    float oneTimeOffset = isStream(osuBeatmap, positionInfos,i, originalDistance) ? getRandomOffsetStream(0.002f) : getRandomOffset(0.002f);

                    if (shouldApplyFlowChange(positionInfos, i))
                    {
                        flowChangeOffset = isStream(osuBeatmap, positionInfos,i, originalDistance) ? getRandomOffsetStream(0.002f) : getRandomOffset(0.002f);
                        flowDirection = !flowDirection;
                    }

                    float totalOffset =
                        // sectionOffset and oneTimeOffset should mainly affect patterns with large spacing.
                        (sectionOffset + oneTimeOffset) * positionInfos[i].DistanceFromPrevious +
                        // flowChangeOffset should mainly affect streams.
                        flowChangeOffset * (playfield_diagonal - positionInfos[i].DistanceFromPrevious);

                    // Logger.Log($"totalOffset i={i} {totalOffset}");
                    positionInfos[i].RelativeAngle = isStream(osuBeatmap, positionInfos,i, originalDistance) ?
                        getRelativeTargetAngleStream(positionInfos[i].DistanceFromPrevious, totalOffset, flowDirection) :
                        getRelativeTargetAngle(positionInfos[i].DistanceFromPrevious, totalOffset, flowDirection);
                    // Logger.Log($"Distance from previous i={i} {positionInfos[i].DistanceFromPrevious}");
                    // Logger.Log($"RelativeAngle i={i} {positionInfos[i].RelativeAngle}");
                }
            }

            osuBeatmap.HitObjects = OsuHitObjectGenerationUtils.RepositionHitObjects(positionInfos,Hardcore.Value,ExtendPlayArea.Value,InfinitePlayArea.Value);
            // var updatedPositionInfos = OsuHitObjectGenerationUtils.GeneratePositionInfos(osuBeatmap.HitObjects);
            // var count = 0;
            // var totalDistanceDifferece =  0f;
            // for (int i = 0; i < positionInfos.Count; i++)
            // {
            //     if(positionInfos[i].DistanceFromPrevious - updatedPositionInfos[i].DistanceFromPrevious > 50)
            //     {
            //         count++;
            //         Logger.Log($"Position is more than 50 off i={i} {positionInfos[i].DistanceFromPrevious} {updatedPositionInfos[i].DistanceFromPrevious}");
            //         Logger.Log($"Updated RelativeAngle i={i} {positionInfos[i].RelativeAngle}");
            //     }
            //     totalDistanceDifferece += positionInfos[i].DistanceFromPrevious - updatedPositionInfos[i].DistanceFromPrevious;
            //     // Logger.Log($"Updated DistanceFromPrevious i={i} {positionInfos[i].DistanceFromPrevious}");
            //     // Logger.Log($"Updated RelativeAngle i={i} {positionInfos[i].RelativeAngle}");
            // }
            // Logger.Log($"Count (Lower is better) {count}");
            // Logger.Log($"TotalDistanceDifferece (Lower is better) {totalDistanceDifferece}");
        }

        private bool isStream(OsuBeatmap osuBeatmap, List<OsuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos,int i, float originalDistance)
        {
            if(DivideByDivisor.Value) {
                // Logger.Log($"Divisor {osuBeatmap.ControlPointInfo.GetClosestBeatDivisor(positionInfos.HitObject.StartTime)}");
                var beatLength = osuBeatmap.ControlPointInfo.TimingPointAt(positionInfos[i].HitObject.StartTime).BeatLength;
                if(i+1 < positionInfos.Count && positionInfos[i].HitObject is HitObject circle && positionInfos[i+1].HitObject is HitObject nextCircle){
                    Logger.Log($"{nextCircle.StartTime - circle.StartTime}");
                    // NOTE: The +1 feels hacky
                    var isStream = nextCircle.StartTime - circle.StartTime + 1 < beatLength / Divisor.Value;
                    if(!isStream && i > 0 && positionInfos[i-1] != null && positionInfos[i-1].HitObject is HitObject previousCircle)
                        return circle.StartTime - previousCircle.StartTime + 1 < beatLength / Divisor.Value;
                }

                return true;
            }
            else
                return (originalDistance < StreamDistance.Value);
        }

        // private int Moved = 0;

        private OsuInputManager inputManager = null!;

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Grab the input manager to disable the user's cursor, and for future use
            inputManager = ((DrawableOsuRuleset)drawableRuleset).KeyBindingInputManager;
        }

        // public void Update(Playfield playfield)
        // {
        //     var padding = 200;
        //     // Get current cursor position
        //     var cursorPos = playfield.Cursor.AsNonNull().ActiveCursor.DrawPosition;
        //     if(cursorPos.X > (playfield.DrawPosition.X + playfield.DrawWidth) || cursorPos.Y > (playfield.DrawPosition.Y + playfield.DrawHeight ))
        //     {
        //         Logger.Log($"Cursor position is out of bounds: {cursorPos}");
        //         var offsetX = cursorPos.X > playfield.DrawWidth ? -cursorPos.X + padding : 0;
        //         var offsetY = cursorPos.Y < playfield.DrawHeight ? -cursorPos.Y + padding : 0;
        //         // inputManager.MoveMouseTo(new Vector2(1000,500));
        //         playfield.Cursor.ActiveCursor.MoveTo(new Vector2(200,200), 0, Easing.None);
        //         // new MousePositionAbsoluteInput { Position = playfield.ToScreenSpace(new Vector2(200,200)) }.Apply(inputManager.CurrentState, inputManager);
        //         playfield.MoveTo(new Vector2(offsetX, offsetY), 0, Easing.None);
        //         return;
        //     }

        //     if(cursorPos.X < 0 || cursorPos.Y < 0)
        //     {
        //         Logger.Log($"Cursor position is out of bounds: {cursorPos}");
        //         var offsetX = cursorPos.X < 0 ? -cursorPos.X - padding : 0;
        //         var offsetY = cursorPos.Y < 0 ? -cursorPos.Y - padding : 0;
        //         // inputManager.MoveMouseTo(new Vector2(1000,500));
        //         // playfield.Cursor.ActiveCursor.MoveTo(new Vector2(200,200), 0, Easing.None);
        //         // new MousePositionAbsoluteInput { Position = playfield.ToScreenSpace(new Vector2(200,200)) }.Apply(inputManager.CurrentState, inputManager);
        //         playfield.MoveTo(new Vector2(offsetX, offsetY), 0, Easing.None);
        //         // playfield.Cursor.ActiveCursor.
        //         // Moved += 1;
        //     }

        //     // Logger.Log($"Cursor position: {cursorPos}");
        //     // Logger.Log($"Playfield: {playfield.DrawWidth}x{playfield.DrawHeight}");

        //     // Calculate screen center
        //     // var screenCenter = new Vector2(playfield.DrawWidth / 2, playfield.DrawHeight / 2);

        //     // // Calculate cursor's offset from center
        //     // var cursorOffset = cursorPos - screenCenter;

        //     // // Calculate desired playfield offset (scaled by extension factor)
        //     // var extension = ExtendPlayArea.Value ? PlayAreaExtension.Value : 0;
        //     // var targetOffset = new Vector2(
        //     //     extension * (cursorOffset.X / screenCenter.X),
        //     //     extension * (cursorOffset.Y / screenCenter.Y)
        //     // );

        //     // // Apply the offset to the playfield
        // }

        private float getRandomOffset(float stdDev)
        {
            // Range: [0.5, 2]
            // Higher angle sharpness -> lower multiplier
            float customMultiplier = (1.5f * AngleSharpness.MaxValue - AngleSharpness.Value) / (1.5f * AngleSharpness.MaxValue - AngleSharpness.Default);

            return OsuHitObjectGenerationUtils.RandomGaussian(random, 0, stdDev * customMultiplier);
        }

        private float getRandomOffsetStream(float stdDev)
        {
            // Range: [0.5, 2]
            // Higher angle sharpness -> lower multiplier
            float customMultiplier = (1.5f * StreamAngleSharpness.MaxValue - StreamAngleSharpness.Value) / (1.5f * StreamAngleSharpness.MaxValue - StreamAngleSharpness.Default);

            return OsuHitObjectGenerationUtils.RandomGaussian(random, 0, stdDev * customMultiplier);
        }

        /// <param name="targetDistance">The target distance between the previous and the current <see cref="OsuHitObject"/>.</param>
        /// <param name="offset">The angle (in rad) by which the target angle should be offset.</param>
        /// <param name="flowDirection">Whether the relative angle should be positive or negative.</param>
        private float getRelativeTargetAngle(float targetDistance, float offset, bool flowDirection)
        {
            // Range: [0.1, 1]
            float angleSharpness = AngleSharpness.Value / AngleSharpness.MaxValue;
            // Range: [0, 0.9]
            float angleWideness = 1 - angleSharpness;

            // Range: [-60, 30]
            float customOffsetX = angleSharpness * 100 - 70;
            // Range: [-0.075, 0.15]
            float customOffsetY = angleWideness * 0.25f - 0.075f;

            targetDistance += customOffsetX;
            float angle = (float)(2.16 / (1+ 200 * Math.Exp(0.036 * (targetDistance - 310 + customOffsetX))) + 0.5);
            angle += offset + customOffsetY;

            float relativeAngle = (float)Math.PI - angle;
            // Logger.Log($"relativeAngle {relativeAngle} angle {angle}");

            if(CustomAngle.Value)
                relativeAngle = GetAngleValue();

            return flowDirection ? -relativeAngle : relativeAngle;
        }

        /// <param name="targetDistance">The target distance between the previous and the current <see cref="OsuHitObject"/>.</param>
        /// <param name="offset">The angle (in rad) by which the target angle should be offset.</param>
        /// <param name="flowDirection">Whether the relative angle should be positive or negative.</param>
        private float getRelativeTargetAngleStream(float targetDistance, float offset, bool flowDirection)
        {
            // Range: [0.1, 1]
            float angleSharpness = StreamAngleSharpness.Value / StreamAngleSharpness.MaxValue;
            // Range: [0, 0.9]
            float angleWideness = 1 - angleSharpness;

            // Range: [-60, 30]
            float customOffsetX = angleSharpness * 100 - 70;
            // Range: [-0.075, 0.15]
            float customOffsetY = angleWideness * 0.25f - 0.075f;

            targetDistance += customOffsetX;
            float angle = (float)(2.16 / (1+ 200 * Math.Exp(0.036 * (targetDistance - 310 + customOffsetX))) + 0.5);
            angle += offset + customOffsetY;

            float relativeAngle = (float)Math.PI - angle;

            if(CustomAngle.Value)
                relativeAngle = GetAngleValue();

            return flowDirection ? -relativeAngle : relativeAngle;
        }

        /// <returns>Whether a new section should be started at the current <see cref="OsuHitObject"/>.</returns>
        private bool shouldStartNewSection(OsuBeatmap beatmap, IReadOnlyList<OsuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos, int i)
        {
            return false;
            if (i == 0)
                return true;

            // Exclude new-combo-spam and 1-2-combos.
            bool previousObjectStartedCombo = positionInfos[Math.Max(0, i - 2)].HitObject.IndexInCurrentCombo > 1 &&
                                              positionInfos[i - 1].HitObject.NewCombo;
            bool previousObjectWasOnDownbeat = OsuHitObjectGenerationUtils.IsHitObjectOnBeat(beatmap, positionInfos[i - 1].HitObject, true);
            bool previousObjectWasOnBeat = OsuHitObjectGenerationUtils.IsHitObjectOnBeat(beatmap, positionInfos[i - 1].HitObject);

            return (previousObjectStartedCombo && random.NextDouble() < 0.6f) ||
                   previousObjectWasOnDownbeat ||
                   (previousObjectWasOnBeat && random.NextDouble() < 0.4f);
        }

        /// <returns>Whether a flow change should be applied at the current <see cref="OsuHitObject"/>.</returns>
        private bool shouldApplyFlowChange(IReadOnlyList<OsuHitObjectGenerationUtils.ObjectPositionInfo> positionInfos, int i)
        {
            // Exclude new-combo-spam and 1-2-combos.
            bool previousObjectStartedCombo = positionInfos[Math.Max(0, i - 2)].HitObject.IndexInCurrentCombo > 1 &&
                                              positionInfos[i - 1].HitObject.NewCombo;

            return previousObjectStartedCombo && random.NextDouble() < 0.6f;
        }

        private void makeMapSquare(IBeatmap beatmap){
            // The 'is' pattern matching already declares and assigns osuBeatmap if the cast is successful.

            if (beatmap is not OsuBeatmap osuBeatmap)
                return;


            var firstHitObject = beatmap.HitObjects.OfType<OsuHitObject>().FirstOrDefault();
            if (firstHitObject == null)
                return;

            // The original code had a redundant LastOrDefault check.
            var lastTime = beatmap.HitObjects.Last().StartTime;

            var firstTime = firstHitObject.StartTime;

            var hitObjects = new List<OsuHitObject>();


            var beatLength = osuBeatmap.ControlPointInfo.TimingPointAt(firstTime).BeatLength / Divisor.Value;

            // if (beatLength <= 0) // Add a safeguard against division by zero or invalid timing points
            //     beatLength = 200;

            var spacing = 200; // The side length of the square

            do
            {
                int cornerIndex = hitObjects.Count % 4;
                Vector2 position;

                // Determine the position based on which corner we are on
                switch (cornerIndex)
                {
                    case 0: // Bottom-left
                        position = new Vector2(0, 0);
                        break;
                    case 1: // Bottom-right
                        position = new Vector2(spacing, 0);
                        break;
                    case 2: // Top-right
                        position = new Vector2(spacing, spacing);
                        break;
                    case 3: // Top-left
                        position = new Vector2(0, spacing);
                        break;
                    default: // This case will never be reached with % 4
                        position = Vector2.Zero;
                        break;
                }

                var circle = new HitCircle
                {
                    Position = position,
                    // Start a new combo at the beginning of each square
                    NewCombo = cornerIndex == 0,
                    Samples = firstHitObject.Samples
                };

                circle.ApplyDefaults(osuBeatmap.ControlPointInfo, osuBeatmap.Difficulty);
                // circle.StartTime = firstTime + (beatLength * hitObjects.Count);
                circle.StartTime = osuBeatmap.ControlPointInfo.GetClosestSnappedTime(firstTime + (beatLength * hitObjects.Count));
                circle.TimePreempt = firstHitObject.TimePreempt;
                circle.TimeFadeIn = firstHitObject.TimeFadeIn;
                // if(osuBeatmap.ControlPointInfo.TimingPointAt(circle.StartTime).BeatLength / 2 > 5)
                //     beatLength = osuBeatmap.ControlPointInfo.TimingPointAt(circle.StartTime).BeatLength / 2;
                hitObjects.Add(circle);
            } while (hitObjects.Last().StartTime < lastTime);

            osuBeatmap.HitObjects = hitObjects;

            beatmap.Breaks.Clear();
            Logger.Log($"Breaks: {beatmap.Breaks.Count}");
            Logger.Log($"TotalBreakTime: {beatmap.TotalBreakTime}ms" );
        }
    }
}
