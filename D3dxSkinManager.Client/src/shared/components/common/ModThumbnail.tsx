import React from 'react';
import { Image } from 'antd';
import { PictureOutlined } from '@ant-design/icons';
import { toAppUrl } from '../../utils/imageUrlHelper';

export interface ModThumbnailProps {
  thumbnailPath?: string;
  alt?: string;
}

export const ModThumbnail: React.FC<ModThumbnailProps> = ({ thumbnailPath, alt = 'Thumbnail' }) => {
  if (!thumbnailPath) {
    return <PictureOutlined style={{ fontSize: '24px', color: '#d9d9d9' }} />;
  }

  // Convert file path to app:// scheme URL
  const imageUrl = toAppUrl(thumbnailPath) || undefined;

  return (
    <Image
      width={50}
      height={100}
      src={imageUrl}
      alt={alt}
      fallback="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
      preview={{
        src: imageUrl
      }}
    />
  );
};
