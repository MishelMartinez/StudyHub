import { AlertTriangle, Loader2, Inbox } from 'lucide-react'
import type { ReactNode } from 'react'
import { useCallback, useEffect, useState } from 'react'
import type { Mastery } from '../api'

export function useAsync<T>(fn: () => Promise<T>, deps: unknown[]) {
  const [data, setData] = useState<T | undefined>()
  const [error, setError] = useState<string | undefined>()
  const [loading, setLoading] = useState(true)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, deps)
  const reload = useCallback(async () => {
    setLoading(true)
    setError(undefined)
    try { setData(await run()) } catch (e) { setError(e instanceof Error ? e.message : String(e)) } finally { setLoading(false) }
  }, [run])
  useEffect(() => { void reload() }, [reload])
  return { data, error, loading, reload, setData }
}

export function Loading({ label = 'Cargando…' }: { label?: string }) {
  return (
    <div role="status" className="flex items-center justify-center gap-2 py-16 text-night/60">
      <Loader2 className="h-5 w-5 animate-spin text-rose" aria-hidden /> {label}
    </div>
  )
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="card flex flex-col items-center gap-3 border-coral/30 bg-coral/5 text-center">
      <AlertTriangle className="h-8 w-8 text-coral" aria-hidden />
      <p className="font-medium text-night">{message}</p>
      {onRetry && <button className="btn-ghost" onClick={onRetry}>Reintentar</button>}
    </div>
  )
}

export function Empty({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="card flex flex-col items-center gap-2 py-12 text-center">
      <Inbox className="h-10 w-10 text-blush" aria-hidden />
      <p className="font-semibold">{title}</p>
      {children && <div className="muted max-w-md">{children}</div>}
    </div>
  )
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="h1">{title}</h1>
        {subtitle && <p className="muted mt-1">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

export function ProgressBar({ value, tone = 'rose', label }: { value: number; tone?: 'rose' | 'amber' | 'coral' | 'night'; label?: string }) {
  const color = { rose: 'bg-rose', amber: 'bg-amber', coral: 'bg-coral', night: 'bg-night' }[tone]
  const v = Math.max(0, Math.min(100, value))
  return (
    <div className="h-2 w-full overflow-hidden rounded-full bg-blush/30" role="progressbar" aria-valuenow={Math.round(v)} aria-valuemin={0} aria-valuemax={100} aria-label={label}>
      <div className={`h-full rounded-full ${color} transition-all`} style={{ width: `${v}%` }} />
    </div>
  )
}

export function Stat({ label, value, hint, tone = 'night' }: { label: string; value: ReactNode; hint?: ReactNode; tone?: 'night' | 'rose' | 'amber' | 'coral' }) {
  const color = { night: 'text-night', rose: 'text-rose', amber: 'text-amber', coral: 'text-coral' }[tone]
  return (
    <div className="card">
      <p className="muted">{label}</p>
      <p className={`mt-1 text-3xl font-bold ${color}`}>{value}</p>
      {hint && <p className="mt-1 text-xs text-night/50">{hint}</p>}
    </div>
  )
}

const masteryLabels: Record<Mastery, [string, string]> = {
  nueva: ['Nueva', 'bg-night/5 text-night/70'],
  en_progreso: ['En progreso', 'bg-amber/15 text-[#9A5A0B]'],
  con_errores: ['Con errores', 'bg-coral/10 text-coral'],
  dominada: ['Dominada', 'bg-rose/15 text-rose-dark'],
}

export function MasteryChip({ mastery }: { mastery: Mastery }) {
  const [label, cls] = masteryLabels[mastery] ?? masteryLabels.nueva
  return <span className={`chip ${cls}`}>{label}</span>
}

export function ReviewWarning({ notes }: { notes: string[] }) {
  return (
    <div className="flex gap-3 rounded-xl border border-amber/40 bg-amber/10 p-3 text-sm text-night" role="note">
      <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber" aria-hidden />
      <div>
        <p className="font-semibold">Pregunta en revisión</p>
        <p className="text-night/70">La respuesta de esta pregunta no está confirmada. Por defecto se excluye de los exámenes.</p>
        {notes.length > 0 && <ul className="mt-1 list-disc pl-5 text-night/70">{notes.map((n) => <li key={n}>{n}</li>)}</ul>}
      </div>
    </div>
  )
}

export const pct = (n: number) => `${Math.round(n)}%`
export const fmtDate = (s: string | null | undefined) => (s ? new Date(s).toLocaleString('es-MX', { dateStyle: 'medium', timeStyle: 'short' }) : '—')
export const fmtDuration = (secs: number | null | undefined) => {
  if (secs == null) return '—'
  const m = Math.floor(secs / 60), s = secs % 60
  return m >= 60 ? `${Math.floor(m / 60)} h ${m % 60} min` : `${m} min ${String(s).padStart(2, '0')} s`
}
