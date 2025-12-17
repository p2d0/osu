using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.MOsu.Database;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Leaderboards;
using osuTK;
using Realms;
using static osu.Game.Screens.SelectV2.BeatmapDetailsArea;

namespace osu.Game.Rulesets.MOsu.UI
{
    public partial class MOsuBeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
        private WedgeSelector<Selection> tabControl = null!;
        private FillFlowContainer leaderboardControls = null!;
        private FillFlowContainer modPresetControls = null!;

        // Buttons
        public Action<string>? SaveCurrentModsAction;
        private SavePresetButton saveModsButton = null!;
        private ShearedButton exportButton = null!;
        private ShearedButton importButton = null!;

        private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
        private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
        private ShearedToggleButton selectedModsToggle = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        // NEW DEPENDENCIES
        [Resolved]
        private MOsuRealmAccess realm { get; set; } = null!;
        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;
        [Resolved]
        private Clipboard clipboard { get; set; } = null!;
        // [Resolved]
        // private OsuGame game { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

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
                            },
                        },
                        // 2. Mod Preset Controls (Mods Tab)
                        modPresetControls = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.X, // Changed to AutoSize to fit buttons tightly
                            Height = 30,
                            Spacing = new Vector2(5f, 0f),
                            Direction = FillDirection.Horizontal,
                            Alpha = 0,
                            Children = new Drawable[]
                            {
                                importButton = new ShearedButton(30f)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Text = "Import",
                                    Height = 30f,
                                    Width = 70f,
                                    Action = importPresets
                                },
                                exportButton = new ShearedButton(30f)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Text = "Export",
                                    Height = 30f,
                                    Width = 70f,
                                    Action = exportPresets
                                },
                                saveModsButton = new SavePresetButton(30f)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Text = "Save New",
                                    Height = 30f,
                                    Margin = new MarginPadding { Left = -9.2f },
                                    Width = 90f,
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

        #region Import / Export Logic

        private void exportPresets()
        {
            var currentHash = beatmap.Value.BeatmapInfo.MD5Hash;

            // 1. Fetch presets for current map
            var presets = realm.Run(r => r.All<BeatmapModPreset>()
                .Filter("BeatmapMD5Hash == $0 && Ruleset.ShortName == $1", currentHash, ruleset.Value.ShortName)
                .Detach() // Detach to allow serialization
                .ToList());

            if (!presets.Any())
            {
                notifications.Post(new SimpleNotification
                {
                    Text = "No presets found for this beatmap to export."
                });
                return;
            }

            // 2. Convert to DTO
            var exportList = presets.Select(p => new PresetExportDto
            {
                Name = p.Name,
                RulesetShortName = p.Ruleset.ShortName,
                Mods = p.Mods.Select(m => new APIMod(m)).ToList()
            }).ToList();

            // 3. Serialize
            try
            {
                string json = JsonConvert.SerializeObject(exportList, Formatting.Indented);
                clipboard.SetText(json);

                // Visual feedback
                exportButton.FlashColour(Colour4.Green, 500);
                notifications.Post(new SimpleNotification
                {
                    Text = $"Copied {exportList.Count} presets to clipboard!"
                });
            }
            catch (Exception ex)
            {
                notifications.Post(new SimpleErrorNotification
                {
                    Text = $"Failed to export: {ex.Message}"
                });
            }
        }

        private void importPresets()
        {
            string json = clipboard.GetText();

            if (string.IsNullOrWhiteSpace(json))
            {
                notifications.Post(new SimpleErrorNotification { Text = "Clipboard is empty." });
                return;
            }

            try
            {
                // 1. Deserialize
                var importedList = JsonConvert.DeserializeObject<List<PresetExportDto>>(json);

                if (importedList == null || !importedList.Any())
                    throw new Exception("No valid presets found in clipboard data.");

                var currentHash = beatmap.Value.BeatmapInfo.MD5Hash;
                int addedCount = 0;

                // 2. Write to Realm
                realm.Write(r =>
                {
                    foreach (var dto in importedList)
                    {
                        // Find ruleset
                        var ruleset = r.Find<RulesetInfo>(dto.RulesetShortName);
                        if (ruleset == null) continue; // Skip if ruleset doesn't exist

                        // Convert APIMods back to Mod instances to ensure compatibility
                        // Then back to APIMod for storage (managed by the Preset object logic)
                        var rulesetInstance = ruleset.CreateInstance();
                        var modList = dto.Mods.Select(m => m.ToMod(rulesetInstance)).ToList();

                        r.Add(new BeatmapModPreset
                        {
                            BeatmapMD5Hash = currentHash, // Assign to CURRENT map
                            Ruleset = ruleset,
                            Name = dto.Name,
                            Mods = modList
                        });
                        addedCount++;
                    }
                });

                importButton.FlashColour(Colour4.Green, 500);
                notifications.Post(new SimpleNotification
                {
                    Text = $"Successfully imported {addedCount} presets!"
                });
            }
            catch (Exception ex)
            {
                notifications.Post(new SimpleErrorNotification
                {
                    Text = "Failed to import presets. Ensure valid JSON is in clipboard.",
                });
                Logger.Error(ex, "Preset Import Failed");
            }
        }

        // DTO for clean JSON export (avoids Realm internal fields)
        private class PresetExportDto
        {
            public string Name { get; set; } = string.Empty;
            public string RulesetShortName { get; set; } = string.Empty;
            public List<APIMod> Mods { get; set; } = new List<APIMod>();
        }

        #endregion

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
