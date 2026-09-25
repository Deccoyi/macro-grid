import type { ReactNode } from "react";

/** Renders the small Markdown subset release notes use (headings, bullet lists, bold, inline code, paragraphs) as React
 * elements. The text never becomes HTML: everything goes through React's escaping, links are shown as their text and
 * raw HTML shows up as plain text, so a release body from outside can never inject script or navigate the window. */
export function SafeMarkdown({ text }: { text: string }) {
  const lines = text.replace(/<!--[\s\S]*?-->/g, "").replace(/\r\n?/g, "\n").split("\n");
  const blocks: ReactNode[] = [];
  let paragraph: string[] = [];
  let items: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length === 0) return;
    blocks.push(<p key={blocks.length} style={{ margin: "0 0 8px" }}>{inline(paragraph.join(" "))}</p>);
    paragraph = [];
  };
  const flushItems = () => {
    if (items.length === 0) return;
    blocks.push(
      <ul key={blocks.length} style={{ margin: "0 0 8px", paddingLeft: 20 }}>
        {items.map((item, i) => <li key={i}>{inline(item)}</li>)}
      </ul>,
    );
    items = [];
  };

  for (const raw of lines) {
    const line = raw.trim();
    const heading = /^#{1,6}\s+(.*)$/.exec(line);
    const bullet = /^[-*+]\s+(.*)$/.exec(line);
    if (heading) {
      flushParagraph();
      flushItems();
      blocks.push(<div key={blocks.length} style={{ fontWeight: 600, margin: "10px 0 4px" }}>{inline(heading[1] ?? "")}</div>);
    } else if (bullet) {
      flushParagraph();
      items.push(bullet[1] ?? "");
    } else if (line === "") {
      flushParagraph();
      flushItems();
    } else {
      flushItems();
      paragraph.push(line);
    }
  }
  flushParagraph();
  flushItems();

  return <div style={{ fontSize: 12.5, lineHeight: 1.5, overflowWrap: "anywhere" }}>{blocks}</div>;
}

/** Bold and inline code; `[label](url)` keeps only the label. */
function inline(text: string): ReactNode[] {
  const plain = text.replace(/\[([^\]]*)\]\([^)]*\)/g, "$1");
  return plain.split(/(\*\*[^*]+\*\*|`[^`]+`)/g).map((part, i) => {
    if (part.startsWith("**") && part.endsWith("**") && part.length > 4) return <strong key={i}>{part.slice(2, -2)}</strong>;
    if (part.startsWith("`") && part.endsWith("`") && part.length > 2) {
      return <code key={i} style={{ fontFamily: "Consolas, 'Cascadia Mono', monospace", fontSize: 11.5 }}>{part.slice(1, -1)}</code>;
    }
    return part;
  });
}
