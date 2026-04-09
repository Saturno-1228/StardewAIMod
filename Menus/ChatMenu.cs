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
    /// <summary>
    /// Interfaz interactiva para escribirle al NPC.
    /// </summary>
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

        public ChatMenu(NPC npc, VeniceApiService veniceApi, MemoryService memoryService, ModConfig config, string modDirectory)
            : base(12, Game1.uiViewport.Height - 300, 800, 200, showUpperRightCloseButton: true)
        {
            _npc = npc;
            _veniceApi = veniceApi;
            _memoryService = memoryService;
            _config = config;
            _modDirectory = modDirectory;
            _promptBuilder = new PromptBuilder(modDirectory);

            // Configurar TextBox (estilo chat multijugador)
            Texture2D textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
            _textBox = new TextBox(textBoxTexture, null, Game1.dialogueFont, Game1.textColor)
            {
                X = this.xPositionOnScreen + 20,
                Y = this.yPositionOnScreen + 80,
                Width = this.width - 120,
                Height = 50,
                Selected = true
            };
            Game1.keyboardDispatcher.Subscriber = _textBox;

            // Botón enviar
            _sendButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 80, this.yPositionOnScreen + 70, 64, 64),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64), // Icono de flecha
                1f
            );
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            _textBox.Update();

            if (_sendButton.containsPoint(x, y) && !_isWaitingForResponse)
            {
                SendMessage();
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Enter && !_isWaitingForResponse)
            {
                SendMessage();
            }
            else if (key == Keys.Escape)
            {
                exitThisMenu();
            }
            else
            {
                // Solo llamar a base.receiveKeyPress si el TextBox NO está activo
                if (!_textBox.Selected)
                {
                    base.receiveKeyPress(key);
                }
            }
        }

        private async void SendMessage()
        {
            string playerText = _textBox.Text.Trim();
            if (string.IsNullOrEmpty(playerText)) return;

            _textBox.Text = "";
            _isWaitingForResponse = true;

            try
            {
                // Construir Contexto
                var memory = _memoryService.GetMemory(_npc.Name);

                // Actualizar datos en vivo del jugador y relación
                memory.PlayerName = Game1.player.Name;
                memory.FriendshipHearts = Game1.player.getFriendshipHeartLevelForNPC(_npc.Name);

                string relationship = "Stranger";
                if (Game1.player.spouse == _npc.Name) {
                    relationship = "Spouse";
                } else if (Game1.player.friendshipData.ContainsKey(_npc.Name)) {
                    var friendship = Game1.player.friendshipData[_npc.Name];
                    if (friendship.IsDating()) {
                        relationship = "Boyfriend/Girlfriend";
                    } else if (memory.FriendshipHearts >= 8) {
                        relationship = "Best Friend";
                    } else if (memory.FriendshipHearts >= 4) {
                        relationship = "Friend";
                    } else if (memory.FriendshipHearts > 0) {
                        relationship = "Acquaintance";
                    }
                }
                memory.RelationshipStatus = relationship;

                // Información del entorno y ropa
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


                // Datos extra: Objeto en mano, cumpleaños, y estado del jugador
                string holdingItem = Game1.player.ActiveObject != null ? Game1.player.ActiveObject.DisplayName : "Nothing";
                bool isBirthday = _npc.isBirthday(Game1.currentSeason, Game1.dayOfMonth);
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

                string systemPrompt = _promptBuilder.BuildSystemPrompt(_npc.Name, memory, currentContext);

                // Añadir el mensaje actual al historial
                _memoryService.AddToConversationHistory(_npc.Name, "player", playerText);

                // Llamar a Venice AI
                string reply = await _veniceApi.SendMessageAsync(systemPrompt, memory.ConversationHistory);

                // Guardar respuesta de la IA en historial
                _memoryService.AddToConversationHistory(_npc.Name, "assistant", reply);

                // Consecuencias de la charla (Amistad dinámica basada en comandos de emoción)
                if (reply.Contains("$h") || reply.Contains("$l"))
                {
                    Game1.player.changeFriendship(10, _npc); // +10 puntos (aprox 1/25 de corazón)
                }
                else if (reply.Contains("$a"))
                {
                    Game1.player.changeFriendship(-10, _npc); // -10 puntos
                }

                // Formatear la respuesta para que las oraciones largas se dividan correctamente
                // usando el comando de pausa/salto de diálogo de Stardew Valley: #$b#
                string formattedReply = FormatDialogueText(reply);

                // Configurar que una vez termine el diálogo se vuelva a abrir el ChatMenu
                Game1.afterDialogues = new Game1.afterFadeFunction(() =>
                {
                    // Evitar que se abra si estamos haciendo otra cosa o en otro menú principal
                    if (Game1.activeClickableMenu == null)
                    {
                        Game1.activeClickableMenu = new ChatMenu(_npc, _veniceApi, _memoryService, _config, _modDirectory);
                    }
                });

                // Show response via standard dialogue box!
                _npc.CurrentDialogue.Push(new Dialogue(_npc, null, formattedReply));
                Game1.drawDialogue(_npc);

                // Exit this custom chat menu since we show the standard dialogue
                this.exitThisMenu();
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

        public override void draw(SpriteBatch b)
        {
            // Fondo
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);

            // Título
            Utility.drawTextWithShadow(b, $"Say something to {_npc.Name}...", Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 50, this.yPositionOnScreen + 40), Game1.textColor);

            // Estado de carga o error
            if (_isWaitingForResponse)
            {
                Utility.drawTextWithShadow(b, "Thinking...", Game1.smallFont, new Vector2(this.xPositionOnScreen + 50, this.yPositionOnScreen + 140), Color.Gray);
            }
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                 Utility.drawTextWithShadow(b, _errorMessage, Game1.smallFont, new Vector2(this.xPositionOnScreen + 50, this.yPositionOnScreen + 140), Color.Red);
            }

            // TextBox y Botón
            _textBox.Draw(b);
            if (!_isWaitingForResponse)
            {
                _sendButton.draw(b);
            }

            // Puntero del mouse
            drawMouse(b);

            base.draw(b);
        }

        private string FormatDialogueText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Enfoque basado en longitud: inserta #$b# si un bloque supera los 150 caracteres
            string[] words = text.Split(' ');
            string currentBlock = "";
            string finalResult = "";
            int charCount = 0;

            foreach (var word in words)
            {
                if (charCount + word.Length > 150)
                {
                    finalResult += currentBlock.TrimEnd() + "#$b#";
                    currentBlock = "";
                    charCount = 0;
                }
                currentBlock += word + " ";
                charCount += word.Length + 1;
            }
            finalResult += currentBlock.TrimEnd();

            return finalResult;
        }

        protected override void cleanupBeforeExit()
        {
            Game1.keyboardDispatcher.Subscriber = null;
            base.cleanupBeforeExit();
        }
    }
}
