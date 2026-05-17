// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.MOsu.UI.Chat
{
    public partial class MOsuChatLine : osu.Game.Overlays.Chat.ChatLine, IHasContextMenu
    {
        public MOsuChatLine(Message message) : base(message) { }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private Bindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        [Resolved(CanBeNull = true)]
        private Bindable<RulesetInfo>? currentRuleset { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                if (selectedMods == null || currentRuleset == null)
                    return Array.Empty<MenuItem>();

                string content = Message.Content;
                if (string.IsNullOrWhiteSpace(content))
                    return Array.Empty<MenuItem>();

                content = content.Trim();
                if (!content.StartsWith("[") || !content.EndsWith("]"))
                    return Array.Empty<MenuItem>();

                try
                {
                    var presets = JsonConvert.DeserializeObject<List<PresetExportDto>>(content);
                    if (presets != null && presets.Count > 0)
                    {
                        return new MenuItem[]
                        {
                            new OsuMenuItem("Apply Mod Preset", MenuItemType.Highlighted, () => applyPreset(presets.First()))
                        };
                    }
                }
                catch
                {
                    // Ignore parsing failures
                }

                return Array.Empty<MenuItem>();
            }
        }

        private void applyPreset(PresetExportDto preset)
        {
            if (selectedMods == null || currentRuleset == null) return;

            if (preset.RulesetShortName != currentRuleset.Value.ShortName)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = $"Preset is for {preset.RulesetShortName}, but you are playing {currentRuleset.Value.ShortName}."
                });
                return;
            }

            try
            {
                var rulesetInstance = currentRuleset.Value.CreateInstance();
                var mods = preset.Mods.Select(m => m.ToMod(rulesetInstance)).ToList();

                selectedMods.Value = mods;

                notifications?.Post(new SimpleNotification
                {
                    Text = $"Applied preset: {preset.Name}"
                });
            }
            catch (Exception ex)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = $"Failed to apply preset: {ex.Message}"
                });
            }
        }

        private class PresetExportDto
        {
            public string Name { get; set; } = string.Empty;
            public string RulesetShortName { get; set; } = string.Empty;
            public List<APIMod> Mods { get; set; } = new List<APIMod>();
        }
    }
}
