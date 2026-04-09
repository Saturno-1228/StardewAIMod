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
    /// Interfaz interactiva de chat con el NPC.
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
        private List<ChatMessage> _conversationHistory = new List<ChatMessage>();
        private string _npcResponse = "";
        private bool _isWaitingForResponse = false;
        private string _errorMessage = "";

        public ChatMenu(NPC npc, VeniceApiService veniceApi, MemoryService memoryService, ModConfig config)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 300, 800, 600, showUpperRightCloseButton: true)
        {
            _npc = npc;
            _veniceApi = veniceApi;
            _memoryService = memoryService;
            _config = config;
            _promptBuilder = new PromptBuilder();

            // Configurar TextBox
            Texture2D textBoxTexture = Game1.content.Load<Texture2D>("LooseSprites\\textBox");
            _textBox = new TextBox(textBoxTexture, null, Game1.dialogueFont, Game1.textColor)
            {
                X = this.xPositionOnScreen + 50,
                Y = this.yPositionOnScreen + this.height - 100,
                Width = this.width - 200,
                Height = 50,
                Selected = true
            };
            Game1.keyboardDispatcher.Subscriber = _textBox;

            // Botón enviar
            _sendButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 130, this.yPositionOnScreen + this.height - 100, 64, 64),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64), // Icono de flecha
                1f
            );

            // Mensaje de bienvenida inicial (local, sin gastar API)
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = $"* {_npc.Name} is listening... *" });
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
                base.receiveKeyPress(key);
            }
        }

        private async void SendMessage()
        {
            string playerText = _textBox.Text.Trim();
            if (string.IsNullOrEmpty(playerText)) return;

            // Añadir mensaje del jugador a la UI
            _conversationHistory.Add(new ChatMessage { Role = "player", Content = playerText });
            _textBox.Text = "";
            _isWaitingForResponse = true;
            _npcResponse = "Thinking...";

            try
            {
                // Construir Contexto
                var memory = _memoryService.GetMemory(_npc.Name);

                // Actualizar datos en vivo del jugador
                memory.PlayerName = Game1.player.Name;
                memory.FriendshipHearts = Game1.player.getFriendshipHeartLevelForNPC(_npc.Name);

                var currentContext = new Dictionary<string, string>
                {
                    { "Season", Game1.currentSeason },
                    { "Day", Game1.dayOfMonth.ToString() },
                    { "Time", Game1.timeOfDay.ToString() },
                    { "Weather", Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny") },
                    { "Location", Game1.currentLocation.Name }
                };

                string systemPrompt = _promptBuilder.BuildSystemPrompt(_npc.Name, memory, currentContext);

                // Llamar a Venice AI
                string reply = await _veniceApi.SendMessageAsync(systemPrompt, _conversationHistory);

                // Añadir respuesta a la UI
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = reply });

                // Guardar como memoria opcionalmente (simplificado por ahora)
                _memoryService.AddMemory(_npc.Name, $"Player said: {playerText}. I replied: {reply}", 1, "neutral");
            }
            catch (Exception ex)
            {
                _errorMessage = "Error connecting to AI.";
                Console.WriteLine(ex);
            }
            finally
            {
                _isWaitingForResponse = false;
                _npcResponse = "";
            }
        }

        public override void draw(SpriteBatch b)
        {
            // Fondo
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);

            // Título
            Utility.drawTextWithShadow(b, $"Chat with {_npc.Name}", Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 50, this.yPositionOnScreen + 50), Game1.textColor);

            // Historial de conversación (solo dibujamos los últimos 5 para que quepan)
            int yOffset = this.yPositionOnScreen + 100;
            int startIdx = Math.Max(0, _conversationHistory.Count - 5);

            for (int i = startIdx; i < _conversationHistory.Count; i++)
            {
                var msg = _conversationHistory[i];
                string prefix = msg.Role == "player" ? "You: " : $"{_npc.Name}: ";
                Color color = msg.Role == "player" ? Color.Blue : Game1.textColor;

                // Truncar texto largo para propósitos visuales (esto se podría mejorar con un word wrap real)
                string displayTxt = Game1.parseText(prefix + msg.Content, Game1.smallFont, this.width - 100);

                Utility.drawTextWithShadow(b, displayTxt, Game1.smallFont, new Vector2(this.xPositionOnScreen + 50, yOffset), color);
                yOffset += (int)Game1.smallFont.MeasureString(displayTxt).Y + 10;
            }

            // Estado de carga o error
            if (_isWaitingForResponse)
            {
                Utility.drawTextWithShadow(b, _npcResponse, Game1.smallFont, new Vector2(this.xPositionOnScreen + 50, yOffset), Color.Gray);
            }
            if (!string.IsNullOrEmpty(_errorMessage))
            {
                 Utility.drawTextWithShadow(b, _errorMessage, Game1.smallFont, new Vector2(this.xPositionOnScreen + 50, yOffset), Color.Red);
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

        public override void cleanupBeforeExit()
        {
            Game1.keyboardDispatcher.Subscriber = null;
            base.cleanupBeforeExit();
        }
    }
}