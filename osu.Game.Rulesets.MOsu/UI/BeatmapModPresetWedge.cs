using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.MOsu.Database; // Ensure this matches your model namespace
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Rulesets.MOsu.UI
{

    public partial class BeatmapModPresetWedge : VisibilityContainer
    {
        [Resolved]
        private MOsuRealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private FillFlowContainer<BeatmapModPresetPanel> presetFlow = null!;
        private IDisposable? realmSubscription;

        protected override bool StartHidden => false;

        public BeatmapModPresetWedge()
        {
            RelativeSizeAxes = Axes.Both;
            Padding = new MarginPadding { Bottom = 20, Left = 20};
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            // 1. OsuContextMenuContainer handles the Right-Click menu logic
            InternalChild = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // 2. The Background stays OUTSIDE the shear correction.
                    // It inherits the parent's negative shear, giving it the "Wedge" shape.
                    new WedgeBackground(),

                    // 3. The Content gets sheared positively to appear straight to the user.
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 15, Bottom = 15, Left = 20, Right = 10 }, // Adjusted Left padding for shear slant
                        Shear = OsuGame.SHEAR,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 10),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = "Saved Mod Presets",
                                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 16),
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                    },
                                    new OsuScrollContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ScrollbarVisible = false,
                                        Child = presetFlow = new FillFlowContainer<BeatmapModPresetPanel>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 5),
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(_ => updateSubscription(), true);
        }

        public void SaveCurrentMods()
        {
            var modsToSave = selectedMods.Value.Where(m => m.Type != ModType.System).ToList();

            if (!modsToSave.Any()) return;

            string currentHash = beatmap.Value.BeatmapInfo.MD5Hash;
            var ruleset = beatmap.Value.BeatmapInfo.Ruleset;

            realm.Write(r =>
            {
                var dbRuleset = r.Find<RulesetInfo>(ruleset.ShortName) ?? ruleset;

                r.Add(new BeatmapModPreset
                {
                    BeatmapMD5Hash = currentHash,
                    Ruleset = dbRuleset,
                    Mods = modsToSave
                });
            });

            presetFlow.FlashColour(Colour4.White, 300);
        }

        private void updateSubscription()
        {
            realmSubscription?.Dispose();
            presetFlow.Clear();

            if (beatmap.Value == null)
                return;

            string currentHash = beatmap.Value.BeatmapInfo.MD5Hash;

            Schedule(() => {
                realmSubscription = realm.RegisterForNotifications(
                    r => r.All<BeatmapModPreset>().Where(p => p.BeatmapMD5Hash == currentHash),
                    (sender, changes) =>
                    {
                        if (changes == null)
                        {
                            foreach (var p in sender) addPanel(p);
                            return;
                        }

                        foreach (var i in changes.DeletedIndices.OrderByDescending(i => i))
                        {
                            if (i < presetFlow.Count) presetFlow.Remove(presetFlow.Children[i], true);
                        }

                        foreach (var i in changes.InsertedIndices)
                        {
                            if (i <= presetFlow.Count) addPanel(sender[i]);
                        }

                        foreach (var i in changes.ModifiedIndices)
                        {
                            if (i < presetFlow.Count) presetFlow.Children[i].FlashColour(Colour4.White, 500);
                        }
                    }
                );
            });
        }

        private void addPanel(BeatmapModPreset preset)
        {
            presetFlow.Add(new BeatmapModPresetPanel(preset));
        }

        protected override void PopIn() => this.FadeIn(300, Easing.OutQuint).MoveToX(0, 300, Easing.OutQuint);
        protected override void PopOut() => this.FadeOut(300, Easing.OutQuint).MoveToX(0, 300, Easing.OutQuint);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            realmSubscription?.Dispose();
        }
    }

    public partial class BeatmapModPresetPanel : OsuClickableContainer, IHasContextMenu
    {
    private readonly BeatmapModPreset preset;
    private readonly BindableBool active = new BindableBool();

    [Resolved]
    private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

    [Resolved]
    private MOsuRealmAccess realm { get; set; } = null!;

    [Resolved]
    private OverlayColourProvider colourProvider { get; set; } = null!;

    // 1. Add Dependency for calculating stars
    [Resolved]
    private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

    // 2. Add Dependency for the current beatmap info
    [Resolved]
    private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

    private Box background = null!;
    private StarRatingDisplay starRatingDisplay = null!;
    private CancellationTokenSource? cancellationTokenSource;

    public BeatmapModPresetPanel(BeatmapModPreset preset)
    {
        this.preset = preset;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        Height = 50;
        CornerRadius = 5;
        Masking = true;

        var mods = preset.Mods.ToList();

        Children = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colourProvider.Background4,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 10 },
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8), // Spacing between Stars and Mods
                                Children = new Drawable[]
                                {
                                    // 3. Add the Star Rating Display
                                    starRatingDisplay = new StarRatingDisplay(default, size: StarRatingDisplaySize.Small)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Scale = new Vector2(0.8f), // Adjust scale to fit 50px height comfortably
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(2),
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        ChildrenEnumerable = mods.Select(m => new ModIcon(m)
                                        {
                                            Scale = new Vector2(0.5f),
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        })
                                    }
                                }
                            }
                        }
                    }
                }
            },
            new HoverLayer()
        };

        Action = toggleSelection;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        selectedMods.BindValueChanged(_ => updateActiveState(), true);
        active.BindValueChanged(val =>
        {
            background.FadeColour(val.NewValue ? colourProvider.Content2 : colourProvider.Background4, 200);
        }, true);

        cancellationTokenSource = new CancellationTokenSource();

        // --- FIX START ---
        // 1. Access properties on the UI thread.
        var beatmapInfo = beatmap.Value.BeatmapInfo;
        var presetRuleset = preset.Ruleset ?? beatmapInfo.Ruleset;

        // 2. Detach them from Realm to create thread-safe unmanaged copies.
        //    'Detach()' requires 'using osu.Game.Database;'
        var safeBeatmapInfo = osu.Game.Database.RealmObjectExtensions.Detach(beatmapInfo);
        var safeRulesetInfo = osu.Game.Rulesets.MOsu.Database.MOsuRealmExtensions.Detach(presetRuleset);

        // 3. Materialize the mods list on the UI thread to ensure we don't access Realm lists in the background.
        var safeMods = preset.Mods.ToList();
        // --- FIX END ---

        // Pass the safe, detached objects to the async method
        difficultyCache.GetDifficultyAsync(safeBeatmapInfo, safeRulesetInfo, safeMods, cancellationTokenSource.Token)
            .ContinueWith(task => Schedule(() =>
            {
                // Handle potential cancellation or errors safely
                if (task.IsCanceled) return;

                var difficulty = task.GetResultSafely();

                if (difficulty != null)
                {
                    starRatingDisplay.Current.Value = difficulty.Value;
                }
            }), cancellationTokenSource.Token);
    }
    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        cancellationTokenSource?.Cancel();
    }
        private void toggleSelection()
        {
            if (active.Value)
            {
                // If already active, deselect these mods
                // We remove the preset's mods from the current selection
                // Note: This relies on Mod equality.
                var newMods = selectedMods.Value.Where(m => !preset.Mods.Any(pm => pm.Equals(m)));
                selectedMods.Value = newMods.ToArray();
            }
            else
            {
                // If inactive, activate these mods
                // Logic: Keep System mods (unless incompatible), replace others with Preset mods.

                var presetMods = preset.Mods;

                // Filter currently selected system mods
                var systemMods = selectedMods.Value.Where(mod => mod.Type == ModType.System);

                // Check for incompatibility: If a system mod is incompatible with a preset mod, drop the system mod
                var compatibleSystemMods = systemMods.Where(sm =>
                    !sm.IncompatibleMods.Any(t => presetMods.Any(pm => t.IsInstanceOfType(pm))));

                selectedMods.Value = presetMods.Concat(compatibleSystemMods).ToArray();
            }
        }

        private void updateActiveState()
        {
            // Check if the current selection matches the preset
            // We ignore System mods in the comparison (e.g., if you have TouchDevice enabled, the preset for HR+DT is still "Active")

            var currentNonSystem = selectedMods.Value.Where(m => m.Type != ModType.System);
            var presetMods = preset.Mods;

            active.Value = new HashSet<Mod>(presetMods).SetEquals(currentNonSystem);
        }

        public MenuItem[] ContextMenuItems => new MenuItem[]
        {
            new OsuMenuItem("Delete Preset", MenuItemType.Destructive, () =>
            {
                // Delete from Realm
                // We need to capture the ID or the object reference safely
                realm.Write(r =>
                {
                    // Ensure the object is managed before deleting
                    if (preset.IsManaged)
                        r.Remove(preset);
                });
            })
        };

        private partial class HoverLayer : Box
        {
            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;
                Colour = Colour4.White;
                Alpha = 0;
                Blending = BlendingParameters.Additive;
            }

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                this.FadeTo(0.1f, 200);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
            {
                this.FadeOut(200);
                base.OnHoverLost(e);
            }
        }
    }
}
