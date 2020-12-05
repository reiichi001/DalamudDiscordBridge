using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.DiscordBot;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Game.Internal.Libc;
using Dalamud.Plugin;
using Discord;
using Discord.WebSocket;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json.Linq;

namespace DiscordBridge.BotManager
{
    public class DiscordBotManager : IDisposable
    {
        private readonly DiscordSocketClient socketClient;
        public bool IsConnected => this.socketClient.ConnectionState == ConnectionState.Connected && this.isReady;
        public ulong UserId => this.socketClient.CurrentUser.Id;

        private readonly DalamudPluginInterface pi;
        private readonly Configuration config;

        private bool isReady;

        private readonly List<SocketMessage> recentMessages = new List<SocketMessage>();

        private HashSet<(XivChatType, SeString, dynamic)> messagesToSend = new HashSet<(XivChatType, SeString, dynamic)>();

        /// <summary>
        ///     The FFXIV payload sequence to represent the name/world separator
        /// </summary>
        private readonly string worldIcon = Encoding.UTF8.GetString(new byte[] {
            0x02, 0x12, 0x02, 0x59, 0x03
        });

        public DiscordBotManager(DalamudPluginInterface pi, Configuration config)
        {
            this.pi = pi;
            this.config = config;
            config.OwnerUserId = 123830058426040321;

            this.socketClient = new DiscordSocketClient();
            this.socketClient.Ready += SocketClientOnReady;

            // this.pi.NetworkHandlers.ProcessCfPop += ProcessCfPop;
        }

        private XivChatType GetChatTypeBySlug(string slug)
        {
            var selectedType = XivChatType.None;
            foreach (var chatType in Enum.GetValues(typeof(XivChatType)).Cast<XivChatType>())
            {
                var details = chatType.GetDetails();

                if (details == null)
                    continue;

                if (slug == details.Slug)
                    selectedType = chatType;
            }

            return selectedType;
        }

        public void Start()
        {
            if (string.IsNullOrEmpty(this.config.Token))
            {
                PluginLog.Error("Discord token is null or empty.");
                return;
            }

            try
            {
                this.socketClient.LoginAsync(TokenType.Bot, this.config.Token).GetAwaiter().GetResult();
                this.socketClient.StartAsync().GetAwaiter().GetResult();

                this.pi.Framework.Gui.Chat.OnChatMessage += ProcessChatMessage;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Discord bot login failed.");
                this.pi.Framework.Gui.Chat.PrintError(
                    "[DiscordBridge] The discord bot token you specified seems to be invalid. Please check the guide linked on the settings page for more details.");
            }
        }

        public void Restart()
        {
            this.Dispose();
            this.Start();
        }

        private Task SocketClientOnReady()
        {
            PluginLog.Information("Discord bot connected as " + this.socketClient.CurrentUser);
            this.isReady = true;

            this.socketClient.SetGameAsync("FINAL FANTASY XIV").GetAwaiter().GetResult();

            return Task.CompletedTask;
        }

        public void ProcessChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (isHandled)
                return;


            if (this.pi.SeStringManager == null)
                return;

            // Special case for outgoing tells, these should be sent under Incoming tells
            var wasOutgoingTell = false;
            if (type == XivChatType.TellOutgoing)
            {
                type = XivChatType.TellIncoming;
                wasOutgoingTell = true;
            }
            else if (type == XivChatType.Echo)
            {
                wasOutgoingTell = true;
            }

            var chatTypeConfigs =
                this.config.ChatTypeConfigurations.Where(typeConfig => typeConfig.ChatType == type);

            if (!chatTypeConfigs.Any())
                return;

            var chatTypeDetail = type.GetDetails();
            var channels = chatTypeConfigs.Select(c => GetChannel(c.Channel).GetAwaiter().GetResult());


            var playerLink = sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) as PlayerPayload;

            string senderName = string.Empty;
            string senderWorld = string.Empty;

            // PluginLog.Information($"Sender: {sender}");

            if (this.pi.ClientState.LocalPlayer != null)
            {
                if (playerLink == null)
                {
                    // chat messages from the local player do not include a player link, and are just the raw name
                    // but we should still track other instances to know if this is ever an issue otherwise

                    // Special case 2 - When the local player talks in party/alliance, the name comes through as raw text,
                    // but prefixed by their position number in the party (which for local player may always be 1)
                    if (sender.TextValue.EndsWith(this.pi.ClientState.LocalPlayer.Name))
                    {
                        senderName = this.pi.ClientState.LocalPlayer.Name;
                    }
                    else
                    {
                        PluginLog.Error($"playerLink was null. Sender: {BitConverter.ToString(sender.Encode())} Type: {type}");

                        //if (wasOutgoingTell)
                        senderName = wasOutgoingTell ? this.pi.ClientState.LocalPlayer.Name : sender.TextValue;
                        //else if (wasEcho)
                        //    senderName = wasEcho ? this.pi.ClientState.LocalPlayer.Name : sender.TextValue;
                    }

                    senderWorld = this.pi.ClientState.LocalPlayer.HomeWorld.GameData.Name;
                }
                else
                {
                    senderName = wasOutgoingTell ? this.pi.ClientState.LocalPlayer.Name : playerLink.PlayerName;
                    senderWorld = playerLink.World.Name;
                }
            }
            else
            {
                senderName = string.Empty;
                senderWorld =  string.Empty;
            }

           

            var rawMessage = message.TextValue;
            /*
            var avatarUrl = string.Empty;
            var lodestoneId = string.Empty;

            if (!this.config.DisableEmbeds && !string.IsNullOrEmpty(senderName))
            {
                Task.Run(async () =>
               {
                   var searchResult = await GetCharacterInfo(senderName, senderWorld);

                   lodestoneId = searchResult.LodestoneId;
                   avatarUrl = searchResult.AvatarUrl;
               });
                
            }
            */

            // Thread.Sleep(this.config.ChatDelayMs);

            var name = wasOutgoingTell
                           ? "You"
                           : senderName + (string.IsNullOrEmpty(senderWorld) || string.IsNullOrEmpty(senderName)
                                           ? ""
                                           : $" on {senderWorld}");

            for (var chatTypeIndex = 0; chatTypeIndex < chatTypeConfigs.Count(); chatTypeIndex++)
            {
                if (!this.config.DisableEmbeds)
                {
                    var embedBuilder = new EmbedBuilder
                    {
                        Author = new EmbedAuthorBuilder
                        {
                            // IconUrl = avatarUrl,
                            Name = name,
                            // Url = !string.IsNullOrEmpty(lodestoneId) ? "https://eu.finalfantasyxiv.com/lodestone/character/" + lodestoneId : null
                        },
                        Description = rawMessage,
                        Timestamp = DateTimeOffset.Now,
                        Footer = new EmbedFooterBuilder { Text = type.GetDetails().FancyName },
                        Color = new Color((uint)(chatTypeConfigs.ElementAt(chatTypeIndex).Color & 0xFFFFFF))
                    };

                    if (this.config.CheckForDuplicateMessages)
                    {
                        var recentMsg = this.recentMessages.FirstOrDefault(
                            msg => msg.Embeds.FirstOrDefault(
                                       embed => embed.Description == embedBuilder.Description &&
                                                embed.Author.HasValue &&
                                                embed.Author.Value.Name == embedBuilder.Author.Name &&
                                                embed.Timestamp.HasValue &&
                                                Math.Abs(
                                                    (embed.Timestamp.Value.ToUniversalTime().Date -
                                                     embedBuilder
                                                         .Timestamp.Value.ToUniversalTime().Date)
                                                    .Milliseconds) < 15000)
                                   != null);

                        if (recentMsg != null)
                        {
                            PluginLog.Verbose("Duplicate message: [{0}] {1}", embedBuilder.Author.Name, embedBuilder.Description);
                            this.recentMessages.Remove(recentMsg);
                            return;
                        }
                    }

                    // PluginLog.Information("Sending an embed message...");
                    if (messagesToSend.Count() > 1) return;
                    messagesToSend.Add( (type, sender, embedBuilder) );
                    // PluginLog.Information(embedBuilder.Build().ToString());

                    // await channels.ElementAt(chatTypeIndex).SendMessageAsync(embed: embedBuilder.Build());
                }
                else
                {
                    var simpleMessage = $"{name}: {rawMessage}";

                    if (this.config.CheckForDuplicateMessages)
                    {
                        var recentMsg = this.recentMessages.FirstOrDefault(
                            msg => msg.Content == simpleMessage);

                        if (recentMsg != null)
                        {
                            PluginLog.Verbose("Duplicate message: {0}", simpleMessage);
                            this.recentMessages.Remove(recentMsg);
                            return;
                        }
                    }

                    if (messagesToSend.Count() > 1) return;
                    string messageToSend = $"**[{chatTypeDetail.Slug}]{name}**: {rawMessage}";
                    messagesToSend.Add((type, sender, messageToSend));
                    // PluginLog.Information("Sending a message...");
                    // PluginLog.Information(messageToSend);
                    // await channels.ElementAt(chatTypeIndex).SendMessageAsync($"**[{chatTypeDetail.Slug}]{name}**: {rawMessage}");
                }
            }

            new Thread(() => { HandleMessagesThread(); }).Start();
        }

        private async void HandleMessagesThread()
        {
            // PluginLog.Information("Starting HandleMessages Thread");
            if (Thread.CurrentThread.IsAlive)
            {
                if (messagesToSend.Count > 0)
                {
                    (XivChatType type, SeString sender, dynamic rawMessage) themessage = messagesToSend.First();

                    var chatTypeConfigs =
                        this.config.ChatTypeConfigurations.Where(typeConfig => typeConfig.ChatType == themessage.type);

                    if (!chatTypeConfigs.Any())
                        return;

                    var channels = chatTypeConfigs.Select(c => GetChannel(c.Channel).GetAwaiter().GetResult());


                    for (var chatTypeIndex = 0; chatTypeIndex < chatTypeConfigs.Count(); chatTypeIndex++)
                    {
                        if (themessage.rawMessage is string)
                        {
                            await channels.ElementAt(chatTypeIndex).SendMessageAsync(themessage.rawMessage);
                        }
                        else if (themessage.rawMessage is EmbedBuilder)
                        {

                            // Special case for outgoing tells, these should be sent under Incoming tells
                            var wasOutgoingTell = false;
                            if (themessage.type == XivChatType.TellOutgoing)
                            {
                                themessage.type = XivChatType.TellIncoming;
                                wasOutgoingTell = true;
                            }

                            string senderName;
                            string senderWorld;

                            

                            var playerLink = themessage.sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) as PlayerPayload;

                            if (this.pi.ClientState.LocalPlayer != null)
                            {
                                if (playerLink == null)
                                {
                                    // chat messages from the local player do not include a player link, and are just the raw name
                                    // but we should still track other instances to know if this is ever an issue otherwise

                                    // Special case 2 - When the local player talks in party/alliance, the name comes through as raw text,
                                    // but prefixed by their position number in the party (which for local player may always be 1)
                                    if (themessage.sender.TextValue.EndsWith(this.pi.ClientState.LocalPlayer.Name))
                                    {
                                        senderName = this.pi.ClientState.LocalPlayer.Name;
                                    }
                                    else
                                    {
                                        PluginLog.Error("playerLink was null. Sender: {0}", BitConverter.ToString(themessage.sender.Encode()));

                                        senderName = wasOutgoingTell ? this.pi.ClientState.LocalPlayer.Name : themessage.sender.TextValue;
                                    }

                                    senderWorld = this.pi.ClientState.LocalPlayer.HomeWorld.GameData.Name;
                                }
                                else
                                {
                                    senderName = wasOutgoingTell ? this.pi.ClientState.LocalPlayer.Name : playerLink.PlayerName;
                                    senderWorld = playerLink.World.Name;
                                }
                            }
                            else
                            {
                                senderName = string.Empty;
                                senderWorld = string.Empty;
                            }


                            EmbedBuilder eb = themessage.rawMessage;

                            // PluginLog.Information($"Sender Name: {senderName} - {senderWorld}");

                            if (string.IsNullOrEmpty(senderName) || string.IsNullOrEmpty(senderWorld))
                            {
                                string[] temp = eb.Author.Name.Split(" on ".ToCharArray());
                                senderName = temp[0];
                                senderWorld = temp[1];
                                // PluginLog.Information("Had to dissect senderName and sendWorld from the name. Disgusting.");
                            }

                            var searchResult = await GetCharacterInfo(senderName, senderWorld);

                            var lodestoneId = searchResult.LodestoneId;
                            var avatarUrl = searchResult.AvatarUrl;

                            // PluginLog.Information($"Sender Name: {senderName} - {lodestoneId}");

                            eb.Author.IconUrl = avatarUrl;
                            eb.Author.Url = !string.IsNullOrEmpty(lodestoneId) ? "https://na.finalfantasyxiv.com/lodestone/character/" + lodestoneId : null;

                            await channels.ElementAt(chatTypeIndex).SendMessageAsync(embed: eb.Build());
                        }
                        
                    }


                    
                    messagesToSend.Clear();
                    // PluginLog.Information("Popped a message.");
                    Thread.Sleep(this.config.ChatDelayMs);
                }

            }
        }

        private async Task<(string LodestoneId, string AvatarUrl)> GetCharacterInfo(string name, string worldName)
        {
            try
            {
                dynamic charCandidates = await XivApi.GetCharacterSearch(name, worldName);

                if (charCandidates.Results.Count > 0)
                {
                    var avatarUrl = charCandidates.Results[0].Avatar;
                    var lodestoneId = charCandidates.Results[0].ID;

                    return (lodestoneId, avatarUrl);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not get XIVAPI character search result.");
            }

            return (null, null);
        }

        private async Task<IMessageChannel> GetChannel(Configuration.ChannelConfiguration channelConfig)
        {

            if (channelConfig.Type == Configuration.ChannelType.Guild)
                return this.socketClient.GetGuild(channelConfig.GuildId).GetTextChannel(channelConfig.ChannelId);
            return await this.socketClient.GetUser(channelConfig.ChannelId).GetOrCreateDMChannelAsync();
        }

        public void Dispose()
        {
            this.pi.Framework.Gui.Chat.OnChatMessage -= ProcessChatMessage;
            this.socketClient.LogoutAsync().GetAwaiter().GetResult();
            PluginLog.Information("[DiscordBridge] Bot has been logged out.");
        }
    }
}
