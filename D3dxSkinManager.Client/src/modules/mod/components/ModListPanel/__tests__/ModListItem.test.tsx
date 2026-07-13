import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import type { ModInfo } from "../../../../../shared/types/mod.types";

// i18n: echo keys so labels/titles are assertable without a real bundle.
vi.mock("react-i18next", () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: "en" } }),
}));

import { ModListItem } from "../ModListItem";

const mk = (over: Partial<ModInfo> = {}): ModInfo =>
  ({
    id: "m1",
    name: "Test Mod",
    isLoaded: false,
    isLoading: false,
    isAvailable: true,
    isOrphaned: false,
    categoryName: "ZZZ",
    tags: [],
    metadata: undefined,
    ...over,
  }) as unknown as ModInfo;

const baseProps = {
  isPrimarySelection: false,
  isInMultiSelection: false,
  isBusy: false,
  isUnavailable: false,
  selectedModIds: [] as string[],
  gameUpdatedUtc: undefined,
  onRowClick: vi.fn(),
  onLoad: vi.fn(),
  onUnload: vi.fn(),
  onEdit: vi.fn(),
  onContextMenu: vi.fn(),
};

describe("ModListItem", () => {
  it("renders the mod name and category", () => {
    render(<ModListItem {...baseProps} mod={mk()} />);
    expect(screen.getByText("Test Mod")).toBeInTheDocument();
    expect(screen.getByText("ZZZ")).toBeInTheDocument();
  });

  it("shows the loaded tag only when loaded (and not loading/busy)", () => {
    const { rerender } = render(<ModListItem {...baseProps} mod={mk({ isLoaded: false })} />);
    expect(screen.queryByText("mods.list.loaded")).toBeNull();
    rerender(<ModListItem {...baseProps} mod={mk({ isLoaded: true })} />);
    expect(screen.getByText("mods.list.loaded")).toBeInTheDocument();
  });

  it("load button calls onLoad when unloaded, onUnload when loaded", () => {
    const onLoad = vi.fn();
    const onUnload = vi.fn();
    const { rerender } = render(
      <ModListItem {...baseProps} mod={mk({ isLoaded: false })} onLoad={onLoad} onUnload={onUnload} />,
    );
    fireEvent.click(screen.getByTitle("mods.list.loadMod"));
    expect(onLoad).toHaveBeenCalledWith("m1");
    expect(onUnload).not.toHaveBeenCalled();

    rerender(<ModListItem {...baseProps} mod={mk({ isLoaded: true })} onLoad={onLoad} onUnload={onUnload} />);
    fireEvent.click(screen.getByTitle("mods.list.unloadMod"));
    expect(onUnload).toHaveBeenCalledWith("m1");
  });

  it("edit button calls onEdit with the mod", () => {
    const onEdit = vi.fn();
    const mod = mk();
    render(<ModListItem {...baseProps} mod={mod} onEdit={onEdit} />);
    fireEvent.click(screen.getByTitle("mods.list.editMod"));
    expect(onEdit).toHaveBeenCalledWith(mod);
  });

  it("right-click calls onContextMenu with the mod", () => {
    const onContextMenu = vi.fn();
    const mod = mk();
    const { container } = render(<ModListItem {...baseProps} mod={mod} onContextMenu={onContextMenu} />);
    fireEvent.contextMenu(container.querySelector(".mod-list-item")!);
    expect(onContextMenu).toHaveBeenCalledWith(mod, expect.anything());
  });

  it("double-click toggles load/unload by loaded state", () => {
    const onLoad = vi.fn();
    const onUnload = vi.fn();
    const { container, rerender } = render(
      <ModListItem {...baseProps} mod={mk({ isLoaded: false })} onLoad={onLoad} onUnload={onUnload} />,
    );
    fireEvent.doubleClick(container.querySelector(".mod-list-item")!);
    expect(onLoad).toHaveBeenCalledWith("m1");

    rerender(<ModListItem {...baseProps} mod={mk({ isLoaded: true })} onLoad={onLoad} onUnload={onUnload} />);
    fireEvent.doubleClick(container.querySelector(".mod-list-item")!);
    expect(onUnload).toHaveBeenCalledWith("m1");
  });
});
