using System.Collections;
using System.Linq;
using System.Reflection;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.MOsu.Extensions;

namespace osu.Game.Rulesets.MOsu.UI.Chat
{
    public partial class ChatOverlayInjector : Component
    {
        [Resolved]
        private OsuGame game { get; set; } = null!;

        private FieldInfo? chatOverlayField;
        private FieldInfo? focusedOverlaysField;

        private ChatOverlay? newOverlay;
        private bool hasInjected;

        public ChatOverlayInjector()
        {
            // Ensure Update runs even if this component isn't visible
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var type = game.GetType();
            chatOverlayField = type.GetField("chatOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
            focusedOverlaysField = type.GetField("focusedOverlays", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        protected override void Update()
        {
            base.Update();

            if (hasInjected) return;

            // Use the extension method to find the correct container
            var waveContainer = game.GetWaveOverlayPlacementContainer();
            var toolbarContainer = game.GetToolbarContainer();

            // Wait until both containers are ready
            if (waveContainer == null || toolbarContainer == null) return;

            // 1. Inject Overlay
            injectOverlay(waveContainer);

            // 2. Inject Toolbar Button
            injectButton(toolbarContainer);

            hasInjected = true;
        }

        private void injectOverlay(Container targetContainer)
        {
            // Check if injection already happened by looking for our custom ChatOverlay in the container
            if (targetContainer.Children.OfType<ChatOverlay>().Any())
            {
                newOverlay = targetContainer.Children.OfType<ChatOverlay>().First();
                hasInjected = true;
                return;
            }

            // Retrieve the old overlay using the field
            var oldOverlay = chatOverlayField?.GetValue(game) as OverlayContainer;

            // Remove the old overlay
            if (oldOverlay != null)
            {
                // oldOverlay.Parent?.Remove(oldOverlay, true);

                if (oldOverlay.IsAlive)
                    oldOverlay.Expire();

                // Also remove it from the focusedOverlays list
                if (focusedOverlaysField != null)
                {
                    var list = focusedOverlaysField.GetValue(game) as IList;
                    list?.Remove(oldOverlay);
                }
            }

            // Instantiate and add the new MOsu ChatOverlay
            newOverlay = new ChatOverlay();
            targetContainer.Add(newOverlay);

            // Register the new overlay in focusedOverlays so it handles ESC properly
            if (focusedOverlaysField != null)
            {
                var list = focusedOverlaysField.GetValue(game) as IList;
                list?.Add(newOverlay);
            }
        }

        private void injectButton(FillFlowContainer toolbarContainer)
        {
            // Find the existing ToolbarChatButton
            var oldButton = toolbarContainer.Children.OfType<ToolbarChatButton>().FirstOrDefault();

            // Only replace if the old button is there and we haven't already added ours
            if (oldButton != null && !toolbarContainer.Children.OfType<MOsuToolbarChatButton>().Any())
            {
                int index = toolbarContainer.IndexOf(oldButton);
                toolbarContainer.Remove(oldButton, true);

                // Create and insert our custom button that controls the new overlay
                var newButton = new MOsuToolbarChatButton(newOverlay!);
                toolbarContainer.Insert(index, newButton);
            }
        }

        private partial class MOsuToolbarChatButton : ToolbarOverlayToggleButton
        {
            protected override Anchor TooltipAnchor => Anchor.TopRight;

            public MOsuToolbarChatButton(ChatOverlay chatOverlay)
            {
                Hotkey = GlobalAction.ToggleChat;
                StateContainer = chatOverlay;
            }
        }
    }
}
