using DiscordBridge.BotManager;
using ImGuiNET;
using System;
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

        private int channelIndexToEdit = -1;

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

        public bool channelEditWindow = false;
        public bool ChannelEditWindow
        {
            get { return this.channelEditWindow; }
            set { this.channelEditWindow = value; }
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
            // DrawChannelEditWindow();
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
                }


            }
            ImGui.End();
        }


        public void DrawChatChannelEntry(int index)
        {

            ulong guildID = this.configuration.ChatTypeConfigurations[index].Channel.GuildId;
            string guildIDInput = guildID.ToString();
            ulong channelID = this.configuration.ChatTypeConfigurations[index].Channel.ChannelId;
            Dalamud.Game.Chat.XivChatType type = this.configuration.ChatTypeConfigurations[index].ChatType;
            Configuration.ChannelType chanType = this.configuration.ChatTypeConfigurations[index].Channel.Type;
            int color = this.configuration.ChatTypeConfigurations[index].Color;

            // ImGui.PushFont(Dalamud.Interface.);


            System.Drawing.Color c = System.Drawing.Color.FromArgb(color);

            var buttoncolor = new Vector4(c.R, c.G, c.B, c.A);

            ImGui.Separator();

            //ImGui.PushStyleColor(ImGuiCol.Button, buttoncolor);
            ImGui.ColorButton($"", buttoncolor);
            ImGui.SameLine();
            ImGui.PushItemWidth(-1);
            ImGui.SetNextItemWidth(-100);

            string buttonlabel = $"{type} (Chat type {(int)type}";
            if (ImGui.Button(buttonlabel, new Vector2((int)Math.Truncate(ImGui.GetScrollMaxY()) != 0 ? ElementSizeX - 16 : ElementSizeX, 25)))
            {
                channelIndexToEdit = index;
                ChannelEditWindow = true;
            }
            ImGui.PopItemWidth();
            //ImGui.PopStyleColor();

            if (ImGui.BeginPopupContextItem($"Popup item###{buttonlabel}"))
            {
                if (ImGui.Selectable("Delete"))
                {
                    // this.currentNote = note;
                    // this.deletionWindowVisible = true;
                    bool confirmdeletion = true;
                    if (DrawDeletionConfirmationWindow(ref confirmdeletion))
                    {

                    }
                }
                ImGui.EndPopup();
            }

        }

        private void DrawDeletionConfirmationWindow()
        {
            if (!DeleteConfirmationVisible)
                return;

            var ret = false;

            ImGui.Begin("Remove this channel config?", ImGuiWindowFlags.NoResize);

            ImGui.Text("Are you sure you want to delete this?");
            ImGui.Text("This cannot be undone.");
            if (ImGui.Button("Yes"))
            {
                DeleteConfirmationVisible = false;
                // ret = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("No"))
            {
                DeleteConfirmationVisible = false;
            }

            ImGui.End();

            // return ret;
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


    }
}