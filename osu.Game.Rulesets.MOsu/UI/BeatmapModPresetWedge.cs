using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
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

        // Button to save current state
        private Button saveButton = null!;

        protected override bool StartHidden => false;

        public BeatmapModPresetWedge()
        {
            Width = 300f;
            Height = 300f; // Adjusted height for visibility
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = new ShearAligningWrapper(new Container
            {
                RelativeSizeAxes = Axes.Both,
                CornerRadius = 10,
                Masking = true,
                Children = new Drawable[]
                {
                    new WedgeBackground(),
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 20, Bottom = 20, Left = 10, Right = 10 },
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
                                        Height = 0.8f, // Leave room for the button
                                        ScrollbarVisible = false,
                                        Child = presetFlow = new FillFlowContainer<BeatmapModPresetPanel>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            // AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 5),
                                        }
                                    },
                                    saveButton = new RoundedButton
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 40,
                                        Text = "Save Current Mods",
                                        Action = saveCurrentMods
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(_ => updateSubscription(), true);

            // Enable/Disable button based on if mods are selected
            selectedMods.BindValueChanged(mods =>
            {
                saveButton.Enabled.Value = mods.NewValue.Any(m => m.Type != ModType.System);
            }, true);
        }

        private void updateSubscription()
        {
            realmSubscription?.Dispose();
            presetFlow.Clear();

            if (beatmap.Value == null)
                return;

            string currentHash = beatmap.Value.BeatmapInfo.MD5Hash;

            // Subscribe to live updates from Realm
            // var presets = realm.Realm.All<BeatmapModPreset>()
            //     .Where(p => p.BeatmapMD5Hash == currentHash);
            Schedule(() => {
                realmSubscription = realm.RegisterForNotifications(
                    r => r.All<BeatmapModPreset>().Where(p => p.BeatmapMD5Hash == currentHash),
                    (sender, changes) =>
                    {
                        if (changes == null)
                        {
                            // Initial population
                            foreach (var p in sender)
                                addPanel(p);
                            return;
                        }

                        // Handle deletions (reverse order to preserve indices)
                        foreach (var i in changes.DeletedIndices.OrderByDescending(i => i))
                        {
                            if (i < presetFlow.Count)
                                presetFlow.Remove(presetFlow.Children[i], true);
                        }

                        // Handle insertions
                        foreach (var i in changes.InsertedIndices)
                        {
                            if (i <= presetFlow.Count)
                                addPanel(sender[i]);
                        }

                        // Handle modifications (flash to indicate change)
                        foreach (var i in changes.ModifiedIndices)
                        {
                            if (i < presetFlow.Count)
                                presetFlow.Children[i].FlashColour(Colour4.White, 500);
                        }
                    }
                );
            });
        }

        private void addPanel(BeatmapModPreset preset)
        {
            // Realm objects must be accessed on the thread they were created (Update thread usually),
            // or valid only within the subscription block.
            // Since UI updates happen on Update thread, this is generally safe,
            // but creating a detached copy or passing the ID is sometimes safer depending on architecture.
            // Here we pass the object directly as is common in Osu Game Logic.

            presetFlow.Add(new BeatmapModPresetPanel(preset));
        }

        private void saveCurrentMods()
        {
            var modsToSave = selectedMods.Value.Where(m => m.Type != ModType.System).ToList();

            if (!modsToSave.Any()) return;

            string currentHash = beatmap.Value.BeatmapInfo.MD5Hash;
            var ruleset = beatmap.Value.BeatmapInfo.Ruleset;

            // Generate a simple name based on Acronyms
            string name = string.Join(" + ", modsToSave.Select(m => m.Acronym));

            realm.Write(r =>
            {
                // Ensure we have a managed version of the ruleset info
                var dbRuleset = r.Find<RulesetInfo>(ruleset.ShortName) ?? ruleset;

                r.Add(new BeatmapModPreset
                {
                    BeatmapMD5Hash = currentHash,
                    // Name = name,
                    // Description = $"{modsToSave.Count} mods",
                    Ruleset = dbRuleset,
                    Mods = modsToSave
                });
            });
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

        private Box background = null!;

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

            // Safe access to mods list
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
                                    Spacing = new Vector2(2),
                                    ChildrenEnumerable = mods.Select(m => new ModIcon(m)
                                    {
                                        Scale = new Vector2(0.5f), // Smaller icons
                                        // Size = new Vector2(100),   // Base size of ModIcon is usually large
                                    })
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

            // Watch for external mod changes to update the "Active" visual state
            selectedMods.BindValueChanged(_ => updateActiveState(), true);
            active.BindValueChanged(val =>
            {
                background.FadeColour(val.NewValue ? colourProvider.Content2 : colourProvider.Background4, 200);
            }, true);
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
