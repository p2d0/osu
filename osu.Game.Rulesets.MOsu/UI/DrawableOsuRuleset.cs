// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.MOsu.Configuration;
using osu.Game.Rulesets.MOsu.Mods;
using osu.Game.Rulesets.MOsu.Objects;
using osu.Game.Rulesets.MOsu.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;
using osu.Framework.Input.Events;
using osuTK.Input;
using osu.Framework.Logging;
using osu.Game.Rulesets.MOsu.Scoring;
using osu.Game.Input.Bindings;
using osu.Framework.Input.Bindings;
using osu.Game.Audio;
using osu.Framework.Threading;
using System.Reflection;

namespace osu.Game.Rulesets.MOsu.UI
{
    public partial class DrawableOsuRuleset : DrawableRuleset<OsuHitObject>, ISamplePlaybackDisabler
    {
        private Bindable<bool>? cursorHideEnabled;

        public new OsuInputManager KeyBindingInputManager => (OsuInputManager)base.KeyBindingInputManager;

        public new OsuPlayfield Playfield => (OsuPlayfield)base.Playfield;

        protected new OsuRulesetConfigManager Config => (OsuRulesetConfigManager)base.Config;

        public DrawableOsuRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;
        [Resolved]
        private GameplayClockContainer GameplayClockContainer { get; set; } = null!;
        // private BreakTracker breakTracker;

        [Resolved]
        private Player Player { get; set; } = null!;

        private readonly Bindable<bool> samplePlaybackDisabled = new BindableBool(true);
        IBindable<bool> ISamplePlaybackDisabler.SamplePlaybackDisabled => samplePlaybackDisabled;


        // public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        // {
        //     if (e.Repeat)
        //         return false;

        //     switch (e.Action)
        //     {
        //         case GlobalAction.SkipCutscene:
        //             BreakTracker breakTracker = (BreakTracker)FrameStableComponents.First(p => p is BreakTracker);
        //             //     beatmap.Value.Track.Seek(breakTracker.CurrentPeriod.Value.Value.End);
        //             // Schedule(() => {
        //             if(beatmap.Value.Track.IsLoaded && breakTracker.CurrentPeriod.Value.HasValue){
        //                 // samplePlaybackDisabled.Value = true;
        //                 Player.Seek(breakTracker.CurrentPeriod.Value.Value.End);
        //                 // (GameplayClockContainer as MasterGameplayClockContainer)?.Start();
        //                 // samplePlaybackDisabled.Value = FrameStableClock.IsCatchingUp.Value || GameplayClockContainer.IsPaused.Value;
        //             }
        //             // });
        //             return true;
        //     }

        //     return false;
        // }

        // public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        // {
        // }

        // protected override bool OnKeyDown(KeyDownEvent e)
        // {
        //     if (e.Key == Key.Space)
        //     {
        //         BreakTracker breakTracker = (BreakTracker)FrameStableComponents.First(p => p is BreakTracker);
        //         if(breakTracker.CurrentPeriod.Value.HasValue)
        //             beatmap.Value.Track.Seek(breakTracker.CurrentPeriod.Value.Value.End);
        //     }

        //     return base.OnKeyDown(e);
        // }

        private ScheduledDelegate? frameStablePlaybackResetDelegate;

        private static readonly PropertyInfo frameStablePlaybackProperty =
            typeof(DrawableRuleset).GetProperty("FrameStablePlayback", BindingFlags.Instance | BindingFlags.NonPublic);

        private void SafeSeek(double time)
        {
            if (GameplayClockContainer == null || frameStablePlaybackProperty == null)
                return;

            // Cancel any pending frame-stable restore
            if (frameStablePlaybackResetDelegate?.Cancelled == false && !frameStablePlaybackResetDelegate.Completed)
                frameStablePlaybackResetDelegate.RunTask();

            // Read current FrameStablePlayback state via reflection
            bool wasFrameStable = (bool)frameStablePlaybackProperty.GetValue(this);

            // Disable frame-stable playback
            frameStablePlaybackProperty.SetValue(this, false);

            // Perform the seek
            GameplayClockContainer.Seek(time);

            // Schedule restore of frame-stable playback after children process
            frameStablePlaybackResetDelegate = ScheduleAfterChildren(() =>
                                                                     frameStablePlaybackProperty.SetValue(this, wasFrameStable));
        }

        [BackgroundDependencyLoader]
        private void load(ReplayPlayer? replayPlayer)
        {
            if (GameplayClockContainer != null && frameStablePlaybackProperty != null)
            {
                SkipOverlay skipOverlay;
                BreakTracker breakTracker = (BreakTracker)FrameStableComponents.First(p => p is BreakTracker);
                Overlays.Add(skipOverlay = new SkipOverlay {
                        Clock = FrameStableClock,
                        ProcessCustomClock = false,
                        BreakTracker = breakTracker,
                        Depth = float.NegativeInfinity
                    });

                skipOverlay.RequestSkip = () => {
                    Logger.Log("SAFE SEEK BRO");
                    if(breakTracker.CurrentPeriod.Value.HasValue)
                        SafeSeek(breakTracker.CurrentPeriod.Value.Value.End);
                };
            }

            if (replayPlayer != null)
            {
                ReplayAnalysisOverlay analysisOverlay;
                PlayfieldAdjustmentContainer.Add(analysisOverlay = new ReplayAnalysisOverlay(replayPlayer.Score.Replay));
                Overlays.Add(analysisOverlay.CreateProxy().With(p => p.Depth = float.NegativeInfinity));
                replayPlayer.AddSettings(new ReplayAnalysisSettings(Config));

                cursorHideEnabled = Config.GetBindable<bool>(OsuRulesetSetting.ReplayCursorHideEnabled);

                // I have little faith in this working (other things touch cursor visibility) but haven't broken it yet.
                // Let's wait for someone to report an issue before spending too much time on it.
                cursorHideEnabled.BindValueChanged(enabled => Playfield.Cursor.FadeTo(enabled.NewValue ? 0 : 1), true);
            }
        }

        public override DrawableHitObject<OsuHitObject>? CreateDrawableRepresentation(OsuHitObject h) => null;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true; // always show the gameplay cursor

        protected override Playfield CreatePlayfield() => new OsuPlayfield();

        protected override PassThroughInputManager CreateInputManager() => new OsuInputManager(Ruleset.RulesetInfo);

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new OsuPlayfieldAdjustmentContainer { AlignWithStoryboard = true };

        protected override ResumeOverlay CreateResumeOverlay()
        {
            if (Mods.Any(m => m is OsuModAutopilot or OsuModTouchDevice))
                return new DelayedResumeOverlay { Scale = new Vector2(0.65f) };

            return new OsuResumeOverlay();
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new OsuFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new OsuReplayRecorder(score);

        public override double GameplayStartTime
        {
            get
            {
                if (Objects.FirstOrDefault() is OsuHitObject first)
                    return first.StartTime - Math.Max(2000, first.TimePreempt);

                return 0;
            }
        }
    }
}
