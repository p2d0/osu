// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions; // For Popover extensions
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.MOsu.Configuration;
using osu.Game.Rulesets.UI;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Localisation;
using osuTK;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.Sprites;
using System.Linq;
using System.Linq.Expressions;
using Realms;
using osu.Game.Screens; // Required for IPerformFromScreenRunner
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Localisation;
using osu.Game.Screens;
using osu.Game.Screens.Import;
using osu.Game.Screens.Utility;
using osu.Game.Rulesets.MOsu.UI.LocalUser;
using osu.Game.Online.API;
using osu.Framework.Testing;
using osu.Game.Rulesets.MOsu.Extensions;
using osu.Game.Rulesets.MOsu.UI.Toolbar;

namespace osu.Game.Rulesets.MOsu.UI
{
    public partial class OsuSettingsSubsection : RulesetSettingsSubsection
    {
        private Ruleset ruleset;

        protected override LocalisableString Header => "osu!";

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IPerformFromScreenRunner? performer { get; set; }

        public OsuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
            this.ruleset = ruleset;
        }
        private LocalUserProfileOverlay? localUserProfileOverlay;
        private ChangelogOverlay? changelogOverlay;
        // private ToolbarLocalUserButton? localUserButton;

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api, OsuGame game,GameHost host)
        {
            // LocalUserManager localUserManager;
            // host.Dependencies.Cache(localUserManager = new LocalUserManager((OsuRuleset)ruleset, realm, api));

            // game.GetToolbarContainer().Add(new ToolbarLocalUserButton());

            var config = (OsuRulesetConfigManager)Config;
            Children = new Drawable[]
            {
                new SettingsCheckbox
                {
                    LabelText = RulesetSettingsStrings.SnakingInSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingInSliders)
                },
                new SettingsCheckbox
                {
                    ClassicDefault = false,
                    LabelText = RulesetSettingsStrings.SnakingOutSliders,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.SnakingOutSliders)
                },
                new SettingsCheckbox
                {
                    LabelText = RulesetSettingsStrings.CursorTrail,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorTrail)
                },
                new SettingsCheckbox
                {
                    LabelText = RulesetSettingsStrings.CursorRipples,
                    Current = config.GetBindable<bool>(OsuRulesetSetting.ShowCursorRipples)
                },
                new SettingsEnumDropdown<PlayfieldBorderStyle>
                {
                    LabelText = RulesetSettingsStrings.PlayfieldBorderStyle,
                    Current = config.GetBindable<PlayfieldBorderStyle>(OsuRulesetSetting.PlayfieldBorderStyle),
                },
                // --- Preset Export ---
                new SettingsButton
                {
                    Text = "Export presets to file",
                    TooltipText = "Saves all mosususu presets to exports/osu_mod_presets.json",
                    Action = exportPresets
                },

                // --- Preset Import (Popup) ---
                new ImportPresetButton(),
                new SettingsButton
                {
                    Text = "Import presets from file",
                    TooltipText = "Select a .json file from your computer",
                    Action = () =>
                    {
                        performer?.PerformFromScreen(screen => screen.Push(new ModPresetFileImportScreen()));
                    }
                }
            };
        }

        private void exportPresets()
        {
            try
            {
                // FIX: Use .Filter() string query to handle relationships (Ruleset.ShortName)
                // and persisted properties directly without LINQ translation issues.
                var presets = realm.Run(r => r.All<ModPreset>()
                    .Filter("Ruleset.ShortName == $0 && DeletePending == false", "mosususu")
                    .ToList());

                if (presets.Count == 0)
                {
                    notifications?.Post(new SimpleNotification { Text = "No mosususu presets found to export." });
                    return;
                }

                var transferObjects = presets.Select(p => new ModPresetTransferObject
                {
                    Name = p.Name,
                    Description = p.Description,
                    ModsJson = p.ModsJson
                }).ToList();

                string json = JsonConvert.SerializeObject(transferObjects, Formatting.Indented);

                var exportStorage = storage.GetStorageForDirectory("exports");
                const string filename = "osu_mod_presets.json";

                using (var stream = exportStorage.CreateFileSafely(filename))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }

                notifications?.Post(new SimpleNotification { Text = $"Exported {presets.Count} presets to {filename}!" });
                exportStorage.PresentFileExternally(filename);
            }
            catch (Exception ex)
            {
                notifications?.Post(new SimpleErrorNotification { Text = $"Export failed: {ex.Message}" });
            }
        }
    }

    // --------------------------------------------------------
    // CUSTOM BUTTON & POPOVER FOR IMPORT
    // --------------------------------------------------------

    public partial class ImportPresetButton : SettingsButton, IHasPopover
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Text = "Import presets (Paste JSON)";
            TooltipText = "Click to paste preset JSON data";
            Action = this.ShowPopover;
        }

        public Popover GetPopover() => new ImportPresetPopover();
    }

public partial class ImportPresetPopover : OsuPopover
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        private readonly FocusedTextBox textBox;

        public ImportPresetPopover()
        {
            AutoSizeAxes = Axes.Both;
            Origin = Anchor.TopCentre;

            RoundedButton importButton;

            Child = new FillFlowContainer
            {
                Direction = FillDirection.Vertical,
                AutoSizeAxes = Axes.Y,
                Width = 300,
                Spacing = new Vector2(10f),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = "Paste JSON below:",
                        Font = OsuFont.GetFont(weight: FontWeight.Bold),
                    },
                    textBox = new FocusedTextBox
                    {
                        PlaceholderText = @"[{ ""Name"": ""..."" ... }]",
                        Height = 100,
                        RelativeSizeAxes = Axes.X,
                        SelectAllOnFocus = true,
                        // FIX: Increase character limit (default is often too low)
                        LengthLimit = 1000000,
                    },
                    importButton = new RoundedButton
                    {
                        Height = 40,
                        RelativeSizeAxes = Axes.X,
                        Text = "Import",
                    }
                }
            };

            importButton.Action += import;
            textBox.OnCommit += (_, _) => import();
        }

        protected override void PopIn()
        {
            base.PopIn();
            textBox.TakeFocus();
        }

        private void import()
        {
            string json = textBox.Text;

            if (string.IsNullOrWhiteSpace(json))
            {
                this.HidePopover();
                return;
            }

            try
            {
                var transferObjects = JsonConvert.DeserializeObject<List<ModPresetTransferObject>>(json);

                if (transferObjects == null || transferObjects.Count == 0)
                {
                    notifications?.Post(new SimpleErrorNotification { Text = "Invalid JSON or no presets found." });
                    return;
                }

                int importedCount = 0;

                realm.Write(r =>
                {
                    var osuRulesetInfo = r.Find<RulesetInfo>("mosususu");

                    if (osuRulesetInfo == null)
                        throw new InvalidOperationException("Could not find mosususu ruleset in database.");

                    foreach (var dto in transferObjects)
                    {
                        bool exists = r.All<ModPreset>()
                            .Filter("Name == $0 && Ruleset.ShortName == $1 && DeletePending == false", dto.Name, "mosususu")
                            .Count() > 0;

                        if (exists) continue;

                        r.Add(new ModPreset
                        {
                            ID = Guid.NewGuid(),
                            Name = dto.Name,
                            Description = dto.Description,
                            ModsJson = dto.ModsJson,
                            Ruleset = osuRulesetInfo,
                            DeletePending = false
                        });
                        importedCount++;
                    }
                });

                if (importedCount > 0)
                    notifications?.Post(new SimpleNotification { Text = $"Successfully imported {importedCount} presets!" });
                else
                    notifications?.Post(new SimpleNotification { Text = "No new presets imported (duplicates skipped)." });

                this.HidePopover();
            }
            catch (JsonException)
            {
                notifications?.Post(new SimpleErrorNotification { Text = "Failed to parse JSON. Check format." });
            }
            catch (Exception ex)
            {
                notifications?.Post(new SimpleErrorNotification { Text = $"Import failed: {ex.Message}" });
            }
        }
    }
}
