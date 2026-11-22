import { Lightbulb, Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api } from '../api'
import { Empty, ErrorState, Loading, PageHeader, useAsync } from '../components/ui'

export function RulesPage() {
  const [search, setSearch] = useState('')
  const [term, setTerm] = useState('')
  useEffect(() => { const t = setTimeout(() => setTerm(search), 250); return () => clearTimeout(t) }, [search])
  const { data, error, loading, reload } = useAsync(() => api.rules({ search: term || undefined }), [term])

  return (
    <div className="space-y-6">
      <PageHeader title="Reglas para memorizar" subtitle="Todas las reglas del banco, agrupadas por módulo." />
      <div className="relative max-w-md">
        <Search className="absolute left-3 top-2.5 h-4 w-4 text-night/40" aria-hidden />
        <label htmlFor="rule-search" className="sr-only">Buscar reglas</label>
        <input id="rule-search" className="input pl-9" placeholder="Buscar (ej. S3, VPC, IAM)…" value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>
      {loading ? <Loading /> : error || !data ? <ErrorState message={error ?? 'Sin datos'} onRetry={reload} /> : data.length === 0 ? <Empty title="No se encontraron reglas" /> : (
        <div className="grid gap-4 md:grid-cols-2">
          {data.map((g) => (
            <section key={g.moduleId} className="card">
              <h2 className="mb-3 flex items-center gap-2 font-bold"><Lightbulb className="h-4 w-4 text-amber" aria-hidden /> {g.module}</h2>
              <ul className="space-y-2 text-sm">
                {g.rules.map((r) => (
                  <li key={r.questionId} className="rounded-lg bg-night/[0.03] p-2">
                    <span className="mr-1 text-xs font-semibold text-night/50">#{r.originalId}</span>{r.rule}
                    {r.needsReview && <span className="chip ml-2 bg-amber/15 text-[#9A5A0B]">En revisión</span>}
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
