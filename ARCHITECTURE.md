StudioCorvus/StardewAIMod/
│
├── manifest.json
├── StardewAIMod.csproj
├── ModEntry.cs               ⬅️ ACTUALIZADO
├── ModConfig.cs              ⬅️ ACTUALIZADO (sin secretos)
├── SecretConfig.cs           ⬅️ NUEVO
│
├── secrets.example.json      ⬅️ NUEVO (plantilla para GitHub)
├── .gitignore                ⬅️ ACTUALIZADO
│
├── Services/
│   ├── VeniceApiService.cs
│   ├── WhisperTranscriptionService.cs ⬅️ NUEVO
│   ├── VoiceInteractionManager.cs
│   ├── AudioRecorder.cs
│   ├── MemoryService.cs
│   └── PromptBuilder.cs
│
├── Models/
│   ├── NpcMemory.cs
│   └── ChatMessage.cs
│
├── Prompts/
│
├── JULES_BRIEFING.md
└── ARCHITECTURE.md