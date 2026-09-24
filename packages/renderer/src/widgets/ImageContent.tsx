export interface ImageContentProps {
  text?: string;
  src?: string;
}

export function ImageContent({ text, src }: ImageContentProps) {
  return (
    <div className="ms-content ms-image">
      {src ? <img className="ms-image-img" src={src} alt="" /> : <span className="ms-text">—</span>}
      {text && <span className="ms-text">{text}</span>}
    </div>
  );
}
