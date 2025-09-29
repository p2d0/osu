// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Logging;
using osu.Game.Users;
using osu.Game.Rulesets;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarUserButton : ToolbarOverlayToggleButton
    {
        private UpdateableAvatar avatar = null!;

        private IBindable<APIUser> localUser = null!;

        private OsuSpriteText pp = null!;

        private LoadingSpinner spinner = null!;

        private SpriteIcon failingIcon = null!;

        private IBindable<APIState> apiState = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        // public Bindable<ScoreBasedUserStatisticsUpdate?> LatestUpdate { get; } = new Bindable<ScoreBasedUserStatisticsUpdate?>();

        public ToolbarUserButton()
        {
            ButtonContent.AutoSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, IAPIProvider api, LoginOverlay? login)
        {

            Flow.Add(new Container
            {
                Masking = true,
                CornerRadius = 4,
                Size = new Vector2(64),
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4,
                    Colour = Color4.Black.Opacity(0.1f),
                },
                Children = new Drawable[]
                {
                    pp = new OsuSpriteText {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight
                        // RelativeSizeAxes = Axes.Both,
                    },
                    // avatar = new UpdateableAvatar(isInteractive: false)
                    // {
                    //     RelativeSizeAxes = Axes.Both,
                    // },
                    spinner = new LoadingLayer(dimBackground: true, withBox: false, blockInput: false)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                    },
                    failingIcon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                        Size = new Vector2(0.3f),
                        Icon = FontAwesome.Solid.ExclamationTriangle,
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.YellowLight,
                    },
                }
            });

            Flow.Add(new TransientUserStatisticsUpdateDisplay
            {
                Alpha = 0
            });
            Flow.AutoSizeEasing = Easing.OutQuint;
            Flow.AutoSizeDuration = 250;

            apiState = api.State.GetBoundCopy();
            apiState.BindValueChanged(onlineStateChanged, true);

            localUser = api.LocalUser.GetBoundCopy();
            localUser.BindValueChanged(userChanged, true);
            StateContainer = login;

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            ruleset.BindValueChanged(_ => updateDisplay(), true);

        }

        private void onStatisticsUpdated(UserStatisticsUpdate update)
        {
            if (update.Ruleset.Equals(ruleset.Value))
                updateDisplay();
        }

        private void updateDisplay()
        {
            var statistics = statisticsProvider?.GetStatisticsFor(ruleset.Value);

            pp.Text = ((int)Math.Abs(statistics?.PP ?? 0M)).ToString() + "PP";
        }

        private void userChanged(ValueChangedEvent<APIUser> user) => Schedule(() =>
        {
            Text = user.NewValue.Username;
            // pp.Text = ((int)Math.Abs(user.NewValue.Statistics.PP ?? 0M)).ToString() + "PP";
            // avatar.User = user.NewValue;
        });

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // LatestUpdate.BindValueChanged(val =>
            // {
            //     Logger.Log("Nice one");
            //     pp.Text = ((int)Math.Abs(val.NewValue.After.PP ?? 0M)).ToString() + "PP";
            // });
        }


        private void onlineStateChanged(ValueChangedEvent<APIState> state) => Schedule(() =>
        {
            failingIcon.FadeTo(state.NewValue == APIState.Failing || state.NewValue == APIState.RequiresSecondFactorAuth ? 1 : 0, 200, Easing.OutQuint);

            switch (state.NewValue)
            {
                case APIState.Connecting:
                    TooltipText = ToolbarStrings.Connecting;
                    spinner.Show();
                    break;

                case APIState.Failing:
                    TooltipText = ToolbarStrings.AttemptingToReconnect;
                    spinner.Show();
                    failingIcon.Icon = FontAwesome.Solid.ExclamationTriangle;
                    break;

                case APIState.RequiresSecondFactorAuth:
                    TooltipText = ToolbarStrings.VerificationRequired;
                    spinner.Show();
                    failingIcon.Icon = FontAwesome.Solid.Key;
                    break;

                case APIState.Offline:
                case APIState.Online:
                    TooltipText = string.Empty;
                    spinner.Hide();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state.NewValue));
            }
        });
    }
}
