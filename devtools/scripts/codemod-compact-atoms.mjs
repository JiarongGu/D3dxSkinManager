// One-shot codemod: migrate raw antd form controls (Button/Input/TextArea/Select/Switch) to the
// compact L1 atom library across the given .tsx files (ui-component-layers.md sweep, 2026-07-05).
// Leaves every other antd component (Modal/Table/Spin/Tooltip/InputNumber/...) untouched.
// Usage: node devtools/scripts/codemod-compact-atoms.mjs <file...>
import fs from 'fs';
import path from 'path';

const COMPACT_DIR = path.resolve('D3dxSkinManager.Client/src/shared/components/compact');
const MIGRATE = ['Button', 'Input', 'Select', 'Switch']; // antd names we may remove from imports

for (const file of process.argv.slice(2)) {
  let src = fs.readFileSync(file, 'utf8');
  const orig = src;
  const used = new Set();

  // Tag replacements — order matters (Input.TextArea before Input; InputNumber must NOT match).
  const tag = (from, to, atom) => {
    const re = new RegExp(`<${from}(?![A-Za-z0-9.])`, 'g');
    const closeRe = new RegExp(`</${from}>`, 'g');
    if (re.test(src)) {
      src = src.replace(re, `<${to}`).replace(closeRe, `</${to}>`);
      used.add(atom);
    }
  };
  tag('Input\\.TextArea', 'CompactTextArea', 'CompactTextArea');
  tag('Input\\.Password', 'CompactPassword', 'CompactPassword');
  tag('TextArea', 'CompactTextArea', 'CompactTextArea'); // `const { TextArea } = Input` alias
  tag('Input', 'CompactInput', 'CompactInput');
  tag('Select', 'CompactSelect', 'CompactSelect');
  tag('Switch', 'CompactSwitch', 'CompactSwitch');
  tag('Button', 'CompactButton', 'CompactButton');

  if (used.size === 0) continue;

  // antd size="middle" is invalid on compact atoms (they use medium default) — drop it on migrated tags.
  src = src.replace(/(<Compact(?:Button|Input|Select|Switch|TextArea)[^>]*?)\s+size="middle"/g, '$1');

  // Drop the TextArea destructure alias if present and no longer referencing antd Input.
  src = src.replace(/\nconst \{ TextArea \} = Input;?\n/, '\n');
  src = src.replace(/\nconst \{ TextArea \} = CompactInput;?\n/, '\n');

  // Rewrite the antd import: remove migrated names that no longer appear as raw JSX or member refs.
  src = src.replace(/import \{([^}]+)\} from ['"]antd['"];?/, (m, names) => {
    const kept = names.split(',').map((n) => n.trim()).filter(Boolean).filter((n) => {
      if (!MIGRATE.includes(n)) return true;
      // Keep if the raw identifier is still used (e.g. Input.Search left behind, ButtonProps typing).
      const stillUsed = new RegExp(`<${n}(?![A-Za-z0-9])|[^a-zA-Z.]${n}\\.[A-Z]|\\b${n}Props`).test(
        src.replace(m, ''));
      return stillUsed;
    });
    return kept.length ? `import { ${kept.join(', ')} } from 'antd';` : '';
  });

  // Merge/append the compact barrel import.
  const rel = path
    .relative(path.dirname(path.resolve(file)), COMPACT_DIR)
    .split(path.sep)
    .join('/');
  const barrel = rel.startsWith('.') ? rel : `./${rel}`;
  const importRe = new RegExp(`import \\{([^}]+)\\} from ['"]${barrel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}['"];?`);
  if (importRe.test(src)) {
    src = src.replace(importRe, (m, names) => {
      const all = new Set(names.split(',').map((n) => n.trim()).filter(Boolean));
      used.forEach((u) => all.add(u));
      return `import { ${[...all].join(', ')} } from '${barrel}';`;
    });
  } else {
    // Insert after the last import line.
    const lines = src.split('\n');
    let lastImport = 0;
    lines.forEach((l, i) => { if (/^import /.test(l)) lastImport = i; });
    lines.splice(lastImport + 1, 0, `import { ${[...used].join(', ')} } from '${barrel}';`);
    src = lines.join('\n');
  }

  if (src !== orig) {
    fs.writeFileSync(file, src);
    console.log(`${file}: ${[...used].join(', ')}`);
  }
}
