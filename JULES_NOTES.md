# Living Companions Valley - Jules Notes

**Fecha de Inicio:** Abril 2026
**Proyecto:** Living Companions Valley (Mod de IA y Voice-to-Text para Stardew Valley)
**Autor:** Studio Corvus

Este documento sirve como registro oficial de desarrollo, historial de sesiones y guía de instrucciones para el flujo de trabajo entre Jules (IA) y el desarrollador local.

---

## 🚀 VISIÓN DEL PROYECTO
"Da vida real a los NPCs de Stardew Valley con conversaciones de IA, memoria persistente y sistema de voz interactivo".
- El objetivo final es integrar Venice API para lógica de NPCs, Whisper.net para transcripción de audio (Push-to-Talk) y memoria persistente inyectada en el contexto.

---

## 🛠️ INSTRUCCIONES DE CONFIGURACIÓN LOCAL (Para el Desarrollador)

Dado que este proyecto está construido como un Mod de SMAPI, **debes compilar el proyecto localmente en tu máquina**, ya que requieres los archivos del juego (`Stardew Valley.dll`, `StardewModdingAPI.dll`).

**Pasos a seguir en VS Code:**
1. Clona/Descarga este repositorio.
2. Abre la carpeta raíz del proyecto en VS Code.
3. Asegúrate de tener instalado el **SDK de .NET 6.0** (obligatorio para SMAPI).
4. Restaura las dependencias ejecutando en la terminal de VS Code:
   ```bash
   dotnet restore
   ```
5. Esto instalará automáticamente el paquete principal necesario (`Pathoschild.Stardew.ModBuildConfig` v4.1.1).
6. Compila el proyecto ejecutando:
   ```bash
   dotnet build
   ```
   *(Nota: `ModBuildConfig` buscará automáticamente la ruta de tu instalación de Stardew Valley y copiará el mod generado directamente en la carpeta `Mods/LivingCompanionsValley`).*

**En caso de error "Game Path Not Found":**
Si el paquete de build de SMAPI no detecta automáticamente la ubicación de tu juego, deberás crear un archivo en la raíz llamado `stardewvalley.targets` (o usar la variable de entorno de tu SO) y apuntarlo manualmente a la carpeta de tu juego. Ejemplo:
```xml
<Project>
  <PropertyGroup>
    <GamePath>C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley</GamePath>
  </PropertyGroup>
</Project>
```

**Archivos Sensibles a Crear Manualmente:**
- `SecretConfig.json` (o los que vayamos integrando) en tu entorno local. Estos están ignorados en el `.gitignore` por seguridad.

---

## 🗂️ ESTRUCTURA ACTUAL
Se ha implementado una estructura avanzada y escalable para futuras fases:
- `Assets/` (Audio, imágenes y Whisper models futuros)
- `Configuration/` (Clases ModConfig y SecretConfig)
- `Core/` (Núcleo de eventos y lógicas de partida)
- `Framework/` (Interfaces y extensiones base)
- `Models/` (Entidades como ChatMessage, NpcMemory, etc.)
- `Prompts/` (Plantillas y base de datos RAG)
- `Services/` (Servicios API, Whisper, Audio)
- `UI/` (Elementos de menús custom)

---

## 📝 HISTORIAL DE SESIONES

### ✅ Fase 0 (Inicialización de Estructura)
- **Estado:** COMPLETADO
- **Acciones Realizadas:**
  - Creación del archivo `.gitignore` estricto (excluyendo bin/obj y configuraciones secretas).
  - Creación de estructura de directorios escalable.
  - Creación del archivo `manifest.json`.
  - Creación de archivo de proyecto `LivingCompanionsValley.csproj` configurado para .NET 6.0 y SMAPI.
  - Creación de `ModEntry.cs` con el punto de entrada y logs iniciales.
  - Creación de `ModConfig.cs` y `SecretConfig.cs`.
  - **Prueba de compilación local exitosa:** Se resolvieron problemas de versión de C# (`LangVersion`) y se confirmó que el `dotnet build` empaqueta el mod en la máquina local exitosamente.

---

## 📌 TAREAS PENDIENTES (Siguientes Fases)

## 🎮 CAPACIDADES ACTUALES DEL MOD (Versión Actual)
Hasta el momento, si compilas e instalas el mod en el juego, esto es exactamente lo que hace:
1. **Atajo Configurable:** Monitorea la tecla `Tab` de tu teclado (o la que definas en el config.json generado al abrir el juego por primera vez).
2. **Push-To-Talk "Simulado":** Al mantener presionada la tecla, registra en la consola de SMAPI (modo Debug) un mensaje de inicio de captura de voz. Al soltarla, registra el fin.
3. **Escaneo de Entorno:** Cuando presionas la tecla, busca instantáneamente en un radio de 3 baldosas ("tiles") a tu alrededor.
4. **Filtro Inteligente:** Ignora animales, monstruos, mascotas, NPCs no descubiertos, y personajes especiales protegidos por historia (como el Bouncer del Casino o Mr. Qi).
5. **Inmersión Básica (Halt & Face):** Si encuentra un NPC válido (ej. Marnie), interrumpe su caminata o rutina (`Halt()`) y hace que se dé la vuelta para mirarte directamente a los ojos mientras "hablas".

### ✅ Fase 1: Sistema de Atajos y Preparación Voice-to-Text (Push to Talk)
- **Estado:** COMPLETADO
- **Acciones Realizadas:**
  - [x] **1.1 Arquitectura:** Se creó la clase `VoiceInteractionManager.cs` en la carpeta `Services/`.
  - [x] **1.2 Captura de Eventos Básica:** Se conectaron los eventos de SMAPI (`ButtonPressed` y `ButtonReleased`) para escuchar `VoiceKey` (SButton.Tab por defecto configurable en `ModConfig`).
  - [x] **1.3 Mock de Grabación (Logs):** Se implementaron mensajes "Iniciando captura de voz..." y "Captura finalizada." con `LogLevel.Debug`.
  - [x] **1.4 Filtro y Búsqueda de NPC:** Se añadió la búsqueda en un radio de 3 tiles. Validación nativa: Es aldeano, puede socializar, y **NO** está en una `NpcBlacklist` explícita (Se removió la necesidad de haber hablado con él primero con el clic derecho).
  - [x] **1.5 Halt & Face Nativo (Push-To-Talk y Pausa de Rutina):** Se corrigió la lógica de "congelamiento". Ahora, al presionar TAB, se llama a `npc.Halt()` y se aplica un único `movementPause` para que el motor de rutinas no se buggee, eliminando el refresco constante que los dejaba paralizados.
  - [x] **1.6 Condición de Salida y Liberación Limpia:** Al expirar los 15 segundos o alejarse 6 tiles, se llama al método `ReleaseTargetNpc`, que simplemente resetea la pausa `movementPause = 0` para garantizar que el motor retome su horario con normalidad de manera compatible.

### ⏳ Fases Futuras
- [ ] Fase 2: Integrar y probar Whisper.net de forma offline (reemplazar logs por captura real del `Microphone`).
- [ ] Fase 3: Integrar la comunicación con Venice API y el sistema PromptBuilder.
- [ ] Fase 4: Implementar sistema de "ConversationHistory" e inyección de memorias diarias.

---

## 🔮 MASTER DESIGN DOCUMENT: VISIÓN FUTURA Y ARQUITECTURA

Este apartado compila y refina todas las metas arquitectónicas y mecánicas discutidas en nuestras sesiones de "brainstorming". Este es el plano definitivo para guiar el desarrollo de las futuras versiones de "Living Companions Valley".

### 1. Sistema Híbrido de Interfaz y Manejo del Tiempo (UI/UX)
**Objetivo:** Evitar que el jugador sufra interrupciones abruptas por la latencia de las llamadas a API (Whisper/Venice).
- **Interacción Casual (Burbujas en tiempo real - Push-To-Talk con TAB):** Al usar Push-to-talk de paso, el juego **NO se pausa**. El NPC interrumpe su rutina usando `movementPause`. Al soltar TAB, aparecerán puntitos animados (`...`) en una pequeña burbuja indicando "pensamiento". La respuesta se mostrará usando `SpeechBubbles` flotantes.
  - *Manejo de Textos Largos (Paginación):* Si la IA genera un párrafo extenso, el código C# cortará la cadena usando los puntos finales (`.`) y rotará las burbujas secuencialmente de forma fluida.
  - *Condición de Salida Orgánica:* El NPC no se irá inmediatamente después de la última burbuja. Solo se irá si el jugador explícitamente se despide, se aleja (>6 tiles), o si se le ordena explícitamente "esperar".
- **Conversación Profunda (Lock-in - Clic Derecho):** Si el jugador "interactúa" formalmente con el NPC, se usará el `DialogueBox` clásico (con los retratos grandes). **Aquí el juego sí se pausa** permitiendo charlas profundas e inmersivas, inyectando puntitos de "..." animando dentro de la caja nativa mientras se espera a la IA.

### 2. Arquitectura de Machine Learning Local (Offline & Rápida)
**Objetivo:** Reducir gasto de tokens en Venice API, acelerar respuestas y permitir lógicas complejas offline.
*Implementación Técnica:* Utilizar C# ML.NET o ONNX Runtime integrados en el mod.
- **RAG (Retrieval-Augmented Generation) Local:** Convertiremos las memorias en vectores (Embeddings con un modelo muy ligero como `all-MiniLM`). Al hablar, el sistema local encuentra rápidamente qué "recuerdo" inyectar al prompt de Venice.
- **Motor de Emoción Instantáneo:** Un micro-modelo NLP evalúa si lo que dijiste es un insulto o halago en milisegundos, alterando la amistad *antes* de que Venice decida la respuesta textual.
- **Clasificador de Favores:** Un modelo pequeño detecta la intención oculta en la charla (ej. "consígueme madera") y dispara el evento en el código del juego.

### 3. Sistemas de Personalidad, Lore y Dinámicas Sociales
**Objetivo:** Que ningún NPC se sienta igual a otro.
- **Perfiles Profundos:** Marnie y Haley no usarán el mismo Prompt. Sus gustos, secretos y profesiones se inyectan en su base.
- **Red de Rumores Probabilísticos:** Si ocurre un gran evento (te casas con Haley), Emily (su hermana) tiene un 95% de enterarse ese día. Krobus tiene un 0%. Al día siguiente, los NPCs con la información te abordarán sobre el tema.
- **Multiplicadores Dinámicos:** Sebastian (introvertido) ganará menos puntos de amistad por charla que Sam (extrovertido), requiriendo más constancia para llegar a los 10 corazones.
- **Moralidad y Precios (Consecuencias):** Tratar mal a todos los aldeanos bajará tu "Moral Global". Esto hará que la probabilidad de que te regalen cosas sea 0, e incluso podemos engancharnos al código de las tiendas para que Pierre te suba los precios un 20%.

### 4. Nuevas Mecánicas de Inmersión Sensorial
- **Reacciones al Volumen de Voz:** Ya que usamos Whisper localmente, el código puede medir los decibelios. Susurrarle a un NPC lo hace reaccionar diferente a si le gritas.
- **Curación Contextual y Activa:** El NPC lee tu `Game1.player.health`. Si te ven desmayándote después de la mina, existe la posibilidad de que no solo hablen, sino que el código te inyecte un objeto curativo de temporada directamente a tu inventario.
- **Retratos Nativos Inteligentes:** El ML deducirá la emoción de la respuesta y el código inyectará comandos nativos de Stardew Valley (como `$h` para sonreír) en el texto. Así, el rostro del NPC cambiará fluidamente en el `DialogueBox` sin tener que crear UI personalizada.
- **Abandono y Rutinas Emocionales:** Si dejas al NPC hablando solo y te vas, se ofenderá. Si le rompes el corazón, el mod sobrescribirá temporalmente su `Schedule` (rutina) para que ese día se quede encerrado en su cuarto en vez de ir a la taberna.
- **Sistema de Compañeros Temporal:** Puedes pedirles que te acompañen a la granja. Usaremos rutas temporales, pero si llega su hora de trabajar, abandonarán automáticamente el modo compañero para no romper el juego.

---

***
**💡 NOTA PARA LA IA EN FUTURAS SESIONES:** 
Soy tu usuario. Revisa continuamente esta sección "Master Design Document" antes de generar código. Me gusta la inmersión, el balance y no romper el gameplay core de Stardew. Antes de diseñar una nueva característica técnica, pregúntame siempre: *"¿Crees que esta forma rompe la inmersión? ¿Se te ocurre una manera más fluida de integrarlo al juego?"*

## 🐛 PROBLEMAS CONOCIDOS / NOTAS TÉCNICAS
- **Error CS1617 al compilar (`LangVersion 12`)**: Se solucionó cambiando la versión de C# de `12` a `10` en el `.csproj` ya que el SDK local de .NET 6 no soporta C# 12. Se reactivó la variable de SMAPI `EnableModDeploy` en el archivo de proyecto para asegurar el autodespliegue.

## 2026-04-12 - Solución a Error de Carga de DLL (Cambio de Nombre)
- Se cambió el nombre de todo el proyecto de `StardewAIMod` (y la transición temporal de `Stardew Living Valley`) al nombre oficial y final: **`Living Companions Valley`**.
- Se renombró el archivo `.csproj` y las configuraciones de `manifest.json`.
- Se instruyó al usuario que elimine la carpeta de mods antigua (`StardewAIMod` / `Stardew Living Valley`) para prevenir colisiones de nombres de DLL en la carga de SMAPI.

## 2026-04-12 - Solución de Conflicto de Versión de Assembly en .NET 10
- Cuando se compilaba el mod con el SDK más reciente de .NET (por ejemplo, `10.0.0.0`), SMAPI (que corre en .NET 6.0) rechazaba cargar el mod por un `FileLoadException: Could not load file or assembly 'System.Text.Encodings.Web, Version=10.0.0.0'`.
- Se añadieron dependencias explícitas en `LivingCompanionsValley.csproj` para forzar las versiones `6.0.0` de `System.Text.Json` y `System.Text.Encodings.Web`, asegurando compatibilidad nativa con Stardew Valley y SMAPI.

## 2026-04-12 - Solución de Conflicto de Versión NU1605 con System.Text.Json
- Después de forzar la versión `6.0.0` de `System.Text.Json` para compatibilidad con SMAPI, el compilador arrojó el error `NU1605 (Package downgrade)` porque `Whisper.net v1.9.0` introdujo una nueva dependencia transitiva (`Microsoft.Extensions.AI.Abstractions v10.0.0`) que exige obligatoriamente `System.Text.Json >= 10.0.0`.
- Para resolver este choque arquitectónico sin romper SMAPI, se bajó la versión de `Whisper.net` y `Whisper.net.Runtime` de la `1.9.0` a la `1.8.1`, la cual no tiene esta dependencia transitiva de .NET 10 y es perfectamente compatible con `.NET 6.0` nativo.

## 2026-04-12 - Solución de Movimiento y Validación Rápida en VoiceInteractionManager
- Se arregló un error donde los NPCs continuaban caminando (rompiendo la interacción) o se quedaban congelados de forma permanente.
- Para detener a un NPC sin romper su 'schedule' o animaciones especiales (como estar sentado), se verifica si el NPC tiene una animacion especial en curso para omitir `Halt()`. En lugar de manipular la velocidad, se utiliza `movementPause` renovado inteligentemente en `UpdateTicked` solo mientras dura la interaccion, evitando que los pies del NPC se muevan.
- Se añadió una validación ('Debounce') en el evento del teclado `OnButtonReleased`: si los datos del buffer de audio ocupan menos de `16000 bytes` (aprox. `0.5 segundos` en 16Hz/16bit), la interacción se aborta inmediatamente y el NPC es liberado. Esto previene interacciones fantasma o envíos innecesarios a la IA por presiones accidentales a la tecla rápida.

## 2026-04-12 - Solución a fallas de micrófono silenciosas y congelamiento
- Se detectó a través de los logs que la IA no respondía porque `Microphone.Default` devolvía nulo (el juego no detectaba micrófono), lo que causaba un salto silencioso del procesamiento. Se añadieron validaciones críticas para abortar la interacción y notificar al usuario (HUD y Consola) si no hay micrófono.
- Los logs asíncronos de Whisper y Venice se elevaron a `LogLevel.Info` para asegurar visibilidad en la consola predeterminada de SMAPI.
- Se implementó la solución definitiva para el control de movimiento: usar `npc.freezeMotion = true` (y `false` al terminar). Esto congela completamente al NPC y sus pies sin corromper el Schedule (como lo hace `movementPause`) y sin bugs visuales (como lo hace `speed=0`).
