using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Dalamud.Game.Chat;
using System.Collections.Generic;

namespace DiscordBridge
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        // Add any other properties or methods here.
        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public string Token { get; set; }

        public bool CheckForDuplicateMessages { get; set; }
        public int ChatDelayMs { get; set; }

        public bool DisableEmbeds { get; set; }

        public ulong OwnerUserId { get; set; }

        public List<ChatTypeConfiguration> ChatTypeConfigurations { get; set; }

        public ChannelConfiguration CfNotificationChannel { get; set; }
        public ChannelConfiguration CfPreferredRoleChannel { get; set; }
        public ChannelConfiguration RetainerNotificationChannel { get; set; }

        public enum ChannelType
        {
            Guild,
            User
        }

        public class ChannelConfiguration
        {
            public ChannelType Type { get; set; }

            public ulong GuildId { get; set; }
            public ulong ChannelId { get; set; }
        }

        public class ChatTypeConfiguration
        {
            public XivChatType ChatType { get; set; }

            public ChannelConfiguration Channel { get; set; }
            public uint Color { get; set; }
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
