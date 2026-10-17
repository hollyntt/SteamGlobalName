# SteamGlobalName

SteamGlobalName automatically uses your Steam username as your ATLYSS global name. When enabled, it also updates the global-name labels shown for other players to use their Steam usernames.

> **Deprecation notice:** SteamGlobalNickname is deprecated and will disable itself on October 31, 2026 (UTC), removing its Harmony patches. Uninstall this mod and use [RPNames](https://thunderstore.io/c/atlyss/p/s0apy/RPNames/) instead.

[**Join the Discord Community**](https://discord.gg/ePhX4Fb2we)

## Features

- **Automatic character name** — Fills the nickname field with your Steam username in the character select menu.
- **Steam names in-game** — Replaces player global-name labels with their Steam usernames.
- **Configurable** — Turn the feature on or off in the mod settings or configuration file.
- **Client-side** — Changes are made locally; the mod does not change server settings.

## Installation

Install with a compatible mod manager or manually. The mod requires:

- BepInEx 5
- ATLYSS
- [Nessie.ATLYSS.EasySettings](https://thunderstore.io/c/atlyss/p/Nessie/EasySettings/) (optional; enables the in-game settings toggle)

## Configuration

The **Use Steam Name** setting is available in the mod's EasySettings tab when EasySettings is installed. It can also be changed in `BepInEx/config/com.SteamGlobalName.sftwre.cfg`.

| Setting | Default | Description |
| --- | --- | --- |
| `Use Steam Name` | `true` | Use Steam usernames for the character nickname field and in-game global-name labels. When disabled, the mod restores the original label text it recorded. |

## Notes

- Steam must be running for Steam usernames to be available.
- Player Steam names may not appear until Steam provides the player's persona information.
- EasySettings is an optional dependency; without it, configure the setting in the `.cfg` file.

## Credits

- s0apy — Author
