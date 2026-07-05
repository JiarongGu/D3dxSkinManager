import React, { useEffect, useRef, useState } from 'react';
import classNames from 'classnames';
import './ImageGallery.css';

/**
 * ImageGallery — L2 molecule: a FIXED-height stage + a single horizontal thumbnail strip. Pure
 * presentational (images via props; optional resolveSrc maps remote URLs to cached local ones).
 * The stage height never changes with the image, so the thumb strip never jumps. Mouse wheel over
 * the strip scrolls it horizontally; the scrollbar is slim.
 *
 * HERO mode (backdrop + overlay): the current image also renders as a blurred, dimmed cover layer
 * behind the letterboxed foreground — the "mod page hero" look — and `overlay` renders over the
 * stage's bottom edge (title/tags/etc., supplied by the caller).
 */
export interface ImageGalleryProps {
  images: string[];
  /** Map a source URL to the src to render (e.g. cached local path); default = identity. */
  resolveSrc?: (url: string) => string;
  alt?: string;
  /** Stage height (CSS size). Default keeps the gallery stable across image sizes. */
  stageHeight?: string;
  /** Render the current image as a blurred cover layer behind the contained foreground. */
  backdropBlur?: boolean;
  /** Content rendered over the stage's bottom edge (gets a readability scrim). */
  overlay?: React.ReactNode;
  className?: string;
}

export const ImageGallery: React.FC<ImageGalleryProps> = ({
  images,
  resolveSrc,
  alt = '',
  stageHeight = 'min(56vh, 620px)',
  backdropBlur = false,
  overlay,
  className,
}) => {
  const [active, setActive] = useState(0);
  const stripRef = useRef<HTMLDivElement>(null);

  // Wheel over the strip scrolls HORIZONTALLY. Native non-passive listener — React's onWheel is
  // passive, so preventDefault there can't stop the page from scrolling vertically instead.
  useEffect(() => {
    const strip = stripRef.current;
    if (!strip) return;
    const onWheel = (e: WheelEvent) => {
      if (strip.scrollWidth <= strip.clientWidth) return; // nothing to scroll
      e.preventDefault();
      strip.scrollLeft += e.deltaY !== 0 ? e.deltaY : e.deltaX;
    };
    strip.addEventListener('wheel', onWheel, { passive: false });
    return () => strip.removeEventListener('wheel', onWheel);
  }, [images.length]);

  if (images.length === 0) return null;

  const src = (url: string) => (resolveSrc ? resolveSrc(url) : url);
  const current = images[Math.min(active, images.length - 1)];

  return (
    <div className={classNames('image-gallery', { 'image-gallery--hero': backdropBlur }, className)}>
      <div className="image-gallery__stage" style={{ height: stageHeight }}>
        {backdropBlur && (
          <img className="image-gallery__backdrop" src={src(current)} alt="" aria-hidden />
        )}
        <img className="image-gallery__main" src={src(current)} alt={alt} />
        {overlay && <div className="image-gallery__overlay">{overlay}</div>}
      </div>
      {images.length > 1 && (
        <div ref={stripRef} className="image-gallery__strip">
          {images.map((image, index) => (
            <img
              key={image}
              src={src(image)}
              alt=""
              loading="lazy"
              className={classNames('image-gallery__thumb', {
                'image-gallery__thumb--active': index === active,
              })}
              onClick={() => setActive(index)}
            />
          ))}
        </div>
      )}
    </div>
  );
};
