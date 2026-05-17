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
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.MOsu.UI.Chat
{
    public partial class MOsuChatTextBar : osu.Game.Overlays.Chat.ChatTextBar, IHasContextMenu
    {
        [Resolved(CanBeNull = true)]
        private Bindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        [Resolved(CanBeNull = true)]
        private Bindable<RulesetInfo>? currentRuleset { get; set; }

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                if (selectedMods == null || currentRuleset == null || channelManager == null)
                    return Array.Empty<MenuItem>();

                return new MenuItem[]
                {
                    new OsuMenuItem("Share Current Mods", MenuItemType.Standard, sendCurrentMods)
                };
            }
        }

        private void sendCurrentMods()
        {
            if (selectedMods == null || currentRuleset == null || channelManager == null) return;

            var mods = selectedMods.Value.Where(m => m.Type != ModType.System).ToList();

            var exportData = new List<PresetExportDto>
            {
                new PresetExportDto
                {
                    Name = "My Mods",
                    RulesetShortName = currentRuleset.Value.ShortName,
                    Mods = mods.Select(m => new APIMod(m)).ToList()
                }
            };

            string json = JsonConvert.SerializeObject(exportData, Formatting.None);
            channelManager.PostMessage(json);
        }

        private class PresetExportDto
        {
            public string Name { get; set; } = string.Empty;
            public string RulesetShortName { get; set; } = string.Empty;
            public List<APIMod> Mods { get; set; } = new List<APIMod>();
        }
    }
}
