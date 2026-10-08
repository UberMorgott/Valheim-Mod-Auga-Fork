### 2.1.5 - Almanac form modal styled without Unity errors

* The Almanac form modal is now Auga-styled on the copy Almanac creates (after AlmanacPanel.Start), not on its bundle prefab. Restyling the prefab tried to attach Auga's four panel corner ornaments to an asset, which Unity refuses (`Cannot instantiate objects with a parent which is persistent`, 4 errors per game start) and left the corners parentless in the scene.

### 2.1.4 - Barber: no error spam after logout

* The Auga barber panel only updates its hair/beard arrows while it is open and a player exists, like the vanilla barber. Before, it ran every frame and, after logging out to the main menu, threw a NullReferenceException each frame until the world closed.
* Hair/beard arrows are both set every frame, so jumping between the first and last style no longer leaves both arrows hidden.

### 2.1.3 - Ally shield on the overhead health bar

* Other players' absorb shield (Staff of Protection bubble, any vanilla SE_Shield) shows as a blue strip under their overhead health bar, filled by remaining / total absorb. Needs MorgottTweaks 1.46.0+ on the shielded player ([Multiplayer] ShareShieldAbsorb, player ZDO mt_shield / mt_shield_max): vanilla keeps the absorb only on the owner.
* The violet energy-shield strip stays right under the bar; the strips close up when one is absent. Strips are cached per HUD instead of looked up every frame.

### 2.1.2 - Epic Loot equipped and set-item marks

* Equipped and queued items show Auga's own marks again (blue / amber corner in the inventory, blue / amber slot on the hotbar); they had become an almost invisible dark backdrop. With Epic Loot they no longer turn into Epic Loot's frame.
* Epic Loot's set-item marker is Auga's corner mark, in the opposite corner, in the set colour.

### 2.1.1 - Epic Loot rarity on Auga slots

* Epic Loot rarity backgrounds (inventory, hotbar, recipe list and icon, Epic Loot panels and dialogs) use Auga's own slot outline in the rarity colour instead of Epic Loot's glow art.

### 2.0.13 - Almanac, main menu texts, IMGUI fonts

* Almanac's windows (main window, quest tracker, NPC dialogue, NPC editor, form) in Auga style.
* Main menu: BepInEx info and version texts in Auga's font; the version text no longer hides under the Mod Config button.
* Configuration Manager window and Build Camera badges use Auga's font.
* Input fields that mods draw with button art take Auga's input art.

### 2.0.12 - Vanilla side panels

* F2 connection panel, F5 console, feedback form, current-players list, manage-saves rows, achievement popup, credits, join code, captions and radial menu in Auga style.

### 2.0.11 - Epic Loot panels, tooltips

* Item tooltips (and Epic Loot's comparison tooltip) use Auga's tooltip frame and fonts.
* Epic Loot's welcome and config-update popups, socket/chisel confirmations, ability bar and debug text in Auga style.
* Epic Loot compendium pages and their search field in Auga style.
* HUD parts other mods add at startup get the Auga restyle too (BossAwakening's world boss bar excepted).

### 2.0.10 - Vanilla HUD parts

* Piece health, stagger, action progress and mount bars use Auga's bar art.
* Ship HUD in Auga's icons (wind circle, ship, wind, rudder wheel, speed chevrons).
* Key hint caps, event banner glow, save and bad-connection icons in Auga style.
* Hover text key tags ([E]) in Auga gold instead of yellow.
* Loading, sleeping and teleport screens use Auga's fonts.

### 2.0.9 - Button sizes, Epic Loot dialogs, quick slots

* Buttons take the Auga art made for their height (small, medium or fancy), and their labels grow to Auga's label size: Epic Loot's enchanting tab and mode labels, Jotunn's colour/gradient picker buttons and map-overlay button are readable now.
* Narrow input fields (Jotunn gradient picker) keep room for their text.
* Text in Unity's default font (Arial) inside restyled windows takes Auga's body font.
* Epic Loot's augment choice and crafting result dialogs and the Haldor/Hildir panels use Auga's scrollbar.
* EquipmentAndQuickSlots' quick-slot bar sits above the health/stamina cluster instead of on top of the health bar (only while its position is still the default).

### 2.0.8 - Epic Loot enchanting table in Auga style

* New: Epic Loot's enchanting table window takes the Auga look on every tab (Sacrifice/Identify, Convert, Enchant, Augment, Disenchant, Runes, Upgrade, locked tabs too): Auga panel and tab column, tab art, buttons, inputs, diamond toggles, list backdrops and rows, scrollbars, dropdown lists, fonts.
* Shared restyle: toggles drawn as a box with a check sprite become Auga diamond toggles, TabHandler tabs on plain button art take Auga tab art, input fields on mod backdrop art take Auga input art, GuiBar fills keep their colour.

### 2.0.7 - Jotunn-built UI in Auga style

* New: every window built with Jotunn's GUIManager (`Create*` builders and `Apply*Style` appliers) takes the Auga look: panels, buttons, inputs, toggles, sliders, scrollbars, dropdown lists, fonts. Covers VNEI, StarLevelSystem config tool / Mod Config button / popups, Epic Loot's Jotunn buttons and scroll views, Jotunn's mod-compatibility window, colour/gradient pickers and map-overlay panel. Jotunn stays optional.
* Fix: runtime atlas sprites named `<sprite>(Clone)` are restyled like their originals; slider handles drawn with checkbox art become Auga knobs; AveriaSerif-Regular legacy texts take Source Sans.

### 2.0.6 - Character stats and HUD fixes

* New: character stats window behind a 5th inventory Info button; shows the real damage reduction per damage type (replaces the armor row), using MorgottTweaks' pooled resistance step when that mod is loaded.
* Fix: own TooHard/Bonus damage numbers use the vanilla damage-text colours.
* Fix: every replaced screen and the message HUD keep the vanilla root canvases.
* Fix: centre messages stay visible in build mode.
* Fix: tooltip extra lines join with \n so $token localization works.
* Fix: game Averia fonts added as Source Sans Pro fallbacks.
* Audit: lone texts measured by glyphs, rotated rects by all corners.

### 1.3.12 - Hud Improvements

* MessageCenter Text no longer blocks Build Menu.
* Moved Stars to be on health bar, to allow compatibility with other mods adding HP text below health bar.
* Added native crafting controls for Input and Arrows that allow other mods, like AAA to make use of native objects.
* Added `GetCraftingControls()` to Auga API (now version 1.6.0) to allow getting the crafting controls directly without having to look through Unity hierarchy

<details>
<summary><b>Changelog History</b> (<i>click to expand</i>)</summary>

### 1.3.11 - Tooltip Improvements

* Added a Default Complex Tooltip to ensure a tooltip is always available.

### 1.3.10 - Updates for 0.217.30

* Fixed Tooltip issue when Augmenting or Crafting in EpicLoot
* Fixed Connected Players Dialogue
* Reduced Font in Crafting Bench Recipe List
* Updated for Valheim 0.217.30

### 1.3.8/1.3.9 - Various Updates

* Fixing RenderScale Setting to actually work.
* Fixed Password Dialog and Reactivated it
* Updated Settings to allow External Modification
* Updated Tooltips to allowing showing of external customized data.
* Updated Repair Icon for external expansion.
* Now Compatible with KG's Valheim Enchantment System
* 1.3.9 - Removes Debug Output

### 1.3.7 - Disabled Auga Password

* Hotfix to disable the Auga Password Box

### 1.3.6 - Updated for Valheim 0.217.28

* NEW COMPATIBILITY
  * Auga now Supports **_Jewelcrafting_**.
    * The following features of Jewelcrafting are currently not implemented:
      * Item Socketing on the Gemcutter's Table.
      * Tooltips for Socketed Items show as the vanilla tooltip, not the Auga Tooltip.
    * These will be implemented in the near-term future updates.
* CHANGES
  * Updated for Valheim 0.217.17 and 0.217.28.
  * Updated Unity to 2022.3.12!
* BUG FIXES
  * Fixed text visibility issues in the Settings Window with respect to non-Latin languages.
  * Fixed the Settings Drop Down for Language and Resolution to **FINALLY** work appropriately.
  * Adding the Darken Background back to provide the shadowing
    * This also addresses mods that were keying off of the Darken gameobject for backwards compatibility.

### 1.3.5 - Refactored for Valheim 0.217.24 Update Part 6

* NEW FEATURES
  * Comfy's Chatter UI Immersion
    * Chatter Buttons Now Show as Auga Buttons
    * Status Effects Auto-Hide to prevent overlapping of Chat Box when chatting.
  * Completely Rebuilt Auga Build Menu
    * Applies Auga theme to Sears Catalog
* BUG FIXES
  * Fixed a repeating error on Death in the BarberController.
  * Fixed an issue where text could overlap in upgrade menu
  * Fixing Build Menu Categories with JVL and Hammer Tooling

### 1.3.4 - Refactored for Valheim 0.217.24 Update Part 5

* NEW FEATURES
  * Refactored Auga to allow Vanilla Status effects to be added
    * I have re-enabled the Status Effect Template and Status Effect Root
      * Mods can now utilize these fields from the HUD.
    * This has allowed Valheim Legends to be compatible and fully functional with Auga
    * There is now a moveable window in Auga for Ability Buttons and Other Status effects in addition to the normal Status Effects List.
      * Defaults Ability Buttons position to just to the left of the Minimal Statuses under the map.
        * Will always stack vertically.
  * Refactored Barber UI for a better look.

### 1.3.3 - Refactored for Valheim 0.217.24 Update Part 4

* NEW FEATURES
  * Added Auga UI for Barber Station
  * Adjusted Compatibility to re-enable SkillsDialog so that other mods can hook in to adjust skills as needed.
    * This is the second half to the change in EpicLoot to allow EpicLoot to send Skill Bonus information to the Auga UI.
* BUG FIXES
  * Compendium Weakness Updates Wrong
  * Vegetation Settings were stopping at MEDIUM
  * Build Menu adjusted to ensure JVL does not complain

### 1.3.2 - Refactored for Valheim 0.217.24 Update Part 3

* Updating Shop Buy Text Button
* Updating Text Input for Signs and Portals to Function
* Updating a AugaTextsDialogeLore Error when in Compendium

### 1.3.1 - Refactored for Valheim 0.217.24 Update Part 2

* Updating Sleep Text to TMP Text

### 1.3.0 - Refactored for Valheim 0.217.24 Update

* NEW COMPATIBILITY
  * Passive Powers compatibility added to Auga
* KNOWN ISSUES
  * Compendium Weakness Updates Wrong **(pre-existing bug)**
  * EpicLoot +Weapon Skills aren't represented in Auga Skills Window (actual increase still applies) **(pre-existing bug)**
  * Mods that add Categories to Build Hud Cause Errors **(pre-existing bug - due to a change in JVL)**
    * Low priority, disable Auga Build Menu (in config) or Use Sears Catalog until fixed.
* NOT FIXING
  * No Barber Station Auga UI (uses Vanilla UI)
  * No Current Players Auga UI (uses Vanilla UI)
* BUGS FIXED
  * Console Crashing
  * Fields Updated for TMP to allow loading
  * Rune Text Animation Display Not Working
  * Settings Errors out Hard
  * Compendium Left Scroll Alignment issue
  * Crafting Label Changes to "Label" when crafting
  * EpicLoot Error when viewing Enchanting Table in Auga
  * On Dedicated Servers, No Players Option (untested as to whether this will error)
  * MessageHud Causes null reference exception when unlocking known texts.
  * Auga's Build Menu updated, refactored, and working
  * Adjust Text values on Two Buttons and 4 Labels in Settings
  * Crafting Stats not Showing Up
  * Reclaim N Recycle Title overlapping
  * Trader Menu causing errors, not showing items.

### 1.2.17

* Adding appropriate Dependency Checks to that Mod Detection actually works.
* Fixing a logic error where it wasn't respecting the priority of Chatter and Sears Catalog correctly
  * This will provide the priority.

### 1.2.16

* Fixed Password Dialogue Box
* Fixed Console Issue
* Fixed Chat Input positioning issue
* Removed Blackbox from under Keybind in Hover Text's
* Added Support for Comfy's Chatter Mod
* Added Additional Support for Comfy's Sears Catalog

### 1.2.15

* Hildir's Request 0.217.14 Update
* Known Issue: The chat input box is in the middle of the box.  Minor issue. Not game breaking.

### 1.2.14

* Fixed the TextMeshPro Blurry Fonts (thanks to Azumatt).
* Put NPC Text back into a smaller box so that the text wraps appropriately.
* Fixed Outline around Biome Name

### 1.2.13

* Password dialogue now hides password.
* Auga API has been updated to allow TooltipTextBox AddLine to overwrite instead of add.
* Fixed (again) Enemy Nameplates to be clear.
* Added Outlines on some HUD TMP Text boxes that were missing

### 1.2.12

* Fixing Password, Portal, Signs, and Tamable Inputs
* Removed some left over debugging

### 1.2.11

* Mini-map pins were not working.
  * Now have mini-map pins working.
* Chat Window text now wraps
* NPC Dialog now wraps

### 1.2.10

* Updates Valheim 0.216.9
* Adds in additional fonts to hopefully fix blurry text on unit frames.

### 1.2.9

* Hotfix for Blurry Text
* Added in Chinese, Japanese, Korean, Russian, and other languages to fonts.
  * This should now make most languages appear correctly.
  * If you are still seeing boxes, please report that to the Discord.

### 1.2.8

* Build Menu has been rebuilt to work with other mods that add hammers/categories.
  * Any mod using Jotunn 2.11.4 or higher to add categories will now work in build menu
  * This includes Odin Architect and ValheimRaft to name a few.
* Added in Chinese, Japanese, Korean, Russian, and other languages to fonts.
  * This should now make most languages appear correctly.
  * If you are still seeing boxes, please report that to the Discord.

### 1.2.7

* Fixes random loading issues with camera and UI lock out.
* Completely redesigned how StoreGui is attached to Auga.
* Better Trader and Knarr the Trader both now work together

### 1.2.6

* Better Trader now loads fully, and has been tested for compatibility.
* Knarr the Trader compatibility has been set.
  * Known Issue: Both Knarr and Better Trader currently don't work at the same time with Auga
* Additional tweaks to Build Menu Controller in order to support Jotunn and HammerTime Compatibility

### 1.2.5

* Build Menu now respects other mods changes to Categories
* Build menu now has pagination of categories when needed.
* Repair Icon is now activated and visible when in Debug/No Cost mode.
* Store Gui has been reconfigured to allow other mods to utilize the Store/Trader

### 1.2.4

* All Inventories now display Quality Diamond Correctly.

### 1.2.3

* Updating Map to show Pin Labels
* Updating Minimap Biome Label
* Updating Inventory to load item Quality Diamond correctly.
* Updating TextInput dialog boxes and providing Cancel and OK buttons
* Added Build Menu Toggle Configuration setting for turning off the Auga Build Menu
  * This is for mod compatibility where otherwise the build menu would break
  * Setting requires a game relog/restart.

### 1.2.2

* Fixed Chat Box
* Fixed resolution settings from resetting everytime settings are changed.
* All Player HUD Elements have been activated.
* Build Menu has been restored.
* Minimap has been restored.
* Enemy Hud Restored
* All Features of Auga should now be working.

### 1.2.1

* Now Compatible with 0.214.300.5 of Valheim (latest branch)
* 1.2.0 was one version behind and the latest version changed a field name breaking the Compendium.
* NOTE:
  * All Menu's, Compendium, Settings, Inventory, and Crafting Interactions SHOULD be working without error.
  * All HUD Elements, like status bars, have been disabled, and the vanilla versions should be displayed.
    * This is temporary as we update the rest of the mod.

### 1.2.0

* Initial Compatibility for Valheim 0.214.300 Update
* All Menu's, Compendium, Settings, Inventory, and Crafting Interactions SHOULD be working without error.
* All HUD Elements, like status bars, have been disabled, and the vanilla versions should be displayed.
  * This is temporary as we update the rest of the mod.
* Adding in DiamondButton to Asset Bundle
* Fixing Compendium Scroll Bar so that it will scroll all entries.

### 1.1.3

* Hotfix for new settings from new Valheim version
* Fix for store item tooltips

### 1.1.2

* Fixed an issue with Lore Compendium not populating
* Build Hud, Selected Piece, Top Left Message, Center Message, and Chat are all movable
* Eitr stats correctly visible in tooltips
* Upgrade item icon correctly displays on the crafting panel
* Added two new loading screen art pieces from the official Valheim press kit
* Minor scrollbar fixes in Compendium and Crafting panel

### 1.1.1

* Fixed animating pause menu buttons
* All HUD elements are now freely movable and scalable, use the config
* Health bars are customizable: fixed size, text position, text display options

### 1.1.0

* Updated for Mistlands!
* Mistlands specific UI and tooltips added
* Compatibility with Simple Recycling Fixed by remmiz

### 1.0.12

* Temporary fix for Valheim v0.211 - disabled auga in main menu until I have time to build the new save management menus
* Settings menu fixed

### 1.0.11

* Hotfix for Valheim v0.209.8

### 1.0.10

* Hotfix for Valheim v0.208.1

### 1.0.9

* Fixed bugs with ZInput preventing Auga from running with the new Valheim update
* Restored the vanilla logo in the main menu
* Added support for [MultiCraft](https://www.nexusmods.com/valheim/mods/263)!

### 1.0.8

* Fixed Minimap/Map
* Fixed Settings
* Fixed issue with custom build menus (Odin's Architect, Clutter, Buildit, Planit)
* Added ComplexTooltip callback to crafting menu
* Updated API with callbacks for food, status effect, and skill tooltips as well

### 1.0.7

* Fixed issue with tower shield tooltips
* Fixed cartography table map issue
* Fixed various screen alignment/resolution issues

### 1.0.6

* Reupload with correct files

### 1.0.5

* Fixed build HUD selector
* Fixed some screen alignment/resolution issues
* Hooked up Last IP Joined

### 1.0.4

* Updated for H&H
* Implemented Auga-style Stagger Bar

### 1.0.3

* BetterTrader bugfix
* Extended Item Data Framework compatibility (please update EIDF to 1.0.8)
* Added BuildExpansion-like support
* Fixed white square on store buy button
* Added support for more skills on the skills page
* Added trash support (like TrashItems), enable it in the config

### 1.0.2

* Fixed overlapping names and health bars for enemies when using CLLC

### 1.0.1

* Valheim+ Compatibility

</details>
