using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.MOsu.Database;
using osu.Game.Rulesets.MOsu.UI;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Tests.Visual.SongSelectV2;
using osuTK;

namespace osu.Game.Rulesets.MOsu.Tests
{
    [TestFixture]
    public partial class TestSceneBeatmapModPresetWedge : SongSelectComponentsTestScene
    {
        private BeatmapModPresetWedge wedge = null!;
        protected MOsuRealmAccess MOsuRealm { get; set; } = null!;

        [Cached]
        private readonly Bindable<IReadOnlyList<Mod>> selectedMods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            Dependencies.Cache(MOsuRealm = new MOsuRealmAccess(LocalStorage, "mosurealm", host.UpdateThread));
            Dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Green));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(wedge = new BeatmapModPresetWedge
            {
                // FIX: Use relative Size (0.5 = 50% width/height)
                // Do NOT set Height = 400 if RelativeSizeAxes is Both/Y
                Size = new Vector2(400,400),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                State = { Value = Visibility.Visible }
            });
        }

        [SetUp]
        public void SetUp()
        {
            MOsuRealm.Write(r => r.RemoveAll<BeatmapModPreset>());
        }

        [Test]
        public void TestPresetDisplay()
        {
            string beatmapHash = Guid.NewGuid().ToString();

            AddStep("create beatmap with presets", () =>
            {
                var working = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                working.BeatmapInfo.MD5Hash = beatmapHash;
                working.Beatmap.BeatmapInfo.MD5Hash = beatmapHash;

                MOsuRealm.Write(r =>
                {
                    r.Add(new BeatmapModPreset
                    {
                        BeatmapMD5Hash = beatmapHash,
                        Ruleset = working.BeatmapInfo.Ruleset,
                        Mods = new Mod[] { new OsuModDoubleTime(), new OsuModHardRock() }
                    });

                    r.Add(new BeatmapModPreset
                    {
                        BeatmapMD5Hash = beatmapHash,
                        Ruleset = working.BeatmapInfo.Ruleset,
                        Mods = new Mod[] { new OsuModHidden() }
                    });
                });

                Beatmap.Value = working;
            });

            AddUntilStep("presets displayed", () => wedge.ChildrenOfType<ModPresetPanel>().Any());
            AddAssert("2 presets loaded", () => wedge.ChildrenOfType<ModPresetPanel>().Count() == 2);
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("hide wedge", () => wedge.Hide());
            AddAssert("wedge hidden", () => wedge.State.Value == Visibility.Hidden);
            AddStep("show wedge", () => wedge.Show());
            AddAssert("wedge visible", () => wedge.State.Value == Visibility.Visible);
        }
    }
}
