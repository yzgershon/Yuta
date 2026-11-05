# Yuta × GatedSpace: proposed connection

This is a proposal, not an implemented connection. GatedSpace already has agent lifecycle and session status events: working, attention, completed, and error, with workspace/session/pane identifiers. Those provide a useful starting point without reading terminal output or inventing progress percentages.

| Feature | In Yuta | Click action |
| --- | --- | --- |
| Needs you | A brief agent preview: question or permission waiting | Open the exact GatedSpace pane |
| Ready | Agent name, task title, and completion state | Open the completed turn |
| Working | A quiet activity count, such as 2 agents working | Open a compact session picker |
| Build finished | Ready / failed with the actual build result | Open build details or its artifact |
| Send selection | An explicit action on selected clipboard text | Choose an agent and review before sending |
| Focus session | Start a Darch focus session while an agent runs | Open Focus or return to the agent |

Start with **Needs you + Ready + exact-pane navigation**. A brief preview is useful while working elsewhere and fits the user's preference for no notification inbox. While Yuta is flattened or hidden, preserve that state; do not raise it merely because an agent has an update. Returning deliberately can show a small attention indicator.

Use a user-enabled local connection with a versioned event contract, authenticated current-user transport, event IDs, timestamps, and session/pane targets. Never treat an event's arbitrary URL or command as executable. Allow only explicit focus actions handled by GatedSpace. Deduplicate native Windows notifications so one completion does not appear twice. Keep message contents out of persisted logs and let the user choose which agent events can appear.

Permissions and sensitive approvals should continue to be resolved in GatedSpace, where the full action and context are visible. Yuta can take the user there; it should not turn a tiny notification into an accidental approval button.
