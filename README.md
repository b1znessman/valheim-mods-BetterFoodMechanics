# BetterFoodMechanics


Take control of how food works in Valheim. Make food last longer, decide whether its buffs decay, and let your food timer freeze while you're sheltered. Can be used on dedicated servers with every setting **server-synced**.

- **Longer food** - Multiply how long all food lasts, from 1x to 5x.
- **Food buff degradation** - Toggle the health, stamina and eitr bonuses at full strength for the entire duration or keep it the vanilla way.
- **Customizable configs** - Set the minimum comfort level to trigger the effect. There's also a dynamic option if you prefer.
- **Honest HUD timer** - While your food is slowed, the timer on the HUD is adjusted to match, so it counts down at a natural pace instead of looking stuck.

This mod only affects **food**. Meads and potions are not touched, and the health regeneration you get from food works exactly like vanilla.

## Installation

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
2. Install BetterFoodMechanics with your mod manager, or drop `BetterFoodMechanics.dll` into `BepInEx/plugins/`.
3. Launch the game once to generate the config file in `BepInEx/config/`.

### Multiplayer / dedicated servers

Install the mod on **both** the server and every client. Jotunn must be installed on both sides too. The server's config values are sent to players when they join, so players can't override them locally.

## Config

The config file is `biznessman.betterfoodmechanics.cfg`. All settings are synced from the server.

### 1 - General

| Setting | Default | Options | Description |
|---|---|---|---|
| `Enabled` | `On` | `On` / `Off` | Turns the mod on or off. |

### 2 - Cozyness

| Setting | Default | Range | Description |
|---|---|---|---|
| `DegradationMode` | `0` | `0` - `2` | How food behaves while you're comfortable (see below). |
| `MinimumComfort` | `10` | `5` - `25` | Comfort level needed for modes 0 and 1 to apply. Not used in mode 2. |

**Degradation modes**

| Mode | Name | What happens |
|---|---|---|
| `0` | Frozen | Food timer and buffs stop completely. |
| `1` | Slow | Food ticks at half speed, so it lasts twice as long. |
| `2` | Scaled | The more comfort you have, the slower your food decays. Ignores `MinimumComfort`. |

**Mode 2 speeds**

| Comfort level | Food decay speed |
|---|---|
| Below 10 | Normal |
| 10 - 14 | 1/2 speed |
| 15 - 19 | 1/3 speed |
| 20 or more | 1/4 speed (maximum) |

When your comfort drops below what's needed, food goes back to decaying at the normal rate.

### 3 - Food

| Setting | Default | Range | Description |
|---|---|---|---|
| `FoodDurationMultiplier` | `1` | `1` - `5` | How much longer food lasts. `2` means a 30 minute food lasts 60. |
| `FoodBuffDegrades` | `On` | `On` / `Off` | **On:** food buffs (health, stamina, eitr) fade gradually over the full duration. **Off:** buffs stay at full strength until the food runs out. |

With a multiplier above 1, the buff fade is spread across the full, longer duration, and you can eat the same food again once less than half of its duration is left, just like vanilla.

## Credits
This mod is inspired by blacks7ar's FoodDurationMultiplier with additional tweaks.
Also from another game that I played :)
