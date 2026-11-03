import { useCallback, useEffect, useId, useRef, useState, useSyncExternalStore } from "react";
import { ChevronDown, X } from "lucide-react";
import { markImageMissing, subscribeImages, thumbSrc } from "../../api/optionIcon";
import type { OptionsResult, SettingOption } from "../../api/types";
import { useT } from "../../i18n/I18nContext";

export const SEARCH_DEBOUNCE_MS = 250;
const PAGE_SIZE = 50;

export interface SearchSelectProps {
  /** The stored option value. */
  value: string;
  /** The label the editor saw when the value was picked; shown while the source cannot answer. */
  storedLabel?: string;
  /** Asks the options source for one page; `signal` aborts it when a newer request replaces it. */
  search: (query: { q: string; offset: number; limit: number; signal: AbortSignal }) => Promise<OptionsResult>;
  /** Re-run the first page when this changes (the fields the options depend on). */
  depsKey?: string;
  ariaLabel: string;
  onPick: (option: SettingOption | null) => void;
  /** Reports the option behind the stored value once the source has answered it (null when it is gone). */
  onResolved?: (option: SettingOption | null) => void;
}

/** A thumbnail for an option icon; hidden when the picture cannot be loaded. */
export function OptionIcon({ icon }: { icon?: string | null }) {
  const src = useSyncExternalStore(subscribeImages, () => thumbSrc(icon));
  if (!src) return null;
  return <img className="pf-opt-icon" src={src} alt="" width={20} height={20} onError={() => markImageMissing(icon)} />;
}

const isAbort = (err: unknown) => err instanceof DOMException && err.name === "AbortError";

/** A combobox over a searchable, paged options source: typing searches (debounced), scrolling to the end loads the next page. */
export function SearchSelect({ value, storedLabel, search, depsKey, ariaLabel, onPick, onResolved }: SearchSelectProps) {
  const { t } = useT();
  const uid = useId();
  const listId = `${uid}-list`;
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [options, setOptions] = useState<SettingOption[]>([]);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);
  const [active, setActive] = useState(0);
  const controller = useRef<AbortController | null>(null);
  const generation = useRef(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const textRef = useRef("");
  const searchRef = useRef(search);
  searchRef.current = search;
  const valueRef = useRef(value);
  valueRef.current = value;
  const resolvedRef = useRef(onResolved);
  resolvedRef.current = onResolved;
  const listRef = useRef<HTMLUListElement | null>(null);

  const load = useCallback((q: string, offset: number) => {
    controller.current?.abort();
    const ctl = new AbortController();
    controller.current = ctl;
    const id = ++generation.current;
    setLoading(true);
    searchRef.current({ q, offset, limit: PAGE_SIZE, signal: ctl.signal })
      .then((res) => {
        if (id !== generation.current) return; // a newer request replaced this one
        setLoading(false);
        setError(res.error ?? null);
        setHasMore(Boolean(res.hasMore) && !res.error);
        setOptions((prev) => (offset === 0 ? res.options : [...prev, ...res.options]));
        if (offset === 0) setActive(0);
        if (q === "" && offset === 0 && !res.error) {
          const found = res.options.find((o) => o.value === valueRef.current) ?? null;
          setMissing(valueRef.current !== "" && found === null);
          resolvedRef.current?.(found);
        }
      })
      .catch((err) => {
        if (id !== generation.current || isAbort(err)) return;
        setLoading(false);
        setHasMore(false);
        setError(err instanceof Error ? err.message : String(err));
      });
  }, []);

  // The stored value is checked against the source once, and again when the fields it depends on change.
  useEffect(() => {
    load("", 0);
    return () => { controller.current?.abort(); generation.current++; if (timer.current) clearTimeout(timer.current); };
  }, [depsKey, load]);

  const openList = () => {
    if (open) return;
    setOpen(true);
    setText("");
    textRef.current = "";
    load("", 0);
  };
  const close = () => {
    setOpen(false);
    if (timer.current) clearTimeout(timer.current);
  };
  const type = (next: string) => {
    setOpen(true);
    setText(next);
    textRef.current = next;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => load(next, 0), SEARCH_DEBOUNCE_MS);
  };
  const loadMore = () => {
    if (hasMore && !loading) load(textRef.current, options.length);
  };
  const pick = (o: SettingOption) => {
    onPick(o);
    close();
  };
  const move = (delta: number) => {
    if (!open) { openList(); return; }
    const next = Math.min(Math.max(active + delta, 0), options.length - 1);
    setActive(next);
    if (next === options.length - 1) loadMore();
    listRef.current?.querySelector(`[data-index="${next}"]`)?.scrollIntoView?.({ block: "nearest" });
  };

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") { e.preventDefault(); move(1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); move(-1); }
    else if (e.key === "Enter" && open) {
      e.preventDefault();
      const o = options[active];
      if (o) pick(o);
    } else if (e.key === "Escape" && open) { e.preventDefault(); e.stopPropagation(); close(); }
    else if (e.key === "Tab") close();
  };

  const shownLabel = options.find((o) => o.value === value)?.label ?? storedLabel ?? value;
  const display = value === "" ? "" : missing ? t("schemaForm.search.unavailable", shownLabel) : shownLabel;

  let lastGroup: string | null | undefined;
  const rows = options.map((o, i) => {
    const header = o.group && o.group !== lastGroup ? <li key={`g-${i}`} role="presentation" className="pf-opt-group">{o.group}</li> : null;
    lastGroup = o.group;
    return [
      header,
      <li
        key={`${o.value}-${i}`}
        id={`${uid}-opt-${i}`}
        role="option"
        data-index={i}
        aria-selected={o.value === value}
        className={i === active ? "pf-opt active" : "pf-opt"}
        onMouseDown={(e) => e.preventDefault()}
        onMouseEnter={() => setActive(i)}
        onClick={() => pick(o)}
      >
        <OptionIcon icon={o.icon} />
        <span className="pf-ellipsis">{o.label}</span>
      </li>,
    ];
  });

  return (
    <div className="pf-search" onBlur={(e) => { if (!e.currentTarget.contains(e.relatedTarget)) close(); }}>
      <div className="pf-sel">
        <input
          type="text"
          role="combobox"
          aria-label={ariaLabel}
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={open && options[active] ? `${uid}-opt-${active}` : undefined}
          autoComplete="off"
          value={open ? text : display}
          placeholder={open ? t("schemaForm.search.placeholder") : t("form.pickPlaceholder")}
          onFocus={openList}
          onClick={openList}
          onChange={(e) => type(e.target.value)}
          onKeyDown={onKeyDown}
          style={{ paddingRight: value ? 48 : 24 }}
        />
        {value !== "" && (
          <button type="button" className="ghost pf-icon-btn small pf-search-clear" title={t("schemaForm.search.clear")} aria-label={t("schemaForm.search.clear")} onClick={() => onPick(null)}>
            <X size={12} />
          </button>
        )}
        <span className="chev" aria-hidden="true"><ChevronDown size={14} /></span>
      </div>
      {open && (
        <ul
          id={listId}
          ref={listRef}
          role="listbox"
          aria-label={ariaLabel}
          className="pf-search-list"
          onScroll={(e) => {
            const el = e.currentTarget;
            if (el.scrollTop + el.clientHeight >= el.scrollHeight - 8) loadMore();
          }}
        >
          {rows}
          {loading && <li role="presentation" className="pf-opt-note">{t("schemaForm.search.loading")}</li>}
          {!loading && options.length === 0 && !error && <li role="presentation" className="pf-opt-note">{t("schemaForm.search.empty")}</li>}
        </ul>
      )}
      {error && <span role="alert" className="pf-hint error">{error}</span>}
    </div>
  );
}
