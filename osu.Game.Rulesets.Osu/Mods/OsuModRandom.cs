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

namespace osu.Game.Rulesets.Osu.Mods
{
    /// <summary>
    /// Mod that randomises the positions of the <see cref="HitObject"/>s
    /// </summary>
    public class OsuModRandom : ModRandom, IApplicableToBeatmap, IUpdatableByPlayfield, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override LocalisableString Description => "It never gets boring!";

        public override Type[] IncompatibleMods => base.IncompatibleMods.Append(typeof(OsuModTargetPractice)).ToArray();

        [SettingSource("Angle sharpness", "How sharp angles should be")]
        public BindableFloat AngleSharpness { get; } = new BindableFloat(7)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 0.1f
        };

        [SettingSource("Stream Angle sharpness", "How sharp angles should be")]
        public BindableFloat StreamAngleSharpness { get; } = new BindableFloat(7)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 0.1f
        };

        [SettingSource("Aim Distance Multiplier", "How much bigger the distance")]
        public BindableFloat AimDistanceMultiplier { get; } = new BindableFloat(1)
        {
            MinValue = 0.5f,
            MaxValue = 10,
            Precision = 0.01f
        };

        [SettingSource("Stream Distance Multiplier", "How much bigger the distance")]
        public BindableFloat StreamDistanceMultiplier { get; } = new BindableFloat(1)
        {
            MinValue = 0.1f,
            MaxValue = 50,
            Precision = 0.1f
        };

        [SettingSource("Stream Distance", "How much bigger the distance")]
        public BindableInt StreamDistance { get; } = new BindableInt(100)
        {
            MinValue = 25,
            MaxValue = 500,
        };

        [SettingSource("Hard random", "Remove circle padding and unnecessary shifting")]
        public Bindable<bool> Hardcore { get; } = new BindableBool(false);

        [SettingSource("Squarish angle", "Squareish angle")]
        public Bindable<bool> Squareish { get; } = new BindableBool(false);


        [SettingSource("Extend playarea", "Extend playarea")]
        public Bindable<bool> ExtendPlayArea { get; } = new BindableBool(false);

        [SettingSource("Infinite playarea", "Infinite playarea")]
        public Bindable<bool> InfinitePlayArea { get; } = new BindableBool(false);


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
                if(positionInfos[i].DistanceFromPrevious < StreamDistance.Value){
                    positionInfos[i].DistanceFromPrevious *= StreamDistanceMultiplier.Value;
                } else {
                    // if(Squareish.Value)
                    //     positionInfos[i].DistanceFromPrevious = SquareDistance.Value;
                    // else
                        positionInfos[i].DistanceFromPrevious *= AimDistanceMultiplier.Value;

                    // if(AimDistanceMultiplier.Value >= 1)
                    //     positionInfos[i].DistanceFromPrevious *= MathF.Pow(AimDistanceMultiplier.Value, 1f - positionInfos[i].DistanceFromPrevious / 640f);
                    // else
                    //     positionInfos[i].DistanceFromPrevious *= MathF.Pow(AimDistanceMultiplier.Value, positionInfos[i].DistanceFromPrevious / 640f);

                }
                if (shouldStartNewSection(osuBeatmap, positionInfos, i))
                {
                    sectionOffset = originalDistance < StreamDistance.Value ? getRandomOffsetStream(0.008f) : getRandomOffset(0.0008f);
                    flowDirection = !flowDirection;
                }

                if (positionInfos[i].HitObject is Slider slider && random.NextDouble() < 0.5)
                {
                    OsuHitObjectGenerationUtils.FlipSliderInPlaceHorizontally(slider);
                }

                if (i == 0)
                {
                    positionInfos[i].DistanceFromPrevious = (float)(random.NextDouble() * OsuPlayfield.BASE_SIZE.Y / 2);
                    Logger.Log($"DistanceFromPrevious i=0 {positionInfos[i].DistanceFromPrevious}");
                    positionInfos[i].RelativeAngle = (float)(random.NextDouble() * 2 * Math.PI - Math.PI);
                }
                else
                {
                    // Offsets only the angle of the current hit object if a flow change occurs.
                    float flowChangeOffset = 0;

                    // Offsets only the angle of the current hit object.
                    float oneTimeOffset = originalDistance < StreamDistance.Value ? getRandomOffsetStream(0.002f) : getRandomOffset(0.002f);

                    if (shouldApplyFlowChange(positionInfos, i))
                    {
                        flowChangeOffset = originalDistance < StreamDistance.Value ? getRandomOffsetStream(0.002f) : getRandomOffset(0.002f);
                        flowDirection = !flowDirection;
                    }

                    float totalOffset =
                        // sectionOffset and oneTimeOffset should mainly affect patterns with large spacing.
                        (sectionOffset + oneTimeOffset) * positionInfos[i].DistanceFromPrevious +
                        // flowChangeOffset should mainly affect streams.
                        flowChangeOffset * (playfield_diagonal - positionInfos[i].DistanceFromPrevious);

                    // Logger.Log($"totalOffset i={i} {totalOffset}");
                    positionInfos[i].RelativeAngle = originalDistance < StreamDistance.Value ?
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
        // private int Moved = 0;

        private OsuInputManager inputManager = null!;

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Grab the input manager to disable the user's cursor, and for future use
            inputManager = ((DrawableOsuRuleset)drawableRuleset).KeyBindingInputManager;
        }

        public void Update(Playfield playfield)
        {
            // Get current cursor position
            var cursorPos = playfield.Cursor.AsNonNull().ActiveCursor.DrawPosition;
            if(cursorPos.X < 0 || cursorPos.Y < 0)
            {
                var offsetX = cursorPos.X < 0 ? -cursorPos.X : 0;
                var offsetY = cursorPos.Y < 0 ? -cursorPos.Y : 0;
                // inputManager.MoveMouseTo(new Vector2(1000,500));
                // playfield.Cursor.ActiveCursor.MoveTo(new Vector2(200,200), 0, Easing.None);
                new MousePositionAbsoluteInput { Position = new Vector2(1000,500) }.Apply(inputManager.CurrentState, inputManager);
                // playfield.Cursor.ActiveCursor.
                // playfield.MoveToOffset(new Vector2(offsetX, offsetY), 0, Easing.None);
                // Moved += 1;
            }

            Logger.Log($"Cursor position: {cursorPos}");
            Logger.Log($"Playfield: {playfield.DrawWidth}x{playfield.DrawHeight}");

            // Calculate screen center
            // var screenCenter = new Vector2(playfield.DrawWidth / 2, playfield.DrawHeight / 2);

            // // Calculate cursor's offset from center
            // var cursorOffset = cursorPos - screenCenter;

            // // Calculate desired playfield offset (scaled by extension factor)
            // var extension = ExtendPlayArea.Value ? PlayAreaExtension.Value : 0;
            // var targetOffset = new Vector2(
            //     extension * (cursorOffset.X / screenCenter.X),
            //     extension * (cursorOffset.Y / screenCenter.Y)
            // );

            // // Apply the offset to the playfield
        }

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
            Logger.Log($"relativeAngle {relativeAngle} angle {angle}");

            if(Squareish.Value)
                relativeAngle = 1.57079f;

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

            if(Squareish.Value)
                relativeAngle = 1.57079f;

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
    }
}
