import React from 'react';
import { Button, Space, Typography, Divider } from 'antd';
import { useSlideInScreen } from '../../context/SlideInScreenContext';

const { Title, Paragraph } = Typography;

/**
 * Demo component to showcase multi-level slide-in screen stacking
 * This component demonstrates how screens stack with blur indicators
 */
export const SlideInScreenDemo: React.FC = () => {
  const { openScreen } = useSlideInScreen();

  const openLevel1Screen = () => {
    openScreen({
      title: 'Level 1 Screen',
      width: '70%',
      content: (
        <div>
          <Title level={3}>First Level Screen</Title>
          <Paragraph>
            This is the first level screen. Notice the small blur area on the left (8% width)
            indicating you're one level deep from the main content.
          </Paragraph>
          <Paragraph>
            Click the button below to open a second level screen on top of this one.
          </Paragraph>
          <Space>
            <Button type="primary" onClick={openLevel2Screen}>
              Open Level 2 Screen
            </Button>
          </Space>

          <Divider />

          <Paragraph>
            <strong>Features:</strong>
          </Paragraph>
          <ul>
            <li>Full-screen slide-in from the right</li>
            <li>Blur backdrop indicator on the left</li>
            <li>Smooth animations</li>
            <li>ESC key to close</li>
            <li>Click backdrop to close</li>
            <li>Theme-aware colors</li>
          </ul>
        </div>
      ),
    });
  };

  const openLevel2Screen = () => {
    openScreen({
      title: 'Level 2 Screen',
      width: '60%',
      content: (
        <div>
          <Title level={3}>Second Level Screen</Title>
          <Paragraph>
            This is the second level screen stacked on top of Level 1.
            The blur area is now wider (12% width) with stronger blur effect,
            indicating you're two levels deep.
          </Paragraph>
          <Paragraph>
            You can stack even more screens! Click below to open Level 3.
          </Paragraph>
          <Space>
            <Button type="primary" onClick={openLevel3Screen}>
              Open Level 3 Screen
            </Button>
          </Space>

          <Divider />

          <Paragraph>
            <strong>Blur Levels:</strong>
          </Paragraph>
          <ul>
            <li>Level 1: 8% width, light blur (4px)</li>
            <li>Level 2: 12% width, medium blur (8px)</li>
            <li>Level 3+: 16%+ width, heavy blur (12px)</li>
          </ul>
        </div>
      ),
    });
  };

  const openLevel3Screen = () => {
    openScreen({
      title: 'Level 3 Screen',
      width: '50%',
      content: (
        <div>
          <Title level={3}>Third Level Screen</Title>
          <Paragraph>
            This is the third level! The blur area is even wider (16% width)
            with the strongest blur effect, clearly showing you're three levels deep.
          </Paragraph>
          <Paragraph>
            Try closing screens in any order:
          </Paragraph>
          <ul>
            <li>Click the X button in the header</li>
            <li>Press ESC key</li>
            <li>Click the blur backdrop on the left</li>
          </ul>

          <Divider />

          <Title level={4}>Application-Style Interface</Title>
          <Paragraph>
            Unlike traditional modals, these slide-in screens provide an application-style
            experience where each screen represents a distinct context or task.
          </Paragraph>
          <Paragraph>
            This pattern is perfect for:
          </Paragraph>
          <ul>
            <li>Multi-step wizards</li>
            <li>Nested forms and details</li>
            <li>Progressive disclosure</li>
            <li>Task hierarchies</li>
          </ul>
        </div>
      ),
    });
  };

  return (
    <div style={{ padding: '24px' }}>
      <Title level={2}>Slide-In Screen System Demo</Title>
      <Paragraph>
        This demo showcases the new slide-in screen system that replaces traditional popups
        with an application-style interface.
      </Paragraph>

      <Space size="large" orientation="vertical" style={{ width: '100%' }}>
        <div>
          <Title level={4}>Try It Out</Title>
          <Paragraph>
            Click the button below to open the first screen. You can then stack multiple
            screens on top of each other to see how the blur indicators work.
          </Paragraph>
          <Button type="primary" size="large" onClick={openLevel1Screen}>
            Open Demo Screen
          </Button>
        </div>

        <Divider />

        <div>
          <Title level={4}>How It Works</Title>
          <Paragraph>
            Each screen slides in from the right with a fast 200ms animation, leaving a blur
            backdrop on the left that grows wider and more blurred with each level. This
            provides a clear visual indicator of how deep you are in the navigation hierarchy.
          </Paragraph>
        </div>

        <div>
          <Title level={4}>Implementation</Title>
          <Paragraph>
            Use the <code>useSlideInScreen</code> hook to open screens programmatically:
          </Paragraph>
          <pre style={{
            background: 'var(--color-bg-elevated)',
            padding: '16px',
            borderRadius: '8px',
            overflow: 'auto'
          }}>
{`import { useSlideInScreen } from '@/context/SlideInScreenContext';

const { openScreen } = useSlideInScreen();

openScreen({
  title: 'My Screen',
  width: '70%',
  content: <YourComponent />
});`}
          </pre>
        </div>
      </Space>
    </div>
  );
};
