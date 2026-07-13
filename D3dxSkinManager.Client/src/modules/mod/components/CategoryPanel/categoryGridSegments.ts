import type React from 'react';
import { CategoryInfo } from '../../../../shared/types/category.types';

/** A pending drag-drop position relative to a target node. */
export interface DropIndicator {
  targetNodeId: string;
  position: 'before' | 'after' | 'inside';
}

/** An ordered segment: a run of card-nodes or a single expanded group. */
export type GridSegment =
  | { type: 'cards'; nodes: CategoryInfo[] }
  | { type: 'group'; node: CategoryInfo };

/**
 * Split an ordered list of nodes into segments that preserve the original order.
 * Leaves and collapsed parents are batched into consecutive "cards" segments;
 * expanded parents break out as individual "group" segments.
 * Shared by CategoryGrid (root segments) and CategoryGroup (child segments).
 */
export const buildSegments = (
  nodes: CategoryInfo[],
  expandedKeys: React.Key[],
): GridSegment[] => {
  const segments: GridSegment[] = [];
  let currentCards: CategoryInfo[] = [];

  for (const node of nodes) {
    const isExpandedParent = node.children.length > 0 && expandedKeys.includes(node.id);
    if (isExpandedParent) {
      if (currentCards.length > 0) {
        segments.push({ type: 'cards', nodes: currentCards });
        currentCards = [];
      }
      segments.push({ type: 'group', node });
    } else {
      currentCards.push(node);
    }
  }
  if (currentCards.length > 0) {
    segments.push({ type: 'cards', nodes: currentCards });
  }
  return segments;
};
