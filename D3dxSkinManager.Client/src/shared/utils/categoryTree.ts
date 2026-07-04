import type { CategoryInfo } from '../types/category.types';

/**
 * Flatten a category tree into a flat node list (depth-first, parents before children —
 * preserves visual order). Replaces the per-component copies that used to live in
 * CategoryScreen, CategoryGrid, ModEditScreen and categoryService.
 */
export function flattenCategoryTree(nodes: CategoryInfo[]): CategoryInfo[] {
  const result: CategoryInfo[] = [];
  for (const node of nodes) {
    result.push(node);
    if (node.children.length > 0) {
      result.push(...flattenCategoryTree(node.children));
    }
  }
  return result;
}

/**
 * Flatten a category tree into breadcrumb-labelled select options (`Parent > Child`).
 * Used by CategorySelect and the ModPackageTool export filter.
 */
export function flattenCategoryOptions(
  cats: CategoryInfo[],
  prefix = '',
): { value: string; label: string }[] {
  const result: { value: string; label: string }[] = [];
  for (const cat of cats) {
    const label = prefix ? `${prefix} > ${cat.name}` : cat.name;
    result.push({ value: cat.id, label });
    result.push(...flattenCategoryOptions(cat.children, label));
  }
  return result;
}
