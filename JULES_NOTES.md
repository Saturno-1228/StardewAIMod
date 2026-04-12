# Stardew Living Valley - Jules Notes

**Fecha de Inicio:** Abril 2024
**Proyecto:** Stardew Living Valley (Mod de IA y Voice-to-Text para Stardew Valley)
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
   *(Nota: `ModBuildConfig` buscará automáticamente la ruta de tu instalación de Stardew Valley y copiará el mod generado directamente en la carpeta `Mods/StardewLivingValley`).*

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
  - Creación de archivo de proyecto `StardewLivingValley.csproj` configurado para .NET 6.0 y SMAPI.
  - Creación de `ModEntry.cs` con el punto de entrada y logs iniciales.
  - Creación de `ModConfig.cs` y `SecretConfig.cs`.

---

## 📌 TAREAS PENDIENTES (Siguientes Fases)
- [ ] Compilar el proyecto en local y verificar que carga exitosamente en la consola SMAPI mostrando "Stardew Living Valley loaded".
- [ ] Fase 1: Implementar el sistema de UI y atajos para Voice-to-Text (Push to Talk).
- [ ] Fase 2: Integrar y probar Whisper.net de forma offline.
- [ ] Fase 3: Integrar la comunicación con Venice API y el sistema PromptBuilder.
- [ ] Fase 4: Implementar sistema de "ConversationHistory" e inyección de memorias diarias.

---

## 🔮 MASTER DESIGN DOCUMENT: VISIÓN FUTURA Y ARQUITECTURA

Este apartado compila y refina todas las metas arquitectónicas y mecánicas discutidas en nuestras sesiones de "brainstorming". Este es el plano definitivo para guiar el desarrollo de las futuras versiones de "Stardew Living Valley".

### 1. Sistema Híbrido de Interfaz y Manejo del Tiempo (UI/UX)
**Objetivo:** Evitar que el jugador sufra interrupciones abruptas por la latencia de las llamadas a API (Whisper/Venice).
- **Interacción Casual (Burbujas en tiempo real):** Al usar Push-to-talk de paso, el juego **NO se pausa**. El jugador sigue farmeando. La respuesta se mostrará usando `SpeechBubbles` flotantes sobre la cabeza del NPC.
  - *Manejo de Textos Largos:* Si la IA genera un párrafo extenso, el código C# cortará la cadena usando los puntos finales (`.`) y rotará las burbujas cada ~3 segundos para dar tiempo a leer.
- **Conversación Profunda (Lock-in):** Si el jugador "interactúa" formalmente (clic derecho) con el NPC para charlar, se usará el `DialogueBox` clásico (con los retratos grandes). **Aquí el juego sí se pausa** permitiendo charlas profundas e inmersivas.

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
- **Control de Movimiento del NPC (Halt & Face):** Interrumpir el `PathFindController` de un NPC o sobrescribir su horario para que se detengan a hablar puede romper su ciclo diario permanentemente.
  - *Solución confirmada:* En lugar de destruir rutinas, utilizaremos la propiedad nativa `npc.movementPause = 5000;` (5 segundos) renovada periódicamente (ej. cada 30 ticks mediante `UpdateTicked`) mientras el jugador mantiene presionado el Push-to-Talk. Esto detiene al NPC de forma segura y se auto-resuelve sin causar bugs en el código base del juego.
