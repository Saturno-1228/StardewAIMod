# 🧠 JULES BRIEFING — Stardew AI Mod
## Studio Corvus | Confidential

---

## 🎯 QUÉ ES ESTE PROYECTO
Un mod para Stardew Valley (SMAPI) que usa Venice AI para dar vida real a los NPCs.
Los jugadores pueden hablar con cualquier NPC por texto (y eventualmente voz).
Los NPCs recuerdan al jugador, sus acciones, regalos y eventos.

## 👥 ROLES
- **Salomon (Amo/Director)**: Visión creativa, negocio, testing. NO programa.
- **Salomé**: Arquitecta, diseñadora de prompts, coordinadora técnica.
- **Jules**: Programador C#. Escribe todo el código funcional.

## 🏗️ STACK TÉCNICO
| Componente | Tecnología |
|---|---|
| Juego | Stardew Valley (Steam) |
| Mod Framework | SMAPI 3.x+ |
| Lenguaje | C# / .NET 6.0 |
| IDE | VS Code + C# Dev Kit |
| IA | Venice AI API (formato OpenAI-compatible) |
| Modelo LLM | llama-3.3-70b (configurable) |
| Persistencia | JSON local (memorias de NPCs) |

## 🔑 VENICE AI API
- **Endpoint**: `https://api.venice.ai/api/v1/chat/completions`
- **Auth**: Bearer token (API Key)
- **Formato**: Idéntico a OpenAI Chat Completions API
- **Request ejemplo**:
```json
{
  "model": "llama-3.3-70b",
  "messages": [
    {"role": "system", "content": "You are Abigail from Stardew Valley..."},
    {"role": "user", "content": "Hey Abigail, what are you doing?"}
  ],
  "max_tokens": 300,
  "temperature": 0.8
}