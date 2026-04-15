import re
from datetime import datetime

with open('JULES_NOTES.md', 'r') as f:
    content = f.read()

date_str = datetime.now().strftime("%Y-%m-%d")

new_entry = f"""
### {date_str} - Audited VoiceInteractionManager.cs for Production
- **Completed**: Cleaned up `VoiceInteractionManager.cs` to remove debug/trace logs and testing comments.
- **Completed**: Ensured that the logic for restoring NPC facing direction (`_originalFacingDirection`) is correct.
- **Completed**: Verified and enforced rhythmic and dynamic paging logic (`_currentBubbleDelay = Math.Max(2.0, fragment.Length * 0.07)` and `maxLength = 45`).
- **Completed**: Verified state shielding functionality (`_isWaitingForApi` and `_speechQueue.IsEmpty`) to prevent 15s timeout interruptions.
"""

if "## Completed Actions" in content:
    content = content.replace("## Completed Actions", "## Completed Actions" + new_entry)
else:
    content += "\n## Completed Actions" + new_entry

with open('JULES_NOTES.md', 'w') as f:
    f.write(content)
