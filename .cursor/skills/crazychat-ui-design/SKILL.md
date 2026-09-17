---
name: crazychat-ui-design
description: Maintains CrazyChat Overlay UI visual and interaction consistency. Use when creating, modifying, reviewing, or styling UI panels, cards, dialogs, settings pages, chat windows, tool buttons, inputs, dropdowns, lists, bubbles, or HUD elements under Project/Assets/Scripts/Overlay/.
---

# CrazyChat UI Design

## Before editing

1. Read `docs/game-ui-rules.md` and `docs/ai-code-guide.md`.
2. Inspect the closest existing interface. Prefer settings/chat for windows and `OverlayTodoUi` for a compact anchored card.
3. Extend the responsible existing `*Ui`; do not add a UI framework, manager, event bus, Canvas, or fallback implementation.

## Structure

- Use existing Canvas layers: `BagLayer`, `FriendLayer`, `FxLayer`, `ChromeLayer`, `WindowLayer`, `ModalLayer`.
- Assemble only in `FriendOverlayView.Build`; keep the View limited to wiring.
- Migrated interfaces load their required Prefab and bind existing nodes only. Do not recreate missing static controls at runtime.
- Unmigrated interfaces may keep one `Create()` + uGUI construction path until migrated.
- Use uGUI `Text` and `OverlaySprites.UiFont`. Decorative graphics must set `raycastTarget = false`.
- Render window UI at 1:1 pixel scale. Use integer sizes and positions where practical.

## Visual language

- Derive all colors from `OverlaySkin`; never hardcode a parallel palette.
- Main card: `ThemeBackground(theme)`.
- Header: `ThemeHeader(theme)` or the card background when the adjacent established interface does so.
- Section/body: `ThemeSection(theme)`.
- Input: `ThemeInputBackground(theme)`.
- Hover: `ThemeHover(theme)`.
- Divider/1px outline: `ThemeDivider(theme)`.
- Primary or selected state: `ThemeAccent(theme)`; use low alpha for selected backgrounds.
- Primary text: `SettingsThemeText(theme)`.
- Secondary/placeholder/completed text: `ThemeMuted(theme)`.
- Destructive action: `ThemeDanger(theme)`.

Use `OverlaySprites.RoundedRect` for cards and rounded surfaces, `OverlaySprites.Circle` for compact icon buttons, and the existing `control_rect`/`square_rect` resources for low-radius controls and 1px dividers. Keep corners restrained, spacing compact, and shadows disabled.

## Typography and controls

- Window title: approximately 14px, left aligned with 12px inset.
- Body/control text: approximately 13px.
- Tool icon: 16px in an approximately 28px hit area.
- Use existing Codicons as transparent PNG sprites and tint them with theme text or accent color.
- Tool buttons rest transparent, show `ThemeHover` on hover, and do not add a permanent filled background.
- Inputs use theme input background, themed caret/selection, and a subtle divider-colored boundary.
- Selected/active state must be visible through accent color, not through unrelated hardcoded color.
- Keep 1px separators between major regions; do not use drop shadows.

## Behavior

- Preserve established open, close, back, outside-click, focus, Enter, and Esc behavior from `docs/game-ui-rules.md`.
- Theme application must include inactive Prefab templates and dynamically cloned rows.
- Reapplying a theme must be idempotent: do not stack listeners, hover relays, outlines, or effects.
- Dynamic lists use dictionaries keyed by stable IDs and destroy stale rows; do not rebuild every frame.
- Visual transparency must not change intended hit, hover, drag, or selection areas.

## Scope boundaries

- New interactions remain desktop-avatar only and use `IOverlayInteractAction` → catalog → `OverlayInteractFx` → channel 2.
- Do not add interactions, A/B effects, key reactions, or desktop bubbles to the bag.
- Chat remains channel 1. Do not create another transport or Canvas.

## Verification

- Check dark, light, and custom accent themes.
- Check normal, hover, selected/active, disabled, completed, and destructive states that exist.
- Check long Chinese text, empty lists, list overflow, screen-edge placement, and 1:1 text clarity.
- Confirm decorative images do not intercept clicks and transparent controls retain their hit areas.
- Confirm Prefab-backed interfaces do not add tabs, pages, or static controls at runtime.
- Do not wait for Unity compilation or batchmode verification; report any Play Mode checks left to the user.
