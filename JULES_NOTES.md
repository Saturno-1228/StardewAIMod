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

## 🐛 PROBLEMAS CONOCIDOS / NOTAS TÉCNICAS
*(Vacío por ahora)*
