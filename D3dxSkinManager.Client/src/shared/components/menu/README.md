# Menu Components

Custom menu components for context menus and dropdowns with smart positioning, animations, and theme-aware styling.

## Components

### ContextMenu

Low-level context menu component. Requires manual position management.

**Use when:**
- You need full control over menu positioning (e.g., dropdown from a button)
- You want to position the menu relative to a specific element
- You're implementing complex positioning logic

```tsx
import { ContextMenu, ContextMenuItem } from 'shared/components/menu';

const [visible, setVisible] = useState(false);
const [position, setPosition] = useState({ x: 0, y: 0 });

// Position menu below a button
const handleButtonClick = (e: React.MouseEvent) => {
  const rect = e.currentTarget.getBoundingClientRect();
  setPosition({ x: rect.right, y: rect.bottom + 4 });
  setVisible(true);
};

<ContextMenu
  items={menuItems}
  visible={visible}
  position={position}
  onClose={() => setVisible(false)}
/>
```

### PopupMenu

High-level wrapper component that manages position from mouse events automatically.

**Use when:**
- You want a simple right-click context menu on an element
- Position tracking should happen automatically at cursor position
- You don't need custom positioning logic

```tsx
import { PopupMenu, ContextMenuItem } from 'shared/components/menu';

const menuItems: ContextMenuItem[] = [
  { key: '1', label: 'Action 1', onClick: () => {} },
  { key: '2', label: 'Action 2', onClick: () => {} },
  { type: 'divider' },
  { key: '3', label: 'Delete', danger: true, onClick: () => {} },
];

<PopupMenu items={menuItems}>
  <div>Right-click me!</div>
</PopupMenu>
```

### usePopupMenu Hook

Custom hook for managing popup menu state and position.

**Use when:**
- You need to track which item was right-clicked (e.g., list items, tree nodes)
- You want automatic position tracking but need custom trigger logic
- You're building complex interactive components

```tsx
import { usePopupMenu, ContextMenu, ContextMenuItem } from 'shared/components/menu';

const menuState = usePopupMenu();
const [selectedItem, setSelectedItem] = useState(null);

// Simple usage with getTriggerProps
<div {...menuState.getTriggerProps()}>
  Right-click me!
</div>

// Advanced usage with custom logic
<div onContextMenu={(e) => {
  e.preventDefault();
  setSelectedItem(item); // Track which item was clicked
  menuState.show(e);     // Show menu at cursor
}}>
  Right-click me!
</div>

<ContextMenu
  items={getMenuItems(selectedItem)}
  visible={menuState.visible}
  position={menuState.position}
  onClose={() => {
    menuState.hide();
    setSelectedItem(null);
  }}
/>
```

## Menu Item Structure

```typescript
interface ContextMenuItem {
  key?: string;           // Unique identifier (optional for dividers)
  label?: string;         // Menu item text
  icon?: React.ReactNode; // Optional icon
  danger?: boolean;       // Red styling for destructive actions
  disabled?: boolean;     // Disable the item
  visible?: boolean;      // Hide the item (default: true)
  onClick?: () => void;   // Click handler
  type?: 'divider';       // Divider type
}
```

## Features

- **Smart Positioning**: Automatically adjusts position to stay within viewport bounds
- **Animations**: Smooth vertical expand/collapse animations (top-down or bottom-up)
- **Theme-Aware**: Adapts styling to light/dark themes
- **Keyboard Support**: Close with Escape key
- **Auto-Close**: Closes on outside click or scroll events
- **TypeScript**: Full type safety with TypeScript

## Examples

### Right-Click Menu on List Items

```tsx
const menuState = usePopupMenu();
const [selectedMod, setSelectedMod] = useState<ModInfo | null>(null);

<List.Item
  onContextMenu={(e) => {
    e.preventDefault();
    setSelectedMod(mod);
    menuState.show(e);
  }}
>
  {/* List item content */}
</List.Item>

{selectedMod && (
  <ContextMenu
    items={getModMenuItems(selectedMod)}
    visible={menuState.visible}
    position={menuState.position}
    onClose={() => {
      menuState.hide();
      setSelectedMod(null);
    }}
  />
)}
```

### Dropdown Menu from Button

```tsx
const [menuVisible, setMenuVisible] = useState(false);
const [menuPosition, setMenuPosition] = useState({ x: 0, y: 0 });
const buttonRef = useRef<HTMLButtonElement>(null);

const handleClick = () => {
  if (buttonRef.current) {
    const rect = buttonRef.current.getBoundingClientRect();
    setMenuPosition({ x: rect.right, y: rect.bottom + 4 });
    setMenuVisible(true);
  }
};

<Button ref={buttonRef} onClick={handleClick}>
  Options
</Button>

<ContextMenu
  items={menuItems}
  visible={menuVisible}
  position={menuPosition}
  onClose={() => setMenuVisible(false)}
/>
```

## Migration Guide

### From Ant Design Dropdown

**Before:**
```tsx
<Dropdown menu={{ items: menuItems }} trigger={['contextMenu']}>
  <div>Right-click me</div>
</Dropdown>
```

**After:**
```tsx
<PopupMenu items={convertMenuItems(menuItems)}>
  <div>Right-click me</div>
</PopupMenu>
```

### Converting Menu Items

Ant Design menu items need to be converted to `ContextMenuItem` format:

```tsx
const convertMenuItems = (antdItems: MenuProps['items']): ContextMenuItem[] => {
  return antdItems.map((item: any) => ({
    key: item.key,
    label: item.label,
    icon: item.icon,
    danger: item.danger,
    disabled: item.disabled,
    onClick: item.onClick,
    type: item.type,
  }));
};
```
