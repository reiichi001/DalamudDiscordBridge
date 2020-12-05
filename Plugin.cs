using System;
using Dalamud.Plugin;
using DiscordBridge.Attributes;

namespace DiscordBridge
{
    public class Plugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        private Configuration config;
        private PluginUI ui;
        private DiscordBridge.BotManager.DiscordBotManager bot;
        public string Name => "Discord Relay";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.config.Initialize(this.pluginInterface);

            this.ui = new PluginUI(this.config, this);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);

            this.bot = new BotManager.DiscordBotManager(this.pluginInterface, this.config);

            // this.pluginInterface.Framework.Gui.Chat.OnChatMessage += this.bot.ProcessChatMessage;

            this.bot.Start();
            
        }

        [Command("/prelay")]
        [HelpMessage("Open Franz's Discord bot settings.")]
        public void DiscordSettingsCommand(string command, string args)
        {
            // You may want to assign these references to private variables for convenience.
            // Keep in mind that the local player does not exist until after logging in.
            //var chat = this.pluginInterface.Framework.Gui.Chat;
            //var world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData;
            //chat.Print($"Hello {world.Name}!");
            //PluginLog.Log("Message sent successfully.");
            
            this.ui.Visible = true;
        }

        public bool RestartBot()
        {
            if(this.bot.IsConnected)
            {
                this.bot.Restart();
            }


            return this.bot.IsConnected;
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.config);

            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.pluginInterface.Dispose();

            this.pluginInterface.Framework.Gui.Chat.OnChatMessage -= this.bot.ProcessChatMessage;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
