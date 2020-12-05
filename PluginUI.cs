using Dalamud.Game.Chat;
using Dalamud.Plugin;
using DiscordBridge.BotManager;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DiscordBridge
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private readonly Plugin plugin;

        private static float WindowSizeY => ImGui.GetWindowSize().Y;
        private static float ElementSizeX => ImGui.GetWindowSize().X - 16;

        // private ImGuiScene.TextureWrap goatImage;

        Configuration.ChatTypeConfiguration currentEntry = null;
        Configuration.ChatTypeConfiguration selectedChannelConfig = null;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }


        private bool tokenInputVisible = false;
        public bool TokenInputVisible
        {
            get { return this.tokenInputVisible; }
            set { this.tokenInputVisible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        public bool channelEditWindowVisible = false;
        public bool ChannelEditWindowVisible
        {
            get { return this.channelEditWindowVisible; }
            set { this.channelEditWindowVisible = value; }
        }

        public bool deleteConfirmationVisible = false;
        public bool DeleteConfirmationVisible
        {
            get { return this.deleteConfirmationVisible; }
            set { this.deleteConfirmationVisible = value; }
        }

        // private bool 

        // passing in the image here just for simplicity
        public PluginUI(Configuration configuration, Plugin plugininstance)
        {
            this.configuration = configuration;
            this.plugin = plugininstance;
            //this.goatImage = goatImage;
        }

        public void Dispose()
        {
            //this.goatImage.Dispose();
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawMainWindow();
            DrawSettingsWindow();
            DrawTokenInputWindow();
            DrawChannelAddEditWindow();
            DrawDeletionConfirmationWindow();
        }

        public void Save()
        {
            this.configuration.Save();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            // initial install stuff
            if (this.configuration == null)
            {
                this.configuration = new Configuration();
            }
            if (this.configuration.ChatTypeConfigurations == null)
            {
                this.configuration.ChatTypeConfigurations = new List<Configuration.ChatTypeConfiguration>();
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Discord Bridge Config", ref this.visible)) //, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text("Discord Bot Token:");
                if (ImGui.Button("Set or Change Bot Token"))
                {
                    TokenInputVisible = true;
                }

                // ImGui.Text($"");
                // ImGui.SameLine();
                bool checkForDuplicateMessages = this.configuration.CheckForDuplicateMessages;
                if (ImGui.Checkbox($"Check for duplicate messages?", ref checkForDuplicateMessages))
                {
                    this.configuration.CheckForDuplicateMessages = checkForDuplicateMessages;
                    Save();
                }

                // ImGui.Text($"");
                // ImGui.SameLine();
                bool disableEmbeds = this.configuration.DisableEmbeds;
                if (ImGui.Checkbox($"Disable Rich Embeds", ref disableEmbeds))
                {
                    this.configuration.DisableEmbeds = disableEmbeds;
                    Save();
                }

                // ImGui.Text("");
                // ImGui.SameLine();
                int ChatDelayMsInput = this.configuration.ChatDelayMs;
                ImGui.SetNextItemWidth(125);
                if (ImGui.InputInt($"Delay chat [in milliseconds]", ref ChatDelayMsInput, 100))
                {
                    this.configuration.ChatDelayMs = ChatDelayMsInput;
                    Save();
                }


                

                ImGui.Spacing();

                // list out all the channel settings here

                
                for (int i = 0; i < configuration.ChatTypeConfigurations.Count; i++)
                {
                    DrawChatChannelEntry(i);
                }


                ImGui.Spacing();
                ImGui.Separator();
                if (ImGui.Button("Add Entry"))
                {
                    // open a window to add new entry
                    Configuration.ChatTypeConfiguration newconfig = new Configuration.ChatTypeConfiguration();
                    newconfig.Channel = new Configuration.ChannelConfiguration();
                    newconfig.Channel.ChannelId = 0;
                    newconfig.Channel.GuildId = 0;
                    newconfig.Channel.Type = 0;
                    newconfig.ChatType = XivChatType.None;
                    newconfig.Color = 0;
                    this.configuration.ChatTypeConfigurations.Add(newconfig);
                }


            }
            ImGui.End();
        }


        public void DrawChatChannelEntry(int index)
        {
            this.currentEntry = this.configuration.ChatTypeConfigurations[index];
            ulong guildID = currentEntry.Channel.GuildId;
            // string guildIDInput = guildID.ToString();
            ulong channelID = currentEntry.Channel.ChannelId;
            Dalamud.Game.Chat.XivChatType type = currentEntry.ChatType;
            Configuration.ChannelType chanType = currentEntry.Channel.Type;
            

            // Making colors work is the worst shit.
            byte[] colorvals = BitConverter.GetBytes(currentEntry.Color);

            int red = (int)(currentEntry.Color >> 16) & 0xFF;
            int green = (int)(currentEntry.Color >> 8) & 0xFF;
            int blue = (int)currentEntry.Color & 0xFF;

            Vector4 colors = new Vector4(colorvals[2]/255.0f, colorvals[1]/255.0f, colorvals[0]/255.0f, 1.0f );

            ImGui.Separator();

            //ImGui.PushStyleColor(ImGuiCol.Button, buttoncolor);
            //ImGui.ColorButton($"", buttoncolor);
            if (ImGui.ColorButton($"##colorpicker-{type}-{guildID}-{channelID}", colors))
            {
                // currentEntry.Color = ImGui.ColorConvertFloat4ToU32(buttoncolor);
            }
            ImGui.SameLine();
            ImGui.PushItemWidth(-1);
            ImGui.SetNextItemWidth(-100);

            string buttonlabel = $"{type} (Chat type {(int)type}##{index})";
            if (ImGui.Button(buttonlabel, new Vector2((int)Math.Truncate(ImGui.GetScrollMaxY()) != 0 ? ElementSizeX - 16 : ElementSizeX, 25)))
            {
                this.selectedChannelConfig = this.configuration.ChatTypeConfigurations[index];
                channelEditWindowVisible = true;
            }
            ImGui.PopItemWidth();
            //ImGui.PopStyleColor();

            if (ImGui.BeginPopupContextItem($"Popup item###{buttonlabel}"))
            {
                if (ImGui.Selectable("Delete"))
                {
                    // this.currentNote = note;
                    // this.deletionWindowVisible = true;
                    DeleteConfirmationVisible = true;
                    //DrawDeletionConfirmationWindow();
                    //PluginLog.Information($"Loaded Delete confirm window? {DrawDeletionConfirmationWindow()}");
                    if (DrawDeletionConfirmationWindow())
                    {
                        
                    }
                }
                ImGui.EndPopup();
            }

        }

        private bool DrawDeletionConfirmationWindow()
        {
            if (!DeleteConfirmationVisible)
                return false;

            var ret = false;

            ImGui.SetNextWindowSize(new Vector2(232, 100), ImGuiCond.Always);
            if (ImGui.Begin("Remove this channel config?", ref this.deleteConfirmationVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text("Are you sure you want to delete this?");
                ImGui.Text("This cannot be undone.");
                if (ImGui.Button("Yes"))
                {
                    
                    
                    PluginLog.Verbose("Killing the thing.");

                    this.configuration.ChatTypeConfigurations.Remove(currentEntry);

                    PluginLog.Verbose($"Removed the configuration for {currentEntry.Channel.ToString()}");
                    Save();

                    currentEntry = null;
                    // Visible = false;
                    // Visible = true;

                    ret = true;
                    DeleteConfirmationVisible = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("No"))
                {
                    Visible = false;
                    Visible = true;
                    DeleteConfirmationVisible = false;
                }
            }

                

            ImGui.End();

            return ret;
        }

        public void DrawSettingsWindow()
        {
            if (!SettingsVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(232, 75), ImGuiCond.Always);
            if (ImGui.Begin("Discord Bridge Settings", ref this.settingsVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                var configValue = this.configuration.DisableEmbeds;
                if (ImGui.Checkbox("DisableEmbeds", ref configValue))
                {
                    this.configuration.DisableEmbeds = configValue;
                    // can save immediately on change, if you don't want to provide a "Save and Close" button
                    Save();
                }
            }
            ImGui.End();
        }

        public void DrawTokenInputWindow()
        {
            if (!TokenInputVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(600, 90), ImGuiCond.Always);
            if (ImGui.Begin("Edit Discord Bot Token", ref this.tokenInputVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                // can't ref a property, so use a local copy
                string tokenInput = this.configuration.Token;
                // ImGui.SetNextItemWidth(-100);
                ImGui.PushItemWidth(-1);
                if (ImGui.InputText("", ref tokenInput, 192))
                {
                    this.configuration.Token = tokenInput;
                }
                ImGui.PopItemWidth();
                if (ImGui.Button("Apply and Restart Bot"))
                {
                    // this.bot.Dispose();
                    this.plugin.RestartBot();
                    TokenInputVisible = false;
                    Save();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    TokenInputVisible = false;
                }
            }
            ImGui.End();
        }

        public void DrawChannelAddEditWindow()
        {
            if (!ChannelEditWindowVisible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(400, 600), ImGuiCond.Always);
            if (ImGui.Begin("Add/Edit Channel Configuration", ref this.channelEditWindowVisible,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.TextWrapped($"Please select what type of XIVChat you'd like to relay and where it should go.");
                ImGui.Spacing();
                // ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
                /*
                var currentitem = currentEntry.ChatType;
                PluginLog.Information($"Current XivChatType: {currentitem}");
                if (ImGui.BeginCombo("##XivChatTypeSelector", $"Select a Chat Type"))
                {
                    foreach (var availableChatType in XivChatTypeExtensions.TypeInfoDict)
                    {
                        bool isSelected = (currentitem == availableChatType.Key);
                        if(ImGui.Selectable($"{availableChatType.Key.GetFancyName()} ({(int)availableChatType.Key})", isSelected))
                        {
                            // set the channel type to this
                            PluginLog.Information($"Inside the Selectable() right now for {availableChatType.Key}");
                        }
                        if (isSelected)
                        {
                            currentEntry.ChatType = availableChatType.Key;
                            ImGui.SetItemDefaultFocus();
                        }
                            
                    }
                    ImGui.EndCombo();
                }
                */

                XivChatType currentChatTypeSelected = selectedChannelConfig.ChatType;



                string[] list = XivChatTypeExtensions.TypeInfoDict.Select(x => x.Key.GetFancyName()).ToArray();
                int currentListIndex = Array.IndexOf(list, selectedChannelConfig.ChatType.GetFancyName());
                if (ImGui.Combo("##XivChatTypeSelector", ref currentListIndex, list, list.Length))
                {
                    selectedChannelConfig.ChatType = XivChatTypeExtensions.GetByFancyName(list[currentListIndex]);
                    PluginLog.Information($"Set ChatType to {selectedChannelConfig.ChatType}");
                }
                

                
                string[]  channel_types = {Configuration.ChannelType.Guild.ToString(), Configuration.ChannelType.User.ToString() };
                int channelTypeSelection = (int)selectedChannelConfig.Channel.Type;
                if (ImGui.Combo("##XivChannelTypeSelector", ref channelTypeSelection, channel_types, 2))
                {
                    selectedChannelConfig.Channel.Type = (Configuration.ChannelType)channelTypeSelection;
                    PluginLog.Information($"Set ChannelType to {selectedChannelConfig.Channel.Type}");
                }

                ImGui.Text($"Server ID");
                string serverIDInput = selectedChannelConfig.Channel.GuildId.ToString();
                ImGui.InputTextWithHint($"##ServerID", $"Put your Server ID here", ref serverIDInput, 30);

                ImGui.Text($"Channel ID");
                string channelIDInput = selectedChannelConfig.Channel.ChannelId.ToString();
                ImGui.InputTextWithHint($"##ChannelID", $"Put your Server ID here", ref channelIDInput, 30);

                ImGui.Spacing();
                // ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.4f);
                byte[] values = BitConverter.GetBytes(selectedChannelConfig.Color);
                //Vector4 embedColor = ImGui.ColorConvertU32ToFloat4(selectedChannelConfig.Color);
                Vector4 embedColor = new Vector4(values[2] / 255.0f, values[1] / 255.0f, values[0] / 255.0f, 1.0f);
                if (ImGui.ColorPicker4($"EmbedColor", ref embedColor))
                {
                    selectedChannelConfig.Color = ImGui.ColorConvertFloat4ToU32(embedColor);

                    
                }
                

                ImGui.Spacing();
                
                if (ImGui.Button("Apply"))
                {
                    // this.bot.Dispose();
                    int red = (byte)(embedColor.X * 255);
                    int green = (byte)(embedColor.Y * 255);
                    int blue = (byte)(embedColor.Z * 255);

                    int rgb = red;
                    rgb = (rgb << 8) + green;
                    rgb = (rgb << 8) + blue;

                    selectedChannelConfig.Color = (uint)rgb;
                    this.plugin.RestartBot();
                    selectedChannelConfig = null;
                    Save();
                    Visible = false;
                    Visible = true;
                    ChannelEditWindowVisible = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    Visible = false;
                    Visible = true;
                    ChannelEditWindowVisible = false;
                }
                
            }
            ImGui.End();
        }


    }
}