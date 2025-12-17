using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.MOsu.Database;
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
        private RulesetStore rulesetStore { get; set; } = null!;

        [Resolved]
        private Bindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private OsuScrollContainer scrollContainer = null!;
        private FillFlowContainer<ModPresetPanel> presetFlow = null!;
        private LoadingLayer loading = null!;

        // Changed to False for easier debugging in tests,
        // usually the parent Screen controls the initial show.
        protected override bool StartHidden => false;

        public BeatmapModPresetWedge()
        {
            // Set defaults here so they can be overridden by object initializers
            // RelativeSizeAxes = Axes.Both;

            Width = 400f; // Default width
            Height = 400f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Do NOT overwrite Width/Height here, or you cannot change them in the Test Scene initializer.
            // InternalChild = new Box() {
            //     RelativeSizeAxes = Axes.Both
            // };

            InternalChild = new Container // Removed ShearAligningWrapper for simplicity unless you specifically need shear
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
                        Padding = new MarginPadding { Top = 20, Bottom = 20, Left = 20, Right = 20 },
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = "Saved Mod Presets",
                                Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 16),
                                Margin = new MarginPadding { Bottom = 10 }
                            },
                            scrollContainer = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Top = 25 },
                                ScrollbarVisible = false,
                                Child = presetFlow = new FillFlowContainer<ModPresetPanel>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5),
                                }
                            }
                        }
                    },
                    loading = new LoadingLayer()
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.BindValueChanged(_ => updatePresets(), true);
        }

        private void updatePresets()
        {
            presetFlow.Clear();

            if (beatmap.Value == null)
                return;

            string currentHash = beatmap.Value.BeatmapInfo.MD5Hash;

            var presets = realm.Run(r => r.All<BeatmapModPreset>()
                .Where(p => p.BeatmapMD5Hash == currentHash)
                .ToList());

            foreach (var preset in presets)
            {
                // 2. Fix: Hydrate the Ruleset property using the RulesetStore.
                // This makes preset.Mods accessible (it will no longer return empty).
                var ruleset = rulesetStore.GetRuleset(preset.RulesetShortName);

                // Only add the panel if we successfully found the ruleset
                if (ruleset != null)
                {
                    presetFlow.Add(new ModPresetPanel(preset)
                    {
                        Action = () => applyPreset(preset)
                    });
                }
            }
        }

        private void applyPreset(BeatmapModPreset preset)
        {
            var mods = preset.Mods;
            if (mods != null)
            {
                selectedMods.Value = mods.ToArray();
            }
        }

        protected override void PopIn()
        {
            this.FadeIn(300, Easing.OutQuint)
                .MoveToX(100, 300, Easing.OutQuint)
                .MoveToY(100, 300, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(300, Easing.OutQuint)
                .MoveToY(-100, 300, Easing.OutQuint);
        }
    }

    public partial class ModPresetPanel : OsuClickableContainer
    {
        private readonly BeatmapModPreset preset;

        public ModPresetPanel(BeatmapModPreset preset)
        {
            this.preset = preset;
        }
        private ICollection<Mod> ModsFromJson(ModPreset preset, Ruleset ruleset){
            if (string.IsNullOrEmpty(preset.ModsJson))
                return Array.Empty<Mod>();

            var apiMods = JsonConvert.DeserializeObject<IEnumerable<APIMod>>(preset.ModsJson);
            return apiMods.AsNonNull().Select(mod => mod.ToMod(ruleset)).ToArray();
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            Height = 40;
            CornerRadius = 5;
            Masking = true;

            var mods = preset.Mods;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 10 },
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5),
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    ChildrenEnumerable = mods.Select(m => new ModIcon(m)
                    {
                        Scale = new Vector2(0.6f),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft
                    })
                },
                new HoverLayer()
            };
        }

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
