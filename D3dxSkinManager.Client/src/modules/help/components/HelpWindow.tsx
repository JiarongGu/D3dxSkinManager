/**
 * Help Window — renders the user guide (docs/user-guide/USER_GUIDE.{en,cn}.md) inside the app.
 *
 * The markdown files are the SINGLE source of truth (also the repo docs); they're raw-imported here and
 * rendered by the zero-dep MarkdownView. Structure: `# ` = a doc GROUP (Overview / Examples / Features /
 * Configuration / About), `## ` = a page within it. The nav shows grouped, icon-labelled sections — so
 * editing the docs updates the in-app help automatically, in both languages.
 */
import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import {
  InfoCircleOutlined,
  BulbOutlined,
  AppstoreOutlined,
  SettingOutlined,
  FileTextOutlined,
} from '@ant-design/icons';
import { MarkdownView } from '../../../shared/components/common/MarkdownView';
// eslint-disable-next-line import/no-unresolved -- Vite ?raw (typed by vite/client); path is the repo docs.
import guideEn from '../../../../../docs/user-guide/USER_GUIDE.en.md?raw';
// eslint-disable-next-line import/no-unresolved
import guideCn from '../../../../../docs/user-guide/USER_GUIDE.cn.md?raw';
import './HelpWindow.css';

interface Page { title: string; body: string; index: number }
interface Group { title: string; pages: Page[] }

/** Parse the guide into `# ` groups, each with its `## ` pages; assigns a flat page index for selection. */
function parseGuide(md: string): { groups: Group[]; pages: Page[] } {
  const norm = md.replace(/\r\n/g, '\n');
  const groups: Group[] = [];
  const pages: Page[] = [];
  let fi = 0;
  for (const chunk of norm.split(/\n(?=# )/)) {
    const gm = chunk.match(/^#\s+(.*)/);
    if (!gm) continue;
    const title = gm[1].trim();
    const afterTitle = chunk.replace(/^#\s+.*\n?/, '');
    const gpages: Page[] = [];
    const pageChunks = afterTitle.split(/\n(?=## )/).map((s) => s.trim()).filter(Boolean);
    for (const pc of pageChunks) {
      const pm = pc.match(/^##\s+(.*)/);
      if (!pm) continue;
      const page = { title: pm[1].trim(), body: pc, index: fi++ };
      gpages.push(page);
      pages.push(page);
    }
    if (gpages.length > 0) groups.push({ title, pages: gpages });
  }
  return { groups, pages };
}

/** Pick a nav icon from a group title (matches EN + CN keywords). */
function groupIcon(title: string): React.ReactNode {
  const t = title.toLowerCase();
  if (/(example|walkthrough|案例|示例|演练|上手)/.test(t)) return <BulbOutlined />;
  if (/(feature|功能)/.test(t)) return <AppstoreOutlined />;
  if (/(config|设置|配置)/.test(t)) return <SettingOutlined />;
  if (/(overview|about|概览|关于|简介)/.test(t)) return <InfoCircleOutlined />;
  return <FileTextOutlined />;
}

export const HelpWindow: React.FC = () => {
  const { i18n } = useTranslation();
  const lang = (i18n.language || 'en').toLowerCase();
  const isCn = lang.startsWith('cn') || lang.startsWith('zh');

  const { groups, pages } = useMemo(() => parseGuide(isCn ? guideCn : guideEn), [isCn]);
  const [active, setActive] = useState(0);
  const current = pages[active] ?? pages[0];

  return (
    <div className="help-window-layout">
      <div className="help-window-nav">
        {groups.map((g) => (
          <div key={g.title} className="help-window-nav-group">
            <div className="help-window-nav-group-title">
              <span className="help-window-nav-group-icon">{groupIcon(g.title)}</span>
              {g.title}
            </div>
            {g.pages.map((p) => (
              <div
                key={p.index}
                className={classNames('help-window-nav-item', {
                  'help-window-nav-item--active': active === p.index,
                })}
                onClick={() => setActive(p.index)}
              >
                <span className="help-window-nav-item-label">{p.title}</span>
              </div>
            ))}
          </div>
        ))}
      </div>

      <div className="help-window-content-area">
        {current && <MarkdownView source={current.body} />}
      </div>
    </div>
  );
};
