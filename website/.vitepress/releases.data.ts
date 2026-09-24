import { defineLoader } from 'vitepress'

export interface ServerRelease {
  tag: string
  /** Tag without the "server-v" prefix and without the "-alpha" suffix. */
  version: string
  prerelease: boolean
  publishedAt: string
  notesUrl: string
  asset: { name: string; size: number; url: string }
}

declare const data: ServerRelease[]
export { data }

const API = 'https://api.github.com/repos/Deccoyi/macro-grid/releases?per_page=30'

// Runs at build time. It must never fail the build: any problem yields an empty list
// and the page shows its fallback.
export default defineLoader({
  async load(): Promise<ServerRelease[]> {
    try {
      const headers: Record<string, string> = {
        Accept: 'application/vnd.github+json',
        'User-Agent': 'macro-grid-website',
      }
      if (process.env.GITHUB_TOKEN) headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`
      const res = await fetch(API, { headers, signal: AbortSignal.timeout(15000) })
      if (!res.ok) return []
      const json = await res.json()
      if (!Array.isArray(json)) return []

      const out: ServerRelease[] = []
      for (const r of json) {
        if (r.draft || typeof r.tag_name !== 'string' || !r.tag_name.startsWith('server-v')) continue
        const exe = (r.assets ?? []).find((a: any) => typeof a.name === 'string' && a.name.toLowerCase().endsWith('.exe'))
        if (!exe) continue
        out.push({
          tag: r.tag_name,
          version: r.tag_name.slice('server-v'.length).replace(/-alpha$/i, ''),
          prerelease: !!r.prerelease,
          publishedAt: r.published_at ?? r.created_at ?? '',
          notesUrl: r.html_url,
          asset: { name: exe.name, size: exe.size, url: exe.browser_download_url },
        })
      }
      out.sort((a, b) => Date.parse(b.publishedAt) - Date.parse(a.publishedAt))
      return out
    } catch {
      return []
    }
  },
})
