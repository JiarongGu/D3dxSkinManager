import React from 'react';

interface HighlightCellRendererProps {
  value: string;
  searchConfig?: {
    find: string;
    caseSensitive: boolean;
    useRegex: boolean;
  } | null;
  colDef: any;
}

export const HighlightCellRenderer: React.FC<HighlightCellRendererProps> = ({
  value,
  searchConfig,
  colDef,
}) => {
  if (!value || typeof value !== 'string') {
    return <span>{value}</span>;
  }

  // No highlighting if no search
  if (!searchConfig || !searchConfig.find) {
    return <span>{value}</span>;
  }

  try {
    let parts: { text: string; highlight: boolean }[] = [];

    if (searchConfig.useRegex) {
      // Regex search
      const flags = searchConfig.caseSensitive ? 'g' : 'gi';
      const regex = new RegExp(searchConfig.find, flags);
      let lastIndex = 0;
      let match;

      while ((match = regex.exec(value)) !== null) {
        // Add non-matching part
        if (match.index > lastIndex) {
          parts.push({ text: value.substring(lastIndex, match.index), highlight: false });
        }
        // Add matching part
        parts.push({ text: match[0], highlight: true });
        lastIndex = match.index + match[0].length;
      }

      // Add remaining text
      if (lastIndex < value.length) {
        parts.push({ text: value.substring(lastIndex), highlight: false });
      }

      // If no matches, return original
      if (parts.length === 0) {
        return <span>{value}</span>;
      }
    } else {
      // Plain text search
      const searchText = searchConfig.find;
      const compareValue = searchConfig.caseSensitive ? value : value.toLowerCase();
      const compareSearch = searchConfig.caseSensitive ? searchText : searchText.toLowerCase();

      let startIndex = 0;
      let index = compareValue.indexOf(compareSearch, startIndex);

      if (index === -1) {
        return <span>{value}</span>;
      }

      while (index !== -1) {
        // Add non-matching part
        if (index > startIndex) {
          parts.push({ text: value.substring(startIndex, index), highlight: false });
        }
        // Add matching part
        parts.push({ text: value.substring(index, index + searchText.length), highlight: true });
        startIndex = index + searchText.length;
        index = compareValue.indexOf(compareSearch, startIndex);
      }

      // Add remaining text
      if (startIndex < value.length) {
        parts.push({ text: value.substring(startIndex), highlight: false });
      }
    }

    return (
      <span>
        {parts.map((part, i) => (
          part.highlight ? (
            <mark
              key={i}
              style={{
                backgroundColor: 'var(--search-highlight-bg)',
                color: 'inherit',
                padding: 0,
              }}
            >
              {part.text}
            </mark>
          ) : (
            <span key={i}>{part.text}</span>
          )
        ))}
      </span>
    );
  } catch (error) {
    // Invalid regex or other error
    return <span>{value}</span>;
  }
};
