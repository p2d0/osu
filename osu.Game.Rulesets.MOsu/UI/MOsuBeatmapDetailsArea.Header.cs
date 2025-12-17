using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Leaderboards;
using osuTK;
using static osu.Game.Screens.SelectV2.BeatmapDetailsArea;

namespace osu.Game.Rulesets.MOsu.UI
{
    public partial class MOsuBeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
            private WedgeSelector<Selection> tabControl = null!;
            private FillFlowContainer leaderboardControls = null!;

            public Action<string>? SaveCurrentModsAction;
            private SavePresetButton saveModsButton = null!;
            private FillFlowContainer modPresetControls = null!; // New container for Save button

            private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
            private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
            private ShearedToggleButton selectedModsToggle = null!;

            [Resolved]
            private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

            public IBindable<Selection> Type => tabControl.Current;

            public IBindable<BeatmapLeaderboardScope> Scope => scopeDropdown.Current;

            private readonly Bindable<BeatmapDetailTab> configDetailTab = new Bindable<BeatmapDetailTab>();

            public IBindable<LeaderboardSortMode> Sorting => sortDropdown.Current;

            private readonly Bindable<LeaderboardSortMode> configLeaderboardSortMode = new Bindable<LeaderboardSortMode>();

            public IBindable<bool> FilterBySelectedMods => selectedModsToggle.Active;

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = osu.Game.Screens.SelectV2.SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                        Children = new Drawable[]
                        {
                            tabControl = new WedgeSelector<Selection>(20f)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 22,
                                Margin = new MarginPadding { Top = 2f },
                                IsSwitchable = true,
                            },
                            // 1. Leaderboard Controls (Ranking Tab)
                            leaderboardControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 },
                                Children = new Drawable[]
                                {
                                    selectedModsToggle = new ShearedToggleButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Text = UserInterfaceStrings.SelectedMods,
                                        Height = 30f,
                                        Margin = new MarginPadding { Left = -9.2f },
                                    },
                                    sortDropdown = new ShearedDropdown<LeaderboardSortMode>(BeatmapLeaderboardWedgeStrings.Sort)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Items = Enum.GetValues<LeaderboardSortMode>(),
                                    },
                                    scopeDropdown = new ScopeDropdown
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Current = { Value = BeatmapLeaderboardScope.Global },
                                    },
                                    // REMOVED modPresetControls from here
                                },
                            },
                            // 2. Mod Preset Controls (Mods Tab) - MOVED HERE
                            modPresetControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X, // Changed to X to match layout
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 }, // Added padding to avoid overlapping the tabs
                                Alpha = 0,
                                Children = new Drawable[]
                                {
                                    saveModsButton = new SavePresetButton(30f)
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Text = "Save Preset",
                                        Height = 30f,
                                        Margin = new MarginPadding { Left = -9.2f },
                                        Width = 100f,
                                        SaveAction = (name) => SaveCurrentModsAction?.Invoke(name)
                                    }
                                }
                            }
                        },
                    },
                };

                config.BindWith(OsuSetting.BeatmapDetailTab, configDetailTab);
                config.BindWith(OsuSetting.BeatmapLeaderboardSortMode, configLeaderboardSortMode);
                config.BindWith(OsuSetting.BeatmapDetailModsFilter, selectedModsToggle.Active);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                scopeDropdown.Current.Value = tryMapDetailTabToLeaderboardScope(configDetailTab.Value) ?? scopeDropdown.Current.Value;
                scopeDropdown.Current.BindValueChanged(_ => updateConfigDetailTab());

                tabControl.Current.Value = configDetailTab.Value == BeatmapDetailTab.Details ? Selection.Details : Selection.Ranking;
                tabControl.Current.BindValueChanged(v =>
                {
                    modPresetControls.FadeTo(v.NewValue == Selection.Mods ? 1 : 0, 300, Easing.OutQuint);
                    leaderboardControls.FadeTo(v.NewValue == Selection.Ranking ? 1 : 0, 300, Easing.OutQuint);
                    updateConfigDetailTab();
                }, true);

                scopeDropdown.Current.BindValueChanged(scope =>
                {
                    sortDropdown.Current.Disabled = false;

                    if (scope.NewValue == BeatmapLeaderboardScope.Local)
                    {
                        sortDropdown.Current.BindTo(configLeaderboardSortMode);
                    }
                    else
                    {
                        // future implementation when we have web-side support.
                        sortDropdown.Current.UnbindFrom(configLeaderboardSortMode);
                        sortDropdown.Current.Value = LeaderboardSortMode.Score;
                        sortDropdown.Current.Disabled = true;
                    }
                }, true);

                selectedMods.BindValueChanged(mods =>
                {
                    saveModsButton.Enabled.Value = mods.NewValue.Any(m => m.Type != ModType.System);
                }, true);
            }

            #region Reading / writing state from / to configuration

            private void updateConfigDetailTab()
            {
                switch (tabControl.Current.Value)
                {
                    case Selection.Details:
                        configDetailTab.Value = BeatmapDetailTab.Details;
                        return;

                    case Selection.Ranking:
                        configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;
                    case Selection.Mods:
                        // configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(tabControl.Current.Value), tabControl.Current.Value, null);
                }
            }
            private partial class SavePresetButton : ShearedButton, IHasPopover
                    {
                        public Action<string>? SaveAction;

                        public SavePresetButton(float width) : base(width)
                        {
                            // Trigger the popover when clicked
                            Action = this.ShowPopover;
                        }

                        public Popover GetPopover() => new NameEntryPopover(SaveAction);
                    }

                    private partial class NameEntryPopover : OsuPopover
                    {
                        private readonly Action<string>? saveAction;
                        private OsuTextBox nameTextBox = null!;

                        public NameEntryPopover(Action<string>? saveAction)
                        {
                            this.saveAction = saveAction;
                        }

                        [BackgroundDependencyLoader]
                        private void load(OsuColour colours)
                        {
                            Child = new FillFlowContainer
                            {
                                Width = 200,
                                AutoSizeAxes = Axes.Y,
                                Spacing = new Vector2(0, 10),
                                Children = new Drawable[]
                                {
                                    nameTextBox = new OsuTextBox
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        PlaceholderText = "Preset Name (optional)",
                                        TabbableContentContainer = this
                                    },
                                    new ShearedButton(200) // Full width button
                                    {
                                        Text = "Save",
                                        Action = onSave
                                    }
                                }
                            };
                        }

                        protected override void LoadComplete()
                        {
                            base.LoadComplete();
                            Schedule(() => GetContainingFocusManager().ChangeFocus(nameTextBox));

                            // Allow pressing Enter to save
                            nameTextBox.OnCommit += (_, _) => onSave();
                        }

                        private void onSave()
                        {
                            // Use "Untitled" if empty, or allow empty if your logic prefers
                            string name = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "Untitled Preset" : nameTextBox.Text;

                            saveAction?.Invoke(name);
                            this.HidePopover();
                        }
                    }

            private static BeatmapLeaderboardScope? tryMapDetailTabToLeaderboardScope(BeatmapDetailTab tab)
            {
                switch (tab)
                {
                    case BeatmapDetailTab.Local:
                        return BeatmapLeaderboardScope.Local;

                    case BeatmapDetailTab.Country:
                        return BeatmapLeaderboardScope.Country;

                    case BeatmapDetailTab.Global:
                        return BeatmapLeaderboardScope.Global;

                    case BeatmapDetailTab.Friends:
                        return BeatmapLeaderboardScope.Friend;

                    case BeatmapDetailTab.Team:
                        return BeatmapLeaderboardScope.Team;

                    default:
                        return null;
                }
            }

            private static BeatmapDetailTab mapLeaderboardScopeToDetailTab(BeatmapLeaderboardScope scope)
            {
                switch (scope)
                {
                    case BeatmapLeaderboardScope.Local:
                        return BeatmapDetailTab.Local;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapDetailTab.Country;

                    case BeatmapLeaderboardScope.Global:
                        return BeatmapDetailTab.Global;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapDetailTab.Friends;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapDetailTab.Team;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
                }
            }

            #endregion

            public enum Selection
            {
                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Details))]
                Details,

                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Ranking))]
                Ranking,

                Mods,
            }

            private partial class ScopeDropdown : ShearedDropdown<BeatmapLeaderboardScope>
            {
                public ScopeDropdown()
                    : base(BeatmapLeaderboardWedgeStrings.Scope)
                {
                    Items = Enum.GetValues<BeatmapLeaderboardScope>();
                }

                protected override LocalisableString GenerateItemText(BeatmapLeaderboardScope item) => item.GetLocalisableDescription();
            }
        }
    }
}
