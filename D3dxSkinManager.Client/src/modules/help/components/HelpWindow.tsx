/**
 * Help Window — renders the user guide (docs/user-guide/USER_GUIDE.{en,cn}.md) inside the app.
 *
 * The markdown files are the SINGLE source of truth (also the repo docs); they're raw-imported here and
 * rendered by the zero-dep MarkdownView. The guide is split into its `## ` (H2) sections, shown with a
 * vertical nav — so editing the docs updates the in-app help automatically, in both languages.
 */
import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import classNames from 'classnames';
import { MarkdownView } from '../../../shared/components/common/MarkdownView';
// eslint-disable-next-line import/no-unresolved -- Vite ?raw (typed by vite/client); path is the repo docs.
import guideEn from '../../../../../docs/user-guide/USER_GUIDE.en.md?raw';
// eslint-disable-next-line import/no-unresolved
import guideCn from '../../../../../docs/user-guide/USER_GUIDE.cn.md?raw';
import './HelpWindow.css';

interface GuideSection {
  title: string;
  body: string;
}

/** Split the guide into its `## ` sections; the text before the first `## ` becomes the intro section. */
function splitSections(md: string, introTitle: string): GuideSection[] {
  const parts = md.replace(/\r\n/g, '\n').split(/\n(?=## )/);
  const sections: GuideSection[] = [];
  const intro = (parts[0] ?? '').trim();
  if (intro) sections.push({ title: introTitle, body: intro });
  for (let i = 1; i < parts.length; i++) {
    const m = parts[i].match(/^##\s+(.*)/);
    sections.push({ title: (m ? m[1] : `Section ${i}`).trim(), body: parts[i] });
  }
  return sections;
}

export const HelpWindow: React.FC = () => {
  const { i18n } = useTranslation();
  const lang = (i18n.language || 'en').toLowerCase();
  const isCn = lang.startsWith('cn') || lang.startsWith('zh');

  const sections = useMemo(
    () => splitSections(isCn ? guideCn : guideEn, isCn ? '概览' : 'Overview'),
    [isCn],
  );

  const [active, setActive] = useState(0);
  const current = sections[active] ?? sections[0];

  return (
    <div className="help-window-layout">
      <div className="help-window-nav">
        {sections.map((s, i) => (
          <div
            key={s.title}
            className={classNames('help-window-nav-item', {
              'help-window-nav-item--active': active === i,
            })}
            onClick={() => setActive(i)}
          >
            <span className="help-window-nav-item-label">{s.title}</span>
          </div>
        ))}
      </div>

      <div className="help-window-content-area">
        {current && <MarkdownView source={current.body} />}
      </div>
    </div>
  );
};
