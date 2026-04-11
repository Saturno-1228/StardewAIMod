# 📝 JULES NOTES (Internal Dev Log)

## ✅ LO QUE HEMOS HECHO HASTA AHORA (Implementación Inicial)

1. **Estructura y Core (`ModEntry.cs`)**
   - Inicialización completa de `VeniceApiService` y `MemoryService` leyendo de `config.json` y `secrets.json`.
   - Sistema de persistencia funcional: carga de las memorias de los NPCs usando `OnSaveLoaded`.
   - Sistema de eventos enlazado: recolección de contexto diario (día, temporada, clima) en `OnDayStarted`.
   - Lógica de interacción en el mapa: detección de pulsación de la tecla de chat (configurable), detección del NPC frente al jugador usando `GetGrabTile()`, e invocación de la UI.
   - Protección de crash: validación de existencia de `VeniceApiKey` antes de intentar abrir el menú de chat.

2. **UI Interactiva (`ChatMenu.cs`)**
   - Creación de una interfaz `IClickableMenu` personalizada.
   - Textbox usando las texturas nativas del juego (`LooseSprites\textBox`) para escribir los prompts.
   - Inyección dinámica de datos del jugador en tiempo real en la memoria (`Name`, `FriendshipHearts`).
   - Envío asíncrono (`SendMessageAsync`) a la API para evitar bloquear el UI Thread de Stardew Valley.
   - Manejo básico de errores visuales e indicadores de "pensando...".
   - Limpieza segura de la suscripción del teclado en `cleanupBeforeExit()` para evitar bloqueos del input del jugador.

## 🚧 LO QUE FALTA (Deuda Técnica y Futuras Mejoras)

1. **Refinamiento de UI / Hilos (Thread Safety)**
   - *Detalle técnico:* Actualmente, `SendMessage()` utiliza `async void` (fire and forget) y actualiza variables como `_conversationHistory` desde un background thread que pertenece al ThreadPool. Como `draw(SpriteBatch)` de Stardew se ejecuta ~60 veces por segundo en el UI thread principal, esto podría lanzar raras excepciones `InvalidOperationException` si la lista se modifica justo cuando se está leyendo para dibujarse.
   - *Solución futura:* Implementar una cola concurrente (ConcurrentQueue) de acciones de actualización de UI, o bloquear/desbloquear los recursos en `Update(GameTime)` para sincronizar la respuesta de la IA en el thread de dibujado.

2. **Mejoras del Contexto e Inmersión**
   - Implementar el diseño final de los Prompts base escritos por Salomé (actualmente hay placeholders para las personalidades).
   - Word wrapping (ajuste de línea automático) para respuestas largas de la IA, ya que ahora mismo simplemente las truncamos un poco.
   - Hacer que las "Memories" se guarden en el archivo local cada fin del día o cuando se recibe una respuesta, no solo en memoria en ejecución.

3. **Funcionalidades de Voz (V2)**
   - Conectar Text-to-Speech o Speech-to-Text en caso de que la configuración `EnableVoice` sea activada, según diseño original.

## 🗣️ IMPLEMENTACIÓN DE WHISPER LOCAL (Migración de Voz)

- Se eliminó completamente la dependencia de Venice AI para la transcripción de audio.
- Se implementó `WhisperTranscriptionService` usando Whisper.net, el cual se ejecuta asíncronamente.
- Se mejoró `AudioRecorder` para convertir el audio grabado desde `16-bit PCM` al formato `float[]` de 32 bits a `16kHz` requerido por Whisper.net (con resampler simple incluido).
- Se implementó `Task.Run` con un `ConcurrentQueue<Action>` en `VoiceInteractionManager` para asegurar el hilo seguro (thread safety) y evitar crashes al actualizar la UI del juego nativa de Stardew Valley.
- Se eliminaron las configuraciones obsoletas de transcripción y se centralizaron en la ruta configurable `WhisperModelPath`.