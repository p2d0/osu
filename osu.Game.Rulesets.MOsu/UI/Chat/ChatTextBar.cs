// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Rulesets.MOsu.UI.Chat
{
    public partial class ChatTextBar : Container, IHasContextMenu
    {
        public const float HEIGHT = 40;

        public readonly BindableBool ShowSearch = new BindableBool();

        public event Action<string>? OnChatMessageCommitted;

        public event Action<string>? OnSearchTermsChanged;

        public void TextBoxTakeFocus() => chatTextBox.TakeFocus();

        public void TextBoxKillFocus() => chatTextBox.KillFocus();

        [Resolved]
        private Bindable<Channel> currentChannel { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private Bindable<IReadOnlyList<Mod>>? selectedMods { get; set; }

        [Resolved(CanBeNull = true)]
        private Bindable<RulesetInfo>? currentRuleset { get; set; }

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        private Container chattingTextContainer = null!;
        private OsuSpriteText chattingText = null!;
        private Container searchIconContainer = null!;
        private ChatTextBox chatTextBox = null!;

        private const float chatting_text_width = 220;
        private const float search_icon_width = 40;
        private const float padding = 5;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            chattingTextContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = chatting_text_width,
                                Masking = true,
                                Padding = new MarginPadding { Horizontal = padding },
                                Child = chattingText = new TruncatingSpriteText
                                {
                                    MaxWidth = chatting_text_width - padding * 2,
                                    Font = OsuFont.Torus,
                                    Colour = colourProvider.Background1,
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                },
                            },
                            searchIconContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = search_icon_width,
                                Child = new SpriteIcon
                                {
                                    Icon = FontAwesome.Solid.Search,
                                    Origin = Anchor.CentreRight,
                                    Anchor = Anchor.CentreRight,
                                    Size = new Vector2(OsuFont.DEFAULT_FONT_SIZE),
                                    Margin = new MarginPadding { Right = 2 },
                                },
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Right = padding },
                                Child = chatTextBox = new ChatTextBox
                                {
                                    FontSize = OsuFont.DEFAULT_FONT_SIZE,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    ShowSearch = { BindTarget = ShowSearch },
                                    HoldFocus = true,
                                    ReleaseFocusOnCommit = false,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            chatTextBox.Current.ValueChanged += chatTextBoxChange;
            chatTextBox.OnCommit += chatTextBoxCommit;

            ShowSearch.BindValueChanged(change =>
            {
                bool showSearch = change.NewValue;

                chattingTextContainer.FadeTo(showSearch ? 0 : 1);
                searchIconContainer.FadeTo(showSearch ? 1 : 0);

                if (showSearch)
                    OnSearchTermsChanged?.Invoke(chatTextBox.Current.Value);
            }, true);

            currentChannel.BindValueChanged(change =>
            {
                Channel newChannel = change.NewValue;

                switch (newChannel?.Type)
                {
                    case null:
                        chattingText.Text = string.Empty;
                        break;

                    case ChannelType.PM:
                        chattingText.Text = ChatStrings.TalkingWith(newChannel.Name);
                        break;

                    default:
                        chattingText.Text = ChatStrings.TalkingIn(newChannel.Name);
                        break;
                }

                if (change.OldValue != null)
                    chatTextBox.Current.UnbindFrom(change.OldValue.TextBoxMessage);

                if (newChannel != null)
                {
                    // change length limit first before binding to avoid accidentally truncating pending message from new channel.
                    chatTextBox.LengthLimit = newChannel.MessageLengthLimit;
                    chatTextBox.Current.BindTo(newChannel.TextBoxMessage);
                }
            }, true);
        }

        private void chatTextBoxChange(ValueChangedEvent<string> change)
        {
            if (ShowSearch.Value)
                OnSearchTermsChanged?.Invoke(change.NewValue);
        }

        private void chatTextBoxCommit(TextBox sender, bool newText)
        {
            if (ShowSearch.Value)
                return;

            OnChatMessageCommitted?.Invoke(sender.Text);
            sender.Text = string.Empty;
        }

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

            // Serialize to compact JSON
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
