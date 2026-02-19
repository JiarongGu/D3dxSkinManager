import React from 'react';
import { Image } from 'antd';
import { PictureOutlined } from '@ant-design/icons';

export interface ModThumbnailProps {
  thumbnailPath?: string;
  alt?: string;
}

export const ModThumbnail: React.FC<ModThumbnailProps> = ({ thumbnailPath, alt = 'Thumbnail' }) => {
  if (!thumbnailPath) {
    return <PictureOutlined style={{ fontSize: '24px', color: '#d9d9d9' }} />;
  }

  // Backend now returns data URIs directly, no need for file:/// prefix
  return (
    <Image
      width={50}
      height={100}
      src={thumbnailPath}
      alt={alt}
      fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
      preview={{
        src: thumbnailPath
      }}
    />
  );
};
