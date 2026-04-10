using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewAIMod.Services;
using StardewAIMod.Models;

namespace StardewAIMod.Menus
{
    public class ChatMenu : IClickableMenu
    {
        private readonly NPC _npc;
        private readonly VeniceApiService _veniceApi;
        private readonly MemoryService _memoryService;
        private readonly PromptBuilder _promptBuilder;
        private readonly ModConfig _config;

        private TextBox _textBox;
        private ClickableTextureComponent _sendButton;

        private bool _isWaitingForResponse = false;
        private string _errorMessage = "";
        private readonly string _modDirectory;

        private int _animationTimer = 0;
        private string _thinkingEllipsis = "";

        // Constructor modificado para pasar dimensiones al IClickableMenu (Soluciona posición 0,0)
        public ChatMenu(NPC npc, VeniceApiService veniceApi, MemoryService memoryService, ModConfig config, string modDirectory)
            : base(
                  (Game1.uiViewport.Width / 2) - (800 / 2),
                  Game1.uiViewport.Height - 300 - 64,
                  800,
                  300
              )
        {
            _npc = npc;
            _veniceApi = veniceApi;
            _memoryService = memoryService;
            _config = config;
            _modDirectory = modDirectory;
            _promptBuilder = new PromptBuilder(modDirectory);

            int padding = 32;
            Texture2D textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
            int portraitWidth = 128;

            _textBox = new TextBox(textBoxTexture, null, Game1.dialogueFont, Game1.textColor)
            {
                X = this.xPositionOnScreen + padding + portraitWidth + 16,
                Y = this.yPositionOnScreen + this.height - padding - 64,
                Width = this.width - portraitWidth - (padding * 3) - 64 - 16,
                Height = 64,
                limitWidth = false,
                textLimit = 200,
                Selected = true
            };

            _textBox.Text = "";
            Game1.keyboardDispatcher.Subscriber = _textBox;

            _sendButton = new ClickableTextureComponent(
                new Rectangle(_textBox.X + _textBox.Width + 16, _textBox.Y, 64, 64),
                Game1.mouseCursors,
                new Rectangle(16, 368, 16, 16),
                4f
            );
        }

        // Blindaje contra cierres accidentales al dar clic (Soluciona el problema de que desaparece)
        public override bool readyToClose()
        {
            return false;
        }

        public override void receiveRightClick(int x, int y, bool playSound = true)
        {
            // Absorber clic derecho para que no haga interactúe con el mapa de fondo
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (_isWaitingForResponse)
                return;

            _textBox.Update();

            if (_sendButton.containsPoint(x, y))
            {
                if (playSound) Game1.playSound("coin");
                SendMessage();
            }
            else if (isWithinBounds(x, y))
            {
                Rectangle textBoxBounds = new Rectangle(_textBox.X, _textBox.Y, _textBox.Width, _textBox.Height);
                if (textBoxBounds.Contains(x, y))
                {
                    _textBox.Selected = true;
                    Game1.keyboardDispatcher.Subscriber = _textBox;
                }
            }
            else
            {
                _textBox.Selected = false;
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                exitThisMenu(false);
                return;
            }

            if (_textBox.Selected && key == Keys.Enter && !_isWaitingForResponse)
            {
                SendMessage();
                return;
            }

            if (!_textBox.Selected)
            {
                base.receiveKeyPress(key);
            }
        }

        private async void SendMessage()
        {
            string playerText = _textBox.Text.Trim();
            if (string.IsNullOrEmpty(playerText) || playerText == "[Escribe tu mensaje...]") return;

            _textBox.Text = "";
            _isWaitingForResponse = true;

            try
            {
                var memory = _memoryService.GetMemory(_npc.Name);

                memory.PlayerName = Game1.player.Name;
                memory.FriendshipHearts = Game1.player.getFriendshipHeartLevelForNPC(_npc.Name);

                string relationship = "Stranger";
                if (Game1.player.spouse == _npc.Name)
                {
                    relationship = "Spouse";
                }
                else if (Game1.player.friendshipData.ContainsKey(_npc.Name))
                {
                    var friendship = Game1.player.friendshipData[_npc.Name];
                    if (friendship.IsDating())
                    {
                        relationship = "Boyfriend/Girlfriend";
                    }
                    else if (memory.FriendshipHearts >= 8)
                    {
                        relationship = "Best Friend";
                    }
                    else if (memory.FriendshipHearts >= 4)
                    {
                        relationship = "Friend";
                    }
                    else if (memory.FriendshipHearts > 0)
                    {
                        relationship = "Acquaintance";
                    }
                }
                memory.RelationshipStatus = relationship;

                string playerClothing = $"Hat: {(Game1.player.hat.Value != null ? Game1.player.hat.Value.DisplayName : "None")}, " +
                                        $"Shirt: {(Game1.player.shirtItem.Value != null ? Game1.player.shirtItem.Value.DisplayName : "Standard")}, " +
                                        $"Pants: {(Game1.player.pantsItem.Value != null ? Game1.player.pantsItem.Value.DisplayName : "Standard")}";

                List<string> nearbyNpcs = new List<string>();
                foreach (var character in Game1.currentLocation.characters)
                {
                    if (character.Name != _npc.Name && Vector2.Distance(Game1.player.Tile, character.Tile) < 10)
                    {
                        nearbyNpcs.Add(character.Name);
                    }
                }
                string environmentNotes = nearbyNpcs.Count > 0
                    ? $"Nearby characters: {string.Join(", ", nearbyNpcs)}"
                    : "You and the player are mostly alone here.";

                string holdingItem = Game1.player.ActiveObject != null ? Game1.player.ActiveObject.DisplayName : "Nothing";
                bool isBirthday = _npc.isBirthday();
                string playerStatus = "Healthy and energetic.";
                if (Game1.player.health < Game1.player.maxHealth / 3)
                    playerStatus = "Looks wounded or very sick.";
                else if (Game1.player.Stamina < 20)
                    playerStatus = "Looks extremely exhausted and ready to pass out.";

                var currentContext = new Dictionary<string, string>
                {
                    { "Season", Game1.currentSeason },
                    { "Day", Game1.dayOfMonth.ToString() },
                    { "Time", Game1.timeOfDay.ToString() },
                    { "Weather", Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny") },
                    { "Location", Game1.currentLocation.Name },
                    { "Player Clothing", playerClothing },
                    { "Environment", environmentNotes },
                    { "Player is holding", holdingItem },
                    { "Player Physical Status", playerStatus },
                    { "Is it your Birthday today?", isBirthday ? "YES! You expect people to congratulate you." : "No." }
                };

                string systemPrompt = _promptBuilder.BuildSystemPrompt(_npc.Name, memory, currentContext, playerText);

                _memoryService.AddToConversationHistory(_npc.Name, "player", playerText);

                string reply = await _veniceApi.SendMessageAsync(systemPrompt, memory.ConversationHistory);

                if (reply.StartsWith("[") && (reply.Contains("Error") || reply.Contains("No response")))
                {
                    _errorMessage = "They need a moment to think. Please wait a few seconds.";
                    return;
                }

                _errorMessage = "";

                if (Game1.player.friendshipData.ContainsKey(_npc.Name))
                {
                    Game1.player.friendshipData[_npc.Name].TalkedToToday = true;
                }

                _memoryService.AddToConversationHistory(_npc.Name, "assistant", reply);

                if (reply.Contains("$l"))
                {
                    Game1.player.changeFriendship(15, _npc);
                }
                else if (reply.Contains("$h"))
                {
                    Game1.player.changeFriendship(10, _npc);
                }
                else if (reply.Contains("$a"))
                {
                    Game1.player.changeFriendship(-20, _npc);
                }

                if (reply.Contains("$follow"))
                {
                    Game1.addHUDMessage(new HUDMessage($"{_npc.Name} is now following you! (WIP feature)", HUDMessage.newQuest_type));
                }

                string formattedReply = FormatDialogueText(reply);

                _npc.CurrentDialogue.Push(new Dialogue(_npc, null, formattedReply));

                Game1.drawDialogue(_npc);

                Game1.afterDialogues = new Game1.afterFadeFunction(() =>
                {
                    if (Game1.CurrentEvent == null && Game1.activeClickableMenu == null)
                    {
                        if (Vector2.Distance(Game1.player.Tile, _npc.Tile) < 3)
                        {
                            Game1.activeClickableMenu = new ChatMenu(_npc, _veniceApi, _memoryService, _config, _modDirectory);
                        }
                    }
                });

                this.exitThisMenu(false);
            }
            catch (Exception ex)
            {
                _errorMessage = "Error connecting to AI.";
                Console.WriteLine(ex);
            }
            finally
            {
                _isWaitingForResponse = false;
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);
            if (_textBox != null && _textBox.Selected)
            {
                _textBox.Update();
            }

            _animationTimer += time.ElapsedGameTime.Milliseconds;
            if (_animationTimer > 500)
            {
                _animationTimer = 0;
                if (_thinkingEllipsis.Length < 3)
                    _thinkingEllipsis += ".";
                else
                    _thinkingEllipsis = "";
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.4f);

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White, 1f, true);

            int padding = 32;

            string title = $"Chateando con {_npc.Name}";
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2(this.xPositionOnScreen + padding, this.yPositionOnScreen + padding), Game1.textColor);

            if (_npc.Portrait != null)
            {
                b.Draw(_npc.Portrait,
                    new Vector2(this.xPositionOnScreen + padding, this.yPositionOnScreen + padding + 48),
                    new Rectangle(0, 0, 64, 64),
                    Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0.8f);
            }

            if (_isWaitingForResponse)
            {
                string thinkingText = $"Pensando{_thinkingEllipsis}";
                Utility.drawTextWithShadow(b, thinkingText, Game1.dialogueFont,
                    new Vector2(_textBox.X, _textBox.Y + 16), Color.DarkGray);
            }
            else
            {
                _textBox.Draw(b);

                if (string.IsNullOrEmpty(_textBox.Text))
                {
                    Utility.drawTextWithShadow(b, "Escribe tu mensaje...", Game1.smallFont,
                        new Vector2(_textBox.X + 16, _textBox.Y + 16), Color.Gray);
                }

                _sendButton.draw(b);
            }

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                Utility.drawTextWithShadow(b, _errorMessage, Game1.smallFont,
                    new Vector2(_textBox.X, _textBox.Y - 24), Color.Red);
            }

            drawMouse(b);
        }

        private string FormatDialogueText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string currentBlock = "";
            string finalResult = "";

            foreach (var word in words)
            {
                if (currentBlock.Length + word.Length + (currentBlock.Length > 0 ? 1 : 0) > 120 && currentBlock.Length > 0)
                {
                    finalResult += currentBlock.TrimEnd() + "#$b#";
                    currentBlock = "";
                }

                if (currentBlock.Length > 0)
                {
                    currentBlock += " ";
                }
                currentBlock += word;
            }
            finalResult += currentBlock.TrimEnd();

            return finalResult;
        }

        protected override void cleanupBeforeExit()
        {
            if (Game1.keyboardDispatcher.Subscriber == _textBox)
            {
                Game1.keyboardDispatcher.Subscriber = null;
            }
            base.cleanupBeforeExit();
        }
    }
}