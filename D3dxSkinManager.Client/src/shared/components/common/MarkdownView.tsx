import React from 'react';
import './MarkdownView.css';

/**
 * L1 atom — a tiny, zero-dependency Markdown renderer (no external lib, so it survives the app's
 * self-contained bundling). Supports the subset the user guide uses: ATX headings (# … ####), ordered
 * and unordered lists (one level of nesting), fenced code blocks, horizontal rules, paragraphs, images
 * (![alt](src)), inline **bold** / `code` / [links](url), and GitHub-style typed callouts
 * (`> [!TIP]` / `[!NOTE]` / `[!WARNING]` / `[!IMPORTANT]` / `[!GOAL]`) rendered with an icon + color.
 * Pure props — no IPC/store.
 */
interface MarkdownViewProps {
  /** Raw markdown source. */
  source: string;
  className?: string;
}

const CALLOUT_ICON: Record<string, string> = {
  goal: '🎯',
  tip: '💡',
  note: '📌',
  info: 'ℹ️',
  important: '❗',
  warning: '⚠️',
};

/** Render inline images, **bold**, `code`, and [text](url) within a line of text. */
function renderInline(text: string, keyBase: string): React.ReactNode[] {
  const nodes: React.ReactNode[] = [];
  const re = /(!\[([^\]]*)\]\(([^)]+)\))|(\*\*([^*]+)\*\*)|(`([^`]+)`)|(\[([^\]]+)\]\(([^)]+)\))/g;
  let last = 0;
  let i = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text)) !== null) {
    if (m.index > last) nodes.push(text.slice(last, m.index));
    if (m[1]) {
      nodes.push(<img key={`${keyBase}-i${i}`} className="markdown-view__img" src={m[3]} alt={m[2]} loading="lazy" />);
    } else if (m[4]) {
      nodes.push(<strong key={`${keyBase}-b${i}`}>{m[5]}</strong>);
    } else if (m[6]) {
      nodes.push(<code key={`${keyBase}-c${i}`} className="markdown-view__code">{m[7]}</code>);
    } else if (m[8]) {
      nodes.push(
        <a key={`${keyBase}-l${i}`} href={m[10]} target="_blank" rel="noreferrer">{m[9]}</a>,
      );
    }
    last = re.lastIndex;
    i += 1;
  }
  if (last < text.length) nodes.push(text.slice(last));
  return nodes;
}

const leadingSpaces = (s: string) => s.length - s.trimStart().length;
const listMatch = (s: string) => s.trimStart().match(/^(\d+\.|[-*])\s+(.*)$/);

/** Parse a contiguous list starting at `start`; returns the element + the index after it. */
function parseList(lines: string[], start: number, key: string): [React.ReactNode, number] {
  const baseIndent = leadingSpaces(lines[start]);
  const ordered = /^\s*\d+\./.test(lines[start]);
  const items: React.ReactNode[] = [];
  let i = start;
  let n = 0;
  while (i < lines.length) {
    const line = lines[i];
    if (line.trim() === '') break;
    const mm = listMatch(line);
    if (!mm) break;
    const indent = leadingSpaces(line);
    if (indent < baseIndent) break; // belongs to a parent list

    const content = mm[2];
    let child: React.ReactNode = null;
    i += 1;
    if (i < lines.length && lines[i].trim() !== '' && listMatch(lines[i]) && leadingSpaces(lines[i]) > indent) {
      const [sub, next] = parseList(lines, i, `${key}-${n}s`);
      child = sub;
      i = next;
    }
    items.push(
      <li key={`${key}-${n}`}>
        {renderInline(content, `${key}-${n}`)}
        {child}
      </li>,
    );
    n += 1;
  }
  const el = ordered
    ? <ol key={key} className="markdown-view__ol">{items}</ol>
    : <ul key={key} className="markdown-view__ul">{items}</ul>;
  return [el, i];
}

function parseBlocks(md: string): React.ReactNode[] {
  const lines = md.replace(/\r\n/g, '\n').split('\n');
  const out: React.ReactNode[] = [];
  let i = 0;
  let key = 0;

  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    // Fenced code block.
    if (trimmed.startsWith('```')) {
      const buf: string[] = [];
      i += 1;
      while (i < lines.length && !lines[i].trim().startsWith('```')) {
        buf.push(lines[i]);
        i += 1;
      }
      i += 1;
      out.push(
        <pre key={`k${key++}`} className="markdown-view__pre"><code>{buf.join('\n')}</code></pre>,
      );
      continue;
    }

    // Horizontal rule.
    if (/^(-{3,}|\*{3,}|_{3,})$/.test(trimmed)) {
      out.push(<hr key={`k${key++}`} className="markdown-view__hr" />);
      i += 1;
      continue;
    }

    // Heading (# … ####).
    const h = trimmed.match(/^(#{1,4})\s+(.*)$/);
    if (h) {
      const level = h[1].length;
      const Tag = (`h${level}`) as keyof React.JSX.IntrinsicElements;
      out.push(
        <Tag key={`k${key++}`} className={`markdown-view__h markdown-view__h${level}`}>
          {renderInline(h[2], `h${key}`)}
        </Tag>,
      );
      i += 1;
      continue;
    }

    // Blockquote / typed callout (group consecutive `>` lines).
    if (trimmed.startsWith('>')) {
      const buf: string[] = [];
      while (i < lines.length && lines[i].trim().startsWith('>')) {
        buf.push(lines[i].trim().replace(/^>\s?/, ''));
        i += 1;
      }
      // Detect a `[!TYPE]` admonition marker on the first line.
      let type: string | undefined;
      const mk = buf[0]?.match(/^\[!(\w+)\]\s*(.*)$/);
      if (mk) {
        type = mk[1].toLowerCase();
        buf[0] = mk[2];
        if (buf[0] === '') buf.shift();
      }
      const body = renderInline(buf.join(' '), `q${key}`);
      if (type) {
        out.push(
          <div key={`k${key++}`} className={`markdown-view__callout markdown-view__callout--${type}`}>
            <span className="markdown-view__callout-icon">{CALLOUT_ICON[type] ?? '💬'}</span>
            <div className="markdown-view__callout-body">{body}</div>
          </div>,
        );
      } else {
        out.push(<blockquote key={`k${key++}`} className="markdown-view__quote">{body}</blockquote>);
      }
      continue;
    }

    // List.
    if (listMatch(line)) {
      const [el, next] = parseList(lines, i, `k${key++}`);
      out.push(el);
      i = next;
      continue;
    }

    // Blank line.
    if (trimmed === '') {
      i += 1;
      continue;
    }

    // Paragraph (group consecutive plain lines until a blank/special line).
    const para: string[] = [];
    while (i < lines.length) {
      const l = lines[i];
      const lt = l.trim();
      if (lt === '' || lt.startsWith('```') || lt.startsWith('>') || /^(#{1,4})\s+/.test(lt) ||
          /^(-{3,}|\*{3,}|_{3,})$/.test(lt) || listMatch(l)) {
        break;
      }
      para.push(lt);
      i += 1;
    }
    // A paragraph that is ONLY an image becomes a figure (block image).
    const joined = para.join(' ');
    const imgOnly = joined.match(/^!\[([^\]]*)\]\(([^)]+)\)$/);
    if (imgOnly) {
      out.push(
        <figure key={`k${key++}`} className="markdown-view__figure">
          <img className="markdown-view__img" src={imgOnly[2]} alt={imgOnly[1]} loading="lazy" />
          {imgOnly[1] ? <figcaption className="markdown-view__figcaption">{imgOnly[1]}</figcaption> : null}
        </figure>,
      );
    } else {
      out.push(<p key={`k${key++}`} className="markdown-view__p">{renderInline(joined, `p${key}`)}</p>);
    }
  }

  return out;
}

export const MarkdownView: React.FC<MarkdownViewProps> = ({ source, className }) => (
  <div className={className ? `markdown-view ${className}` : 'markdown-view'}>{parseBlocks(source)}</div>
);
