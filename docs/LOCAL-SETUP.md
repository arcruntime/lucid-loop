# Open the demo and save your API key once

## Open the right Unity project

1. Open Unity Hub. Choose **Projects → Add → Add project from disk** (or **Open**, depending on Hub version).
2. Select this folder exactly: `/Users/kellymartin/Projects2/dev/lucid-loop/mvp-checkpoint/Unity`.
3. Open it with Unity **6000.3.24f1**. Do **not** select its `My project` subfolder; that is a separate empty project.
4. In Unity's Project panel, open **Assets → Gyms → Scenes**.
5. Double-click **BeforeTheDrop**. Press **Play** at the top. `SampleScene` is not this demo.
6. For recording, click **Show AI observer** near the top. Click again to hide it. The panel shows real conversation decisions and labels scripted events separately.

## Save your key once — no code to paste

1. Open Spotlight (**Command–Space**), type **Keychain Access**, and open it. If macOS offers to open Passwords instead, choose **Open Keychain Access**.
2. Select the **login** keychain in the sidebar.
3. Choose **File → New Password Item** (usually **Command–N**).
4. Fill in these three fields exactly:
   - **Keychain Item Name:** `BeforeTheDrop/OpenAI`
   - **Account Name:** `openai`
   - **Password:** paste your OpenAI API key here.
5. Click **Add**. Do not paste the key into PM notes, Unity, or a script.

## Start testing each time

1. In Finder, press **Command–Shift–G**, paste `/Users/kellymartin/Projects2/dev/lucid-loop/mvp-checkpoint/tools`, and press Enter.
2. Double-click **start-dialogue.command**. It opens in Terminal.
3. If macOS asks permission to read `BeforeTheDrop/OpenAI`, check that this is the item you just created. Enter your Mac login password if requested. **Allow** permits this use; **Always Allow** avoids repeated access prompts for that requesting tool, but grants it ongoing access to this item.
4. Leave the server Terminal open. Return to Unity and play **BeforeTheDrop**.
5. After the first rewind, approach a character and choose **Start voice**. Wear headphones. Ren uses the music buttons.

If Terminal says the server is already running and ready, simply return to Unity. If it still asks you to paste the API key, check that the Keychain item name and account match step 4 exactly and that you allowed access. A manually pasted fallback key is used only for that session; it is not automatically saved.

The secret is stored by macOS Keychain. The launcher reads it into the local server's environment. This document contains only the item's name, not the key. No key is bundled into Unity or written into Git files. Saving the Keychain item is a user step; it has not been performed automatically.
